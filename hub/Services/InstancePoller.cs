using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ScanBridgeHub.Data;
using ScanBridgeHub.Models;

namespace ScanBridgeHub.Services;

/// <summary>
/// Кэшированные данные удалённого экземпляра ScanBridge.
/// Хранит результат последнего опроса: статус, сканеры, логи, действия.
/// </summary>
public class CachedInstanceData
{
    /// <summary>
    /// Статус доступности экземпляра (true если последний опрос успешен).
    /// </summary>
    public bool Online { get; set; }

    /// <summary>
    /// UTC-время последнего успешного опроса.
    /// </summary>
    public DateTime LastPoll { get; set; }

    /// <summary>
    /// Список сканеров с их статусами (JSON-элементы от API).
    /// </summary>
    public List<JsonElement>? Scanners { get; set; }

    /// <summary>
    /// Список последних логов (JSON-элементы от API).
    /// </summary>
    public List<JsonElement>? Logs { get; set; }

    /// <summary>
    /// Конфигурация пост-скан действий (JSON-элемент от API).
    /// </summary>
    public JsonElement? Actions { get; set; }

    /// <summary>
    /// UTC-время последнего сканирования на экземпляре.
    /// </summary>
    public DateTime LastScanTime { get; set; }

    /// <summary>
    /// Имя сканера, выполнившего последнее сканирование.
    /// </summary>
    public string LastScannerName { get; set; } = string.Empty;
}

/// <summary>
/// Фоновый сервис опроса удалённых экземпляров ScanBridge.
/// Периодически запрашивает данные у каждого экземпляра и кэширует результаты.
/// После каждого цикла отправляет обновление через SignalR.
/// </summary>
public class InstancePoller : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InstancePoller> _logger;
    private readonly HttpClient _httpClient;
    private readonly IHubContext<StatsHub> _hubContext;
    private readonly AlertService _alertService;
    private readonly ConcurrentDictionary<int, CachedInstanceData> _cache = new();
    private readonly ConcurrentDictionary<int, DateTime> _lastKnownScanTimes = new();
    private int _pollIntervalMs = 5000;

    /// <summary>
    /// Событие, вызываемое после каждого цикла опроса всех экземпляров.
    /// </summary>
    public event Action? OnUpdate;

    /// <summary>
    /// Текущий интервал опроса в миллисекундах.
    /// </summary>
    public int PollIntervalMs => _pollIntervalMs;

    /// <summary>
    /// Создаёт экземпляр сервиса опроса.
    /// </summary>
    /// <param name="scopeFactory">Фабрика scope'ов для доступа к БД.</param>
    /// <param name="logger">Логгер.</param>
    /// <param name="httpClientFactory">Фабрика HTTP-клиентов.</param>
    /// <param name="hubContext">Контекст SignalR Hub для отправки обновлений.</param>
    /// <param name="alertService">Сервис алертов.</param>
    public InstancePoller(
        IServiceScopeFactory scopeFactory,
        ILogger<InstancePoller> logger,
        IHttpClientFactory httpClientFactory,
        IHubContext<StatsHub> hubContext,
        AlertService alertService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(3);
        _hubContext = hubContext;
        _alertService = alertService;
    }

    /// <summary>
    /// Устанавливает интервал опроса (ограничен диапазоном 1000-60000 мс).
    /// </summary>
    /// <param name="ms">Интервал в миллисекундах.</param>
    public void SetPollInterval(int ms)
    {
        _pollIntervalMs = Math.Clamp(ms, 1000, 60000);
    }

    /// <summary>
    /// Возвращает кэшированные данные экземпляра по ID.
    /// </summary>
    /// <param name="instanceId">ID экземпляра.</param>
    /// <returns>Кэшированные данные или null, если экземпляр не опрошен.</returns>
    public CachedInstanceData? GetCached(int instanceId)
        => _cache.TryGetValue(instanceId, out var data) ? data : null;

    /// <summary>
    /// Возвращает кэшированные данные всех экземпляров.
    /// </summary>
    /// <returns>Словарь ID → данные.</returns>
    public Dictionary<int, CachedInstanceData> GetAllCached()
        => new(_cache);

    /// <summary>
    /// Основной цикл опроса: запрашивает данные у всех активных экземпляров,
    /// вызывает событие OnUpdate и отправляет обновление через SignalR.
    /// </summary>
    /// <param name="stoppingToken">Токен отмены.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InstancePoller запущен, интервал: {Interval}мс", _pollIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllInstances(stoppingToken);
                OnUpdate?.Invoke();
                await BroadcastStatsUpdate(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка опроса");
            }

            await Task.Delay(_pollIntervalMs, stoppingToken);
        }
    }

    /// <summary>
    /// Отправляет агрегированную статистику всем подключённым клиентам через SignalR.
    /// </summary>
    /// <param name="ct">Токен отмены.</param>
    private async Task BroadcastStatsUpdate(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var instances = await db.Instances.OrderBy(i => i.SortOrder).ToListAsync(ct);
            var allCached = GetAllCached();

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

            await _hubContext.Clients.All.SendAsync("StatsUpdate", new
            {
                totalServers = instances.Count,
                onlineServers = onlineCount,
                totalScanners,
                totalActions,
                pollInterval = _pollIntervalMs,
                instances = instanceDetails
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка отправки StatsUpdate через SignalR");
        }
    }

    /// <summary>
    /// Опрашивает все активные экземпляры параллельно.
    /// </summary>
    /// <param name="ct">Токен отмены.</param>
    private async Task PollAllInstances(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var instances = await db.Instances.Where(i => i.Enabled).OrderBy(i => i.SortOrder).ToListAsync(ct);

        var tasks = instances.Select(instance => PollInstance(instance, ct));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Опрашивает один экземпляр: запрашивает сканеры, логи, действия и время последнего сканирования.
    /// Результаты кэшируются в ConcurrentDictionary.
    /// </summary>
    /// <param name="instance">Экземпляр для опроса.</param>
    /// <param name="ct">Токен отмены.</param>
    private async Task PollInstance(RemoteInstance instance, CancellationToken ct)
    {
        var baseUrl = $"http://{instance.Host}:{instance.Port}";
        var cached = _cache.GetOrAdd(instance.Id, _ => new CachedInstanceData());

        try
        {
            var scannersTask = GetJsonList($"{baseUrl}/api/scanners/status", ct);
            var logsTask = GetJsonList($"{baseUrl}/api/logs?limit=500", ct);
            var actionsTask = GetJsonElementOrNull($"{baseUrl}/api/postscan/groups", ct);
            var lastScanTask = GetJsonElementOrNull($"{baseUrl}/api/scanners/lastscan", ct);

            await Task.WhenAll(scannersTask, logsTask, actionsTask, lastScanTask);

            cached.Scanners = await scannersTask;
            cached.Logs = await logsTask;
            cached.Actions = await actionsTask;

            var lastScan = await lastScanTask;
            if (lastScan is { } ls)
            {
                if (ls.TryGetProperty("time", out var timeProp) && timeProp.ValueKind == JsonValueKind.String)
                    cached.LastScanTime = DateTime.Parse(timeProp.GetString()!);
                if (ls.TryGetProperty("scannerName", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                    cached.LastScannerName = nameProp.GetString() ?? string.Empty;
            }

            cached.Online = true;
            cached.LastPoll = DateTime.UtcNow;

            _alertService.CheckForAlerts(instance.Id, instance.Name, true, null);

            if (_lastKnownScanTimes.TryGetValue(instance.Id, out var prevScanTime) && cached.LastScanTime > prevScanTime && cached.LastScanTime != DateTime.MinValue)
            {
                await SaveScanEvent(instance, cached.LastScannerName, cached.LastScanTime, ct);
            }
            _lastKnownScanTimes[instance.Id] = cached.LastScanTime;
        }
        catch (Exception ex)
        {
            cached.Online = false;
            cached.LastPoll = DateTime.UtcNow;
            _logger.LogWarning("Сервер {Name} ({Host}:{Port}) недоступен: {Error}",
                instance.Name, instance.Host, instance.Port, ex.Message);

            _alertService.CheckForAlerts(instance.Id, instance.Name, false, null);
        }
    }

    private async Task SaveScanEvent(RemoteInstance instance, string scannerName, DateTime scanTime, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var scanEvent = new ScanEvent
            {
                InstanceId = instance.Id,
                InstanceName = instance.Name,
                ScannerName = scannerName,
                Timestamp = scanTime,
                ReceivedAt = DateTime.UtcNow
            };
            db.ScanEvents.Add(scanEvent);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save scan event for {Instance}", instance.Name);
        }
    }

    /// <summary>
    /// Запрашивает JSON-массив с удалённого сервера.
    /// </summary>
    /// <param name="url">URL-адрес API.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список JSON-элементов.</returns>
    private async Task<List<JsonElement>> GetJsonList(string url, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<JsonElement>>(content) ?? new();
    }

    /// <summary>
    /// Запрашивает один JSON-объект с удалённого сервера.
    /// </summary>
    /// <param name="url">URL-адрес API.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>JSON-элемент.</returns>
    private async Task<JsonElement> GetJsonElement(string url, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    private async Task<JsonElement?> GetJsonElementOrNull(string url, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;
            var content = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch
        {
            return null;
        }
    }
}
