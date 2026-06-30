using ScanBridge.Models;

namespace ScanBridge.Utils;

/// <summary>
/// Утилита форматирования шаблонов с плейсхолдерами данных сканирования.
/// Используется действиями уведомлений и экспорта.
/// </summary>
public static class ScanTemplateHelper
{
    /// <summary>
    /// Заменяет плейсхолдеры в шаблоне на значения из результата сканирования.
    /// Поддерживаемые плейсхолдеры: {data}, {raw}, {format}, {scanner}, {timestamp}.
    /// </summary>
    /// <param name="template">Шаблон строки с плейсхолдерами.</param>
    /// <param name="scan">Результат сканирования.</param>
    /// <returns>Форматированная строка.</returns>
    public static string Format(string template, ScanResult scan)
    {
        return template
            .Replace("{data}", scan.ParsedData)
            .Replace("{raw}", scan.RawData)
            .Replace("{format}", scan.Format)
            .Replace("{scanner}", scan.ScannerName)
            .Replace("{timestamp}", scan.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}