using ScanBridge.Models;

namespace ScanBridge.Parsers;

/// <summary>
/// Интерфейс парсера штрихкодов.
/// Определяет контракт для обработки сырых данных от сканера
/// и преобразования их в структурированный результат.
/// </summary>
public interface IBarcodeParser
{
    /// <summary>
    /// Парсит сырые данные штрихкода и возвращает результат с метаданными.
    /// </summary>
    /// <param name="rawData">Исходные данные, полученные от сканера.</param>
    /// <param name="controlCharMode">0 — удалять, 1 — оставлять (валидация пройдёт), 2 — оставлять (обычная валидация).</param>
    /// <returns>Результат парсинга с информацией о формате, валидности и типе содержимого.</returns>
    ScanResult Parse(string rawData, int controlCharMode = 0);
}
