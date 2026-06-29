using ScanBridge.Models;

namespace ScanBridge.Services;

/// <summary>
/// Сервис обработки результатов сканирования.
/// Координирует запись времени сканирования и запуск пост-скан действий.
/// </summary>
public class ScanProcessorService
{
    private readonly PostScanManager _postScanManager;
    private readonly ScanTracker _scanTracker;
    private readonly ScanHistoryService _historyService;
    private readonly ILogger<ScanProcessorService> _logger;

    /// <summary>
    /// Создаёт экземпляр сервиса обработки сканирований.
    /// </summary>
    /// <param name="postScanManager">Менеджер пост-скан действий.</param>
    /// <param name="scanTracker">Трекер времени последнего сканирования.</param>
    /// <param name="historyService">Сервис истории сканирований.</param>
    /// <param name="logger">Логгер.</param>
    public ScanProcessorService(PostScanManager postScanManager, ScanTracker scanTracker, ScanHistoryService historyService, ILogger<ScanProcessorService> logger)
    {
        _postScanManager = postScanManager;
        _scanTracker = scanTracker;
        _historyService = historyService;
        _logger = logger;
    }

    /// <summary>
    /// Обрабатывает результат сканирования: записывает время и запускает пост-скан действия.
    /// Невалидные штрихкоды не обрабатываются (только логируется предупреждение).
    /// </summary>
    /// <param name="scan">Результат сканирования.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task ProcessAsync(ScanResult scan, CancellationToken ct)
    {
        if (!scan.IsValid)
        {
            _logger.LogWarning("Неверный штрихкод: {Raw}", scan.RawData);
            return;
        }

        _scanTracker.RecordScan(scan.ScannerName);
        _historyService.RecordScan(scan);

        await _postScanManager.ExecuteAllAsync(scan, ct);
    }
}
