using ScanBridge.Models;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие паузы между пост-скан действиями.
/// Приостанавливает выполнение на указанное время.
/// </summary>
public class PauseAction : IPostScanAction
{
    /// <summary>
    /// Тип действия.
    /// </summary>
    public string Type => "Pause";

    private readonly ILogger<PauseAction> _logger;
    private readonly int _delayMs;

    /// <summary>
    /// Создаёт экземпляр действия паузы.
    /// </summary>
    /// <param name="logger">Логгер.</param>
    /// <param name="settings">Параметры: DelayMs (задержка в миллисекундах, по умолчанию 1000).</param>
    public PauseAction(ILogger<PauseAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;
        _delayMs = settings.TryGetValue("DelayMs", out var delayStr)
            && int.TryParse(delayStr, out var delay) ? Math.Clamp(delay, 1, 60000) : 1000;
    }

    /// <summary>
    /// Выполняет паузу на указанное время.
    /// </summary>
    /// <param name="scan">Результат сканирования.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        _logger.LogInformation("Пауза: {Delay} мс", _delayMs);
        await Task.Delay(_delayMs, ct);
    }
}
