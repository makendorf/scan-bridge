using ScanBridge.Models;
using ScanBridge.Utils;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие логирования результата сканирования.
/// Записывает информацию о штрихкоде через ILogger.
/// </summary>
public class LogAction : IPostScanAction
{
    /// <summary>
    /// Тип действия.
    /// </summary>
    public string Type => "Log";

    private readonly ILogger<LogAction> _logger;

    /// <summary>
    /// Создаёт экземпляр действия логирования.
    /// </summary>
    /// <param name="logger">Логгер для записи информации.</param>
    public LogAction(ILogger<LogAction> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Логирует результат сканирования. Валидные штрихкоды логируются как Information,
    /// невалидные — как Warning.
    /// </summary>
    /// <param name="scan">Результат сканирования.</param>
    /// <param name="ct">Токен отмены.</param>
    public Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (!scan.IsValid)
        {
            _logger.LogWarning("Неверный штрихкод: {Raw}", ControlCharDisplay.ForDisplay(scan.RawData));
        }
        else
        {
            _logger.LogInformation("Штрихкод {Format}: {Data}", scan.Format, ControlCharDisplay.ForDisplay(scan.ParsedData));
        }

        return Task.CompletedTask;
    }
}
