using Microsoft.EntityFrameworkCore;
using Serilog;
using ScanBridgeHub.Data;
using ScanBridgeHub.Models;
using ScanBridgeHub.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5001");

// ── Регистрация сервисов ──

builder.Services.AddDbContext<HubDbContext>(options =>
    options.UseSqlite("Data Source=hub.db"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<InstancePoller>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<InstancePoller>());

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/hub-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

// ── Инициализация БД ──

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
    db.Database.EnsureCreated();
}

var poller = app.Services.GetRequiredService<InstancePoller>();

app.UseCors();

// ── API: Управление экземплярами ──

/// <summary>
/// GET /api/instances — возвращает список всех удалённых экземпляров ScanBridge.
/// </summary>
app.MapGet("/api/instances", async (HubDbContext db) =>
{
    var list = await db.Instances.OrderBy(i => i.SortOrder).ToListAsync();
    return Results.Ok(list);
});

/// <summary>
/// POST /api/instances — добавляет новый удалённый экземпляр.
/// </summary>
app.MapPost("/api/instances", async (HubDbContext db, InstanceRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Host))
        return Results.BadRequest(new { error = "Name и Host обязательны" });

    var maxOrder = db.Instances.Any() ? db.Instances.Max(i => i.SortOrder) + 1 : 0;
    var instance = new RemoteInstance
    {
        Name = req.Name,
        Host = req.Host,
        Port = req.Port,
        Enabled = req.Enabled,
        SortOrder = maxOrder
    };
    db.Instances.Add(instance);
    await db.SaveChangesAsync();
    Log.Information("Добавлен сервер: {Name} ({Host}:{Port})", instance.Name, instance.Host, instance.Port);
    return Results.Ok(await db.Instances.OrderBy(i => i.SortOrder).ToListAsync());
});

/// <summary>
/// PUT /api/instances/{id} — обновляет конфигурацию удалённого экземпляра.
/// </summary>
app.MapPut("/api/instances/{id:int}", async (int id, HubDbContext db, InstanceRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Host))
        return Results.BadRequest(new { error = "Name и Host обязательны" });

    var instance = await db.Instances.FindAsync(id);
    if (instance == null) return Results.NotFound();
    instance.Name = req.Name;
    instance.Host = req.Host;
    instance.Port = req.Port;
    instance.Enabled = req.Enabled;
    await db.SaveChangesAsync();
    Log.Information("Сервер обновлён: {Name} ({Host}:{Port})", instance.Name, instance.Host, instance.Port);
    return Results.Ok(await db.Instances.OrderBy(i => i.SortOrder).ToListAsync());
});

/// <summary>
/// DELETE /api/instances/{id} — удаляет удалённый экземпляр.
/// </summary>
app.MapDelete("/api/instances/{id:int}", async (int id, HubDbContext db) =>
{
    var instance = await db.Instances.FindAsync(id);
    if (instance == null) return Results.NotFound();
    db.Instances.Remove(instance);
    await db.SaveChangesAsync();
    Log.Information("Сервер удалён: {Name}", instance.Name);
    return Results.Ok(await db.Instances.OrderBy(i => i.SortOrder).ToListAsync());
});

// ── API: Статус экземпляра ──

/// <summary>
/// GET /api/instances/{id}/status — возвращает статус экземпляра: онлайн,
/// данные сканеров, логи и пост-скан действия из кэша опроса.
/// </summary>
app.MapGet("/api/instances/{id:int}/status", async (int id, HubDbContext db) =>
{
    var instance = await db.Instances.FindAsync(id);
    if (instance == null) return Results.NotFound();
    var cached = poller.GetCached(id);
    return Results.Ok(new
    {
        instance,
        online = cached?.Online ?? false,
        lastPoll = cached?.LastPoll,
        scanners = cached?.Scanners,
        logs = cached?.Logs,
        actions = cached?.Actions
    });
});

// ── API: Статистика Hub ──

/// <summary>
/// GET /api/hub/stats — возвращает агрегированную статистику Hub:
/// количество серверов, онлайн-серверов, сканеров, действий и интервал опроса.
/// </summary>
app.MapGet("/api/hub/stats", async (HubDbContext db) =>
{
    var instances = await db.Instances.OrderBy(i => i.SortOrder).ToListAsync();
    var allCached = poller.GetAllCached();

    var totalScanners = 0;
    var onlineCount = 0;
    var totalActions = 0;

    var instanceDetails = new List<object>();
    foreach (var inst in instances)
    {
        var cached = allCached.TryGetValue(inst.Id, out var c) ? c : null;
        var online = cached?.Online ?? false;
        if (online) onlineCount++;

        var scannerCount = cached?.Scanners?.Count ?? 0;
        totalScanners += scannerCount;

        var actionCount = 0;
        if (cached?.Actions is { } actionsEl && actionsEl.TryGetProperty("configs", out var configs))
            actionCount = configs.GetArrayLength();
        totalActions += actionCount;

        instanceDetails.Add(new
        {
            inst.Id,
            inst.Name,
            inst.Host,
            inst.Port,
            inst.Enabled,
            online,
            lastPoll = cached?.LastPoll,
            scannerCount,
            actionCount,
            lastScanTime = cached?.LastScanTime,
            lastScannerName = cached?.LastScannerName ?? string.Empty
        });
    }

    return Results.Ok(new
    {
        totalServers = instances.Count,
        onlineServers = onlineCount,
        totalScanners,
        totalActions,
        pollInterval = poller.PollIntervalMs,
        instances = instanceDetails
    });
});

// ── API: Настройки опроса ──

/// <summary>
/// GET /api/settings/poll-interval — возвращает текущий интервал опроса (мс).
/// </summary>
app.MapGet("/api/settings/poll-interval", () =>
    Results.Ok(new { intervalMs = poller.PollIntervalMs }));

/// <summary>
/// PUT /api/settings/poll-interval — изменяет интервал опроса (1000-60000 мс).
/// </summary>
app.MapPut("/api/settings/poll-interval", (PollIntervalRequest req) =>
{
    if (req.IntervalMs < 1000 || req.IntervalMs > 60000)
        return Results.BadRequest(new { error = "IntervalMs должен быть от 1000 до 60000" });

    poller.SetPollInterval(req.IntervalMs);
    Log.Information("Интервал опроса изменён на {Interval}мс", poller.PollIntervalMs);
    return Results.Ok(new { intervalMs = poller.PollIntervalMs });
});

// ── Статические файлы ──

app.UseDefaultFiles();
app.UseStaticFiles();

Log.Information("ScanBridge Hub запущен на порту 5001");

app.Run();

/// <summary>
/// DTO-запрос для изменения интервала опроса.
/// </summary>
/// <param name="IntervalMs">Интервал в миллисекундах (1000-60000).</param>
internal record PollIntervalRequest(int IntervalMs);
