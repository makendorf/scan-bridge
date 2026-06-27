using System.IO.Ports;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Models;
using ScanBridge.Parsers;
using ScanBridge.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5000");

// ── Регистрация сервисов ──

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite("Data Source=scanbridge.db"));

builder.Host.UseSerilog();

builder.Services.AddSingleton<LogCollector>();
builder.Services.AddSingleton<IBarcodeParser, SimpleBarcodeParser>();
builder.Services.AddSingleton<PostScanManager>();
builder.Services.AddSingleton<ScanProcessorService>();
builder.Services.AddSingleton<ScannerManager>();
builder.Services.AddSingleton<ScanTracker>();

// ── Windows Service (опционально) ──

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "ScanBridge";
    });
}

var app = builder.Build();

// ── Конфигурация Serilog с записью в БД ──

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Sink(new CollectorSink(() => app.Services.GetRequiredService<LogCollector>()))
    .CreateLogger();

// ── Миграция БД ──

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Ошибка миграции БД, попыткаEnsureCreated");
        db.Database.EnsureCreated();
    }
    SeedFromLegacyConfig(db);
}

// ── Конфигурация пост-скан действий (группы) ──

var postScanManager = app.Services.GetRequiredService<PostScanManager>();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var groupConfigs = db.PostScanActionGroups
        .OrderBy(g => g.SortOrder)
        .Select(g => new PostScanActionGroupConfig
        {
            Id = g.Id,
            Name = g.Name,
            Enabled = g.Enabled,
            ScannerNames = db.PostScanActionGroupScanners
                .Where(s => s.GroupId == g.Id)
                .Select(s => s.ScannerName)
                .ToList(),
            Actions = db.PostScanActions
                .Where(a => a.GroupId == g.Id)
                .OrderBy(a => a.SortOrder)
                .Select(a => new PostScanActionConfig
                {
                    Type = a.Type,
                    Enabled = a.Enabled,
                    Settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(a.SettingsJson) ?? new()
                })
                .ToList()
        })
        .ToList();
    postScanManager.Configure(groupConfigs);
}

// ── Вспомогательные функции ──

/// <summary>
/// Читает список сканеров из БД и возвращает их конфигурации.
/// </summary>
List<SerialPortConfig> ReadScanners()
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var reconnect = ReadReconnectConfig(db);
    return db.Scanners.OrderBy(s => s.SortOrder).Select(s => new SerialPortConfig
    {
        Name = s.Name,
        PortName = s.PortName,
        BaudRate = s.BaudRate,
        DataBits = s.DataBits,
        Parity = s.Parity,
        StopBits = s.StopBits,
        Handshake = s.Handshake,
        ReadTimeout = s.ReadTimeout,
        WriteTimeout = s.WriteTimeout,
        ControlCharMode = s.ControlCharMode,
        Reconnect = new ReconnectConfig
        {
            DelayMs = s.ReconnectDelayMs,
            MaxRetries = s.ReconnectMaxRetries,
            Continuous = s.ReconnectContinuous
        }
    }).ToList();
}

/// <summary>
/// Читает настройки переподключения из таблицы Settings.
/// </summary>
ReconnectConfig ReadReconnectConfig(AppDbContext db)
{
    return new ReconnectConfig
    {
        DelayMs = int.TryParse(db.Settings.FirstOrDefault(s => s.Key == "ReconnectDelayMs")?.Value, out var d) ? d : 1000,
        MaxRetries = int.TryParse(db.Settings.FirstOrDefault(s => s.Key == "ReconnectMaxRetries")?.Value, out var r) ? r : 10,
        Continuous = db.Settings.FirstOrDefault(s => s.Key == "ReconnectContinuous")?.Value == "true"
    };
}

// ── Запуск сканеров ──

var manager = app.Services.GetRequiredService<ScannerManager>();
manager.StartAll(ReadScanners());

// ── Статические файлы ──

app.UseDefaultFiles();
app.UseStaticFiles();

// ── API: Управление сканерами ──

/// <summary>
/// GET /api/scanners — возвращает список всех сканеров.
/// </summary>
app.MapGet("/api/scanners", () => Results.Ok(ReadScanners()));

/// <summary>
/// GET /api/scanners/status — возвращает статус каждого сканера (имя, порт, запущен ли).
/// </summary>
app.MapGet("/api/scanners/status", () =>
{
    var running = manager.GetRunning();
    var scanners = ReadScanners();
    return Results.Ok(scanners.Select(s => new
    {
        s.Name,
        s.PortName,
        Running = running.Contains(s.Name)
    }));
});

/// <summary>
/// GET /api/scanners/lastscan — возвращает время и имя сканера последнего сканирования.
/// </summary>
app.MapGet("/api/scanners/lastscan", () =>
{
    var tracker = app.Services.GetRequiredService<ScanTracker>();
    var (time, scannerName) = tracker.GetLastScan();
    return Results.Ok(new { time, scannerName });
});

/// <summary>
/// POST /api/scanners/{name}/restart — перезапускает сканер по имени.
/// </summary>
app.MapPost("/api/scanners/{name}/restart", (string name) =>
{
    var scanners = ReadScanners();
    var config = scanners.FirstOrDefault(s => s.Name == name);
    if (config == null) return Results.NotFound(new { error = $"Сканер '{name}' не найден" });
    manager.RestartScanner(config);
    return Results.Ok(new { message = $"Сканер '{name}' перезапущен" });
});

/// <summary>
/// POST /api/scanners — добавляет новый сканер и запускает его.
/// </summary>
app.MapPost("/api/scanners", (SerialPortConfig scanner) =>
{
    if (string.IsNullOrWhiteSpace(scanner.Name) || string.IsNullOrWhiteSpace(scanner.PortName))
        return Results.BadRequest(new { error = "Name и PortName обязательны" });

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var maxOrder = db.Scanners.Any() ? db.Scanners.Max(s => s.SortOrder) + 1 : 0;
    db.Scanners.Add(new ScannerConfig
    {
        Name = scanner.Name,
        PortName = scanner.PortName,
        BaudRate = scanner.BaudRate,
        DataBits = scanner.DataBits,
        Parity = scanner.Parity,
        StopBits = scanner.StopBits,
        Handshake = scanner.Handshake,
        ReadTimeout = scanner.ReadTimeout,
        WriteTimeout = scanner.WriteTimeout,
        SortOrder = maxOrder,
        ControlCharMode = scanner.ControlCharMode,
        ReconnectDelayMs = scanner.Reconnect?.DelayMs ?? 1000,
        ReconnectMaxRetries = scanner.Reconnect?.MaxRetries ?? 10,
        ReconnectContinuous = scanner.Reconnect?.Continuous ?? false
    });
    db.SaveChanges();
    var conflict = manager.StartScanner(scanner);
    return Results.Ok(new { scanners = ReadScanners(), conflict });
});

/// <summary>
/// PUT /api/scanners/{index} — обновляет конфигурацию сканера по индексу.
/// В зависимости от режима переподключения перезапускает один или все сканеры.
/// </summary>
app.MapPut("/api/scanners/{index:int}", (int index, SerialPortConfig scanner) =>
{
    if (string.IsNullOrWhiteSpace(scanner.Name) || string.IsNullOrWhiteSpace(scanner.PortName))
        return Results.BadRequest(new { error = "Name и PortName обязательны" });

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var all = db.Scanners.OrderBy(s => s.SortOrder).ToList();
    if (index < 0 || index >= all.Count) return Results.NotFound();
    var entity = all[index];
    entity.Name = scanner.Name;
    entity.PortName = scanner.PortName;
    entity.BaudRate = scanner.BaudRate;
    entity.DataBits = scanner.DataBits;
    entity.Parity = scanner.Parity;
    entity.StopBits = scanner.StopBits;
    entity.Handshake = scanner.Handshake;
    entity.ReadTimeout = scanner.ReadTimeout;
    entity.WriteTimeout = scanner.WriteTimeout;
    entity.ControlCharMode = scanner.ControlCharMode;
    entity.ReconnectDelayMs = scanner.Reconnect?.DelayMs ?? 1000;
    entity.ReconnectMaxRetries = scanner.Reconnect?.MaxRetries ?? 10;
    entity.ReconnectContinuous = scanner.Reconnect?.Continuous ?? false;
    db.SaveChanges();

    var list = ReadScanners();
    string? conflict = null;
    var mode = GetReconnectMode(db);
    if (mode == "all")
        manager.RestartAll(list);
    else
    {
        conflict = manager.CheckPortConflict(scanner.PortName, scanner.Name);
        manager.RestartScanner(scanner);
    }

    return Results.Ok(new { scanners = list, conflict });
});

/// <summary>
/// DELETE /api/scanners/{index} — удаляет сканер по индексу.
/// </summary>
app.MapDelete("/api/scanners/{index:int}", (int index) =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var all = db.Scanners.OrderBy(s => s.SortOrder).ToList();
    if (index < 0 || index >= all.Count) return Results.NotFound();
    var removed = all[index];
    db.Scanners.Remove(removed);
    db.SaveChanges();
    manager.StopScanner(removed.Name);
    return Results.Ok(ReadScanners());
});

// ── API: Логи ──

/// <summary>
/// GET /api/logs — возвращает последние записи логов (по умолчанию 500).
/// </summary>
app.MapGet("/api/logs", (int limit = 500) =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var entries = db.Logs
        .OrderByDescending(l => l.Timestamp)
        .Take(limit)
        .Select(l => new { l.Timestamp, l.Level, l.Message, l.Exception })
        .ToList();
    return Results.Ok(entries);
});

/// <summary>
/// DELETE /api/logs — очищает все записи логов.
/// </summary>
app.MapDelete("/api/logs", () =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Logs.ExecuteDelete();
    db.SaveChanges();
    return Results.Ok(new { message = "Логи очищены" });
});

// ── API: COM-порты ──

/// <summary>
/// GET /api/ports — возвращает список доступных COM-портов.
/// </summary>
app.MapGet("/api/ports", () =>
{
    try
    {
        var ports = SerialPort.GetPortNames();
        return Results.Ok(ports);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Не удалось получить список COM-портов");
        return Results.Ok(Array.Empty<string>());
    }
});

// ── API: Пост-скан действия ──

/// <summary>
/// GET /api/postscan/groups — возвращает все группы с действиями и привязками к сканерам.
/// </summary>
app.MapGet("/api/postscan/groups", () =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var groups = db.PostScanActionGroups
        .OrderBy(g => g.SortOrder)
        .Select(g => new
        {
            g.Id,
            g.Name,
            g.Enabled,
            ScannerNames = db.PostScanActionGroupScanners
                .Where(s => s.GroupId == g.Id)
                .Select(s => s.ScannerName)
                .ToList(),
            Actions = db.PostScanActions
                .Where(a => a.GroupId == g.Id)
                .OrderBy(a => a.SortOrder)
                .Select(a => new
                {
                    a.Id,
                    a.Type,
                    a.Enabled,
                    Settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(a.SettingsJson) ?? new Dictionary<string, string>()
                })
                .ToList()
        })
        .ToList();

    var enabled = postScanManager.GetEnabledActions();
    return Results.Ok(new { groups, enabled });
});

/// <summary>
/// PUT /api/postscan/groups — полная перезапись всех групп.
/// </summary>
app.MapPut("/api/postscan/groups", (List<PostScanActionGroupConfig> groupConfigs) =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.PostScanActionGroupScanners.ExecuteDelete();
    db.PostScanActions.ExecuteDelete();
    db.PostScanActionGroups.ExecuteDelete();

    for (var gi = 0; gi < groupConfigs.Count; gi++)
    {
        var gc = groupConfigs[gi];
        var group = new PostScanActionGroup
        {
            Name = gc.Name,
            Enabled = gc.Enabled,
            SortOrder = gi
        };
        db.PostScanActionGroups.Add(group);
        db.SaveChanges();

        foreach (var scannerName in gc.ScannerNames)
        {
            db.PostScanActionGroupScanners.Add(new PostScanActionGroupScanner
            {
                GroupId = group.Id,
                ScannerName = scannerName
            });
        }

        for (var ai = 0; ai < gc.Actions.Count; ai++)
        {
            var ac = gc.Actions[ai];
            db.PostScanActions.Add(new PostScanAction
            {
                GroupId = group.Id,
                Type = ac.Type,
                Enabled = ac.Enabled,
                SettingsJson = System.Text.Json.JsonSerializer.Serialize(ac.Settings ?? new()),
                SortOrder = ai
            });
        }
    }
    db.SaveChanges();

    var finalConfigs = db.PostScanActionGroups
        .OrderBy(g => g.SortOrder)
        .Select(g => new PostScanActionGroupConfig
        {
            Id = g.Id,
            Name = g.Name,
            Enabled = g.Enabled,
            ScannerNames = db.PostScanActionGroupScanners
                .Where(s => s.GroupId == g.Id)
                .Select(s => s.ScannerName)
                .ToList(),
            Actions = db.PostScanActions
                .Where(a => a.GroupId == g.Id)
                .OrderBy(a => a.SortOrder)
                .Select(a => new PostScanActionConfig
                {
                    Type = a.Type,
                    Enabled = a.Enabled,
                    Settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(a.SettingsJson) ?? new()
                })
                .ToList()
        })
        .ToList();

    postScanManager.Configure(finalConfigs);
    Log.Information("Группы пост-скан действий обновлены: {Count}", finalConfigs.Count);

    var enabled = postScanManager.GetEnabledActions();
    return Results.Ok(new { groups = finalConfigs, enabled });
});

// ── API: Настройки переподключения ──

/// <summary>
/// GET /api/settings/reconnect — возвращает текущий режим переподключения (single/all).
/// </summary>
app.MapGet("/api/settings/reconnect", () =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    return Results.Ok(new { mode = GetReconnectMode(db) });
});

/// <summary>
/// PUT /api/settings/reconnect — устанавливает режим переподключения.
/// </summary>
app.MapPut("/api/settings/reconnect", (ReconnectModeRequest req) =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    SetSetting(db, "ReconnectMode", req.Mode);
    Log.Information("Режим переподключения изменён на: {Mode}", req.Mode);
    return Results.Ok(new { mode = req.Mode });
});

/// <summary>
/// GET /api/settings/reconnect/config — возвращает параметры переподключения.
/// </summary>
app.MapGet("/api/settings/reconnect/config", () =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = ReadReconnectConfig(db);
    return Results.Ok(new { config.DelayMs, config.MaxRetries, config.Continuous });
});

/// <summary>
/// PUT /api/settings/reconnect/config — обновляет параметры переподключения
/// и перезапускает все сканеры для применения изменений.
/// </summary>
app.MapPut("/api/settings/reconnect/config", (ReconnectConfigRequest req) =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    SetSetting(db, "ReconnectDelayMs", Math.Clamp(req.DelayMs, 100, 60000).ToString());
    SetSetting(db, "ReconnectMaxRetries", Math.Clamp(req.MaxRetries, 1, 10000).ToString());
    SetSetting(db, "ReconnectContinuous", req.Continuous.ToString().ToLower());
    var config = ReadReconnectConfig(db);
    Log.Information("Настройки переподключения обновлены: задержка={Delay}мс, попытки={MaxRetries}, непрерывно={Continuous}",
        config.DelayMs, config.MaxRetries, config.Continuous);

    var scanners = ReadScanners();
    manager.RestartAll(scanners);

    return Results.Ok(new { config.DelayMs, config.MaxRetries, config.Continuous });
});

// ── Информация при запуске ──

using (var scope = app.Services.CreateScope())
{
    var scanners = ReadScanners();
    Log.Information("Запуск {Count} сканер(ов): {Names}",
        scanners.Count, string.Join(", ", scanners.Select(s => $"{s.Name} ({s.PortName})")));

    try
    {
        var ports = SerialPort.GetPortNames();
        Log.Information("Доступные COM-порты: {Ports}", ports.Length > 0 ? string.Join(", ", ports) : "НЕТ");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Не удалось получить список COM-портов");
    }

    Log.Information("База данных: {Path}", Path.GetFullPath("scanbridge.db"));
}

app.Run();

// ── Вспомогательные статические методы ──

/// <summary>
/// Возвращает текущий режим переподключения из БД.
/// </summary>
/// <param name="db">Контекст базы данных.</param>
/// <returns>"single" или "all".</returns>
static string GetReconnectMode(AppDbContext db)
{
    return db.Settings.FirstOrDefault(s => s.Key == "ReconnectMode")?.Value ?? "single";
}

/// <summary>
/// Устанавливает значение настройки в БД. Создаёт запись если не существует.
/// </summary>
/// <param name="db">Контекст базы данных.</param>
/// <param name="key">Ключ настройки.</param>
/// <param name="value">Значение настройки.</param>
static void SetSetting(AppDbContext db, string key, string value)
{
    var setting = db.Settings.FirstOrDefault(s => s.Key == key);
    if (setting == null)
    {
        db.Settings.Add(new AppSetting { Key = key, Value = value });
    }
    else
    {
        setting.Value = value;
    }
    db.SaveChanges();
}

/// <summary>
/// Импортирует конфигурацию из legacy-файла appsettings.json в БД.
/// Выполняется только если БД пуста (нет сканеров и пост-скан действий).
/// </summary>
/// <param name="db">Контекст базы данных.</param>
static void SeedFromLegacyConfig(AppDbContext db)
{
    if (db.Scanners.Any() || db.PostScanActionGroups.Any()) return;

    var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    if (!File.Exists(configPath)) return;

    try
    {
        var json = File.ReadAllText(configPath);
        var doc = System.Text.Json.JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("Scanners", out var scannersArr))
        {
            var scanners = JsonSerializer.Deserialize<List<SerialPortConfig>>(
                scannersArr.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            var order = 0;
            foreach (var s in scanners)
            {
                db.Scanners.Add(new ScannerConfig
                {
                    Name = s.Name, PortName = s.PortName, BaudRate = s.BaudRate,
                    DataBits = s.DataBits, Parity = s.Parity, StopBits = s.StopBits,
                    Handshake = s.Handshake, ReadTimeout = s.ReadTimeout,
                    WriteTimeout = s.WriteTimeout, SortOrder = order++,
                    ReconnectDelayMs = s.Reconnect.DelayMs,
                    ReconnectMaxRetries = s.Reconnect.MaxRetries,
                    ReconnectContinuous = s.Reconnect.Continuous
                });
            }
        }

        if (doc.RootElement.TryGetProperty("ReconnectMode", out var modeProp))
        {
            db.Settings.Add(new AppSetting { Key = "ReconnectMode", Value = modeProp.GetString() ?? "single" });
        }

        if (doc.RootElement.TryGetProperty("ReconnectDelayMs", out var delayProp))
        {
            db.Settings.Add(new AppSetting { Key = "ReconnectDelayMs", Value = delayProp.GetInt32().ToString() });
        }

        if (doc.RootElement.TryGetProperty("ReconnectMaxRetries", out var retriesProp))
        {
            db.Settings.Add(new AppSetting { Key = "ReconnectMaxRetries", Value = retriesProp.GetInt32().ToString() });
        }

        if (doc.RootElement.TryGetProperty("ReconnectContinuous", out var continuousProp))
        {
            db.Settings.Add(new AppSetting { Key = "ReconnectContinuous", Value = continuousProp.GetBoolean().ToString().ToLower() });
        }

        db.SaveChanges();
        Log.Information("Конфигурация импортирована из appsettings.json");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Не удалось импортировать конфигурацию из appsettings.json");
    }
}

/// <summary>
/// DTO-запрос для изменения режима переподключения.
/// </summary>
/// <param name="Mode">Режим: "single" (только изменённый сканер) или "all" (все сканеры).</param>
internal record ReconnectModeRequest(string Mode);

/// <summary>
/// DTO-запрос для изменения параметров переподключения.
/// </summary>
/// <param name="DelayMs">Задержка между попытками (100-60000 мс).</param>
/// <param name="MaxRetries">Максимальное количество попыток (1-10000).</param>
/// <param name="Continuous">Непрерывный режим (бесконечные попытки).</param>
internal record ReconnectConfigRequest(int DelayMs, int MaxRetries, bool Continuous);
