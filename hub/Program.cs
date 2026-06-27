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
builder.Services.AddSignalR();
builder.Services.AddSingleton<InstancePoller>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<InstancePoller>());
builder.Services.AddSingleton<AlertService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AlertService>());

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
var alertService = app.Services.GetRequiredService<AlertService>();

app.UseCors();

// ── SignalR Hub ──

app.MapHub<StatsHub>("/hubs/stats");

// ── API: Удалённое управление сканерами ──

/// <summary>
/// POST /api/instances/{id}/scanners/{name}/restart — перезапускает сканер на удалённом экземпляре.
/// </summary>
app.MapPost("/api/instances/{id:int}/scanners/{name}/restart", async (int id, string name, HubDbContext db, IHttpClientFactory httpClientFactory) =>
{
    var instance = await db.Instances.FindAsync(id);
    if (instance == null) return Results.NotFound(new { error = "Сервер не найден" });

    try
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        var url = $"http://{instance.Host}:{instance.Port}/api/scanners/{Uri.EscapeDataString(name)}/restart";
        var response = await client.PostAsync(url, null);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return Results.StatusCode((int)response.StatusCode);
        return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<object>(content));
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Ошибка перезапуска сканера {Name} на {Instance}", name, instance.Name);
        return Results.BadRequest(new { error = $"Сервер '{instance.Name}' недоступен: {ex.Message}" });
    }
});

/// <summary>
/// POST /api/instances/{id}/scanners — добавляет сканер на удалённом экземпляре.
/// </summary>
app.MapPost("/api/instances/{id:int}/scanners", async (int id, HubDbContext db, IHttpClientFactory httpClientFactory, object scannerConfig) =>
{
    var instance = await db.Instances.FindAsync(id);
    if (instance == null) return Results.NotFound(new { error = "Сервер не найден" });

    try
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        var url = $"http://{instance.Host}:{instance.Port}/api/scanners";
        var json = System.Text.Json.JsonSerializer.Serialize(scannerConfig);
        var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, body);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return Results.StatusCode((int)response.StatusCode);
        return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<object>(content));
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Ошибка добавления сканера на {Instance}", instance.Name);
        return Results.BadRequest(new { error = $"Сервер '{instance.Name}' недоступен: {ex.Message}" });
    }
});

/// <summary>
/// DELETE /api/instances/{id}/scanners/{index} — удаляет сканер на удалённом экземпляре.
/// </summary>
app.MapDelete("/api/instances/{id:int}/scanners/{index:int}", async (int id, int index, HubDbContext db, IHttpClientFactory httpClientFactory) =>
{
    var instance = await db.Instances.FindAsync(id);
    if (instance == null) return Results.NotFound(new { error = "Сервер не найден" });

    try
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        var url = $"http://{instance.Host}:{instance.Port}/api/scanners/{index}";
        var response = await client.DeleteAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return Results.StatusCode((int)response.StatusCode);
        return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<object>(content));
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Ошибка удаления сканера {Index} на {Instance}", index, instance.Name);
        return Results.BadRequest(new { error = $"Сервер '{instance.Name}' недоступен: {ex.Message}" });
    }
});

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

// ── API: Алерты ──

app.MapGet("/api/alerts", async (HubDbContext db) =>
{
    var alerts = await db.Alerts.OrderBy(a => a.Id).ToListAsync();
    return Results.Ok(alerts);
});

app.MapPost("/api/alerts", async (HubDbContext db, AlertRuleRequest req) =>
{
    var rule = new AlertRule
    {
        Name = req.Name,
        Type = req.Type,
        Enabled = req.Enabled,
        SettingsJson = System.Text.Json.JsonSerializer.Serialize(req.Settings ?? new())
    };
    db.Alerts.Add(rule);
    await db.SaveChangesAsync();
    Log.Information("Alert rule created: {Name} ({Type})", rule.Name, rule.Type);
    return Results.Ok(rule);
});

app.MapPut("/api/alerts/{id:int}", async (int id, HubDbContext db, AlertRuleRequest req) =>
{
    var rule = await db.Alerts.FindAsync(id);
    if (rule == null) return Results.NotFound();
    rule.Name = req.Name;
    rule.Type = req.Type;
    rule.Enabled = req.Enabled;
    rule.SettingsJson = System.Text.Json.JsonSerializer.Serialize(req.Settings ?? new());
    await db.SaveChangesAsync();
    Log.Information("Alert rule updated: {Name} ({Type})", rule.Name, rule.Type);
    return Results.Ok(rule);
});

app.MapDelete("/api/alerts/{id:int}", async (int id, HubDbContext db) =>
{
    var rule = await db.Alerts.FindAsync(id);
    if (rule == null) return Results.NotFound();
    db.Alerts.Remove(rule);
    await db.SaveChangesAsync();
    Log.Information("Alert rule deleted: {Name}", rule.Name);
    return Results.Ok();
});

// ── API: История сканирований ──

app.MapGet("/api/history", async (HubDbContext db, int? instanceId, string? scannerName, DateTime? from, DateTime? to, int limit = 100) =>
{
    var query = db.ScanEvents.AsQueryable();
    if (instanceId.HasValue) query = query.Where(e => e.InstanceId == instanceId.Value);
    if (!string.IsNullOrWhiteSpace(scannerName)) query = query.Where(e => e.ScannerName == scannerName);
    if (from.HasValue) query = query.Where(e => e.Timestamp >= from.Value);
    if (to.HasValue) query = query.Where(e => e.Timestamp <= to.Value);
    var events = await query.OrderByDescending(e => e.Timestamp).Take(Math.Clamp(limit, 1, 1000)).ToListAsync();
    return Results.Ok(events);
});

app.MapGet("/api/history/stats", async (HubDbContext db) =>
{
    var totalScans = await db.ScanEvents.CountAsync();
    var todayScans = await db.ScanEvents.CountAsync(e => e.Timestamp >= DateTime.UtcNow.Date);
    var topScanners = await db.ScanEvents
        .GroupBy(e => e.ScannerName)
        .Select(g => new { Scanner = g.Key, Count = g.Count() })
        .OrderByDescending(x => x.Count)
        .Take(10)
        .ToListAsync();
    var hourlyScans = await db.ScanEvents
        .Where(e => e.Timestamp >= DateTime.UtcNow.AddHours(24))
        .GroupBy(e => e.Timestamp.Hour)
        .Select(g => new { Hour = g.Key, Count = g.Count() })
        .OrderBy(x => x.Hour)
        .ToListAsync();
    return Results.Ok(new { totalScans, todayScans, topScanners, hourlyScans });
});

app.MapDelete("/api/history", async (HubDbContext db) =>
{
    await db.ScanEvents.ExecuteDeleteAsync();
    await db.SaveChangesAsync();
    Log.Information("Scan history cleared");
    return Results.Ok(new { message = "История очищена" });
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
