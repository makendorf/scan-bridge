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

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite("Data Source=scanbridge.db"));

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddSingleton<LogCollector>();
builder.Services.AddSingleton<IBarcodeParser, SimpleBarcodeParser>();
builder.Services.AddSingleton<PostScanManager>();
builder.Services.AddSingleton<ScanProcessorService>();
builder.Services.AddSingleton<ScannerManager>();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "ScanBridge";
    });
}

var app = builder.Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Sink(new CollectorSink(() => app.Services.GetRequiredService<LogCollector>()))
    .CreateLogger();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SeedFromLegacyConfig(db);
}

var postScanManager = app.Services.GetRequiredService<PostScanManager>();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var configs = db.PostScanActions
        .OrderBy(a => a.SortOrder)
        .Select(a => new PostScanActionConfig
        {
            Type = a.Type,
            Enabled = a.Enabled,
            ScannerName = a.ScannerName,
            Settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(a.SettingsJson) ?? new()
        })
        .ToList();
    postScanManager.Configure(configs);
}

List<SerialPortConfig> ReadScanners()
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
        WriteTimeout = s.WriteTimeout
    }).ToList();
}

var manager = app.Services.GetRequiredService<ScannerManager>();
manager.StartAll(ReadScanners());

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/scanners", () => Results.Ok(ReadScanners()));

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

app.MapPost("/api/scanners/{name}/restart", (string name) =>
{
    var scanners = ReadScanners();
    var config = scanners.FirstOrDefault(s => s.Name == name);
    if (config == null) return Results.NotFound(new { error = $"Сканер '{name}' не найден" });
    manager.RestartScanner(config);
    return Results.Ok(new { message = $"Сканер '{name}' перезапущен" });
});

app.MapPost("/api/scanners", (SerialPortConfig scanner) =>
{
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
        SortOrder = maxOrder
    });
    db.SaveChanges();
    var conflict = manager.StartScanner(scanner);
    return Results.Ok(new { scanners = ReadScanners(), conflict });
});

app.MapPut("/api/scanners/{index:int}", (int index, SerialPortConfig scanner) =>
{
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

app.MapDelete("/api/logs", () =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Logs.ExecuteDelete();
    db.SaveChanges();
    return Results.Ok(new { message = "Логи очищены" });
});

app.MapGet("/api/ports", () =>
{
    try
    {
        var ports = SerialPort.GetPortNames();
        return Results.Ok(ports);
    }
    catch
    {
        return Results.Ok(Array.Empty<string>());
    }
});

app.MapGet("/api/postscan/actions", () =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var configs = db.PostScanActions.OrderBy(a => a.SortOrder).Select(a => new
    {
        a.Id,
        a.Type,
        a.Enabled,
        a.ScannerName,
        Settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(a.SettingsJson) ?? new Dictionary<string, string>()
    }).ToList();
    var enabled = postScanManager.GetEnabledActions();
    return Results.Ok(new { configs, enabled });
});

app.MapPut("/api/postscan/actions", (List<PostScanActionConfig> configs) =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.PostScanActions.ExecuteDelete();
    for (var i = 0; i < configs.Count; i++)
    {
        var c = configs[i];
        db.PostScanActions.Add(new PostScanAction
        {
            Type = c.Type,
            Enabled = c.Enabled,
            ScannerName = c.ScannerName ?? string.Empty,
            SettingsJson = System.Text.Json.JsonSerializer.Serialize(c.Settings ?? new()),
            SortOrder = i
        });
    }
    db.SaveChanges();

    postScanManager.Configure(configs);
    Log.Information("Пост-скан действия обновлены: {Count} активных", configs.Count(c => c.Enabled));

    var result = db.PostScanActions.OrderBy(a => a.SortOrder).Select(a => new
    {
        a.Id,
        a.Type,
        a.Enabled,
        a.ScannerName,
        Settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(a.SettingsJson) ?? new Dictionary<string, string>()
    }).ToList();
    return Results.Ok(new { configs = result, enabled = postScanManager.GetEnabledActions() });
});

app.MapGet("/api/settings/reconnect", () =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    return Results.Ok(new { mode = GetReconnectMode(db) });
});

app.MapPut("/api/settings/reconnect", (ReconnectModeRequest req) =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    SetSetting(db, "ReconnectMode", req.Mode);
    Log.Information("Режим переподключения изменён на: {Mode}", req.Mode);
    return Results.Ok(new { mode = req.Mode });
});

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
    catch { }

    Log.Information("База данных: {Path}", Path.GetFullPath("scanbridge.db"));
}

app.Run();

static string GetReconnectMode(AppDbContext db)
{
    return db.Settings.FirstOrDefault(s => s.Key == "ReconnectMode")?.Value ?? "single";
}

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

static void SeedFromLegacyConfig(AppDbContext db)
{
    if (db.Scanners.Any() || db.PostScanActions.Any()) return;

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
                    WriteTimeout = s.WriteTimeout, SortOrder = order++
                });
            }
        }

        if (doc.RootElement.TryGetProperty("PostScanActions", out var actionsArr))
        {
            var actions = JsonSerializer.Deserialize<List<PostScanActionConfig>>(
                actionsArr.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            var order = 0;
            foreach (var a in actions)
            {
                db.PostScanActions.Add(new PostScanAction
                {
                    Type = a.Type, Enabled = a.Enabled,
                    ScannerName = a.ScannerName ?? string.Empty,
                    SettingsJson = System.Text.Json.JsonSerializer.Serialize(a.Settings ?? new()),
                    SortOrder = order++
                });
            }
        }

        if (doc.RootElement.TryGetProperty("ReconnectMode", out var modeProp))
        {
            db.Settings.Add(new AppSetting { Key = "ReconnectMode", Value = modeProp.GetString() ?? "single" });
        }

        db.SaveChanges();
        Log.Information("Конфигурация импортирована из appsettings.json");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Не удалось импортировать конфигурацию из appsettings.json");
    }
}

internal record ReconnectModeRequest(string Mode);
