using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// </summary>
public class InstancePoller : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InstancePoller> _logger;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<int, CachedInstanceData> _cache = new();
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
    public InstancePoller(IServiceScopeFactory scopeFactory, ILogger<InstancePoller> logger, IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(3);
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
    /// Основной цикл опроса: запрашивает данные у всех активных экземпляров
    /// и вызывает событие OnUpdate после каждого цикла.
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка опроса");
            }

            await Task.Delay(_pollIntervalMs, stoppingToken);
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
            var actionsTask = GetJsonElement($"{baseUrl}/api/postscan/actions", ct);
            var lastScanTask = GetJsonElement($"{baseUrl}/api/scanners/lastscan", ct);

            await Task.WhenAll(scannersTask, logsTask, actionsTask, lastScanTask);

            cached.Scanners = await scannersTask;
            cached.Logs = await logsTask;
            cached.Actions = await actionsTask;

            var lastScan = await lastScanTask;
            if (lastScan.TryGetProperty("time", out var timeProp) && timeProp.ValueKind == JsonValueKind.String)
                cached.LastScanTime = DateTime.Parse(timeProp.GetString()!);
            if (lastScan.TryGetProperty("scannerName", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                cached.LastScannerName = nameProp.GetString() ?? string.Empty;

            cached.Online = true;
            cached.LastPoll = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            cached.Online = false;
            cached.LastPoll = DateTime.UtcNow;
            _logger.LogWarning("Сервер {Name} ({Host}:{Port}) недоступен: {Error}",
                instance.Name, instance.Host, instance.Port, ex.Message);
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
}
