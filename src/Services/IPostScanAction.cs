using ScanBridge.Models;

namespace ScanBridge.Services;

/// <summary>
/// Интерфейс пост-скан действия.
/// Определяет контракт для обработки результата сканирования.
/// </summary>
public interface IPostScanAction
{
    /// <summary>
    /// Тип действия (Log, ClipboardPaste, Export, Replacement).
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Выполняет действие над результатом сканирования.
    /// </summary>
    /// <param name="scan">Результат сканирования.</param>
    /// <param name="ct">Токен отмены.</param>
    Task ExecuteAsync(ScanResult scan, CancellationToken ct);
}
