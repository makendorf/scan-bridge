namespace ScanBridge.Models;

/// <summary>
/// Результат обработки отсканированного штрихкода или QR-кода.
/// Содержит сырые данные, распарсенные значения, метаданные формата и валидности.
/// </summary>
public class ScanResult
{
    /// <summary>
    /// Исходные данные, полученные от сканера без обработки.
    /// </summary>
    public string RawData { get; set; } = string.Empty;

    /// <summary>
    /// Обработанные (очищенные от пробелов, нормализованные) данные штрихкода.
    /// </summary>
    public string ParsedData { get; set; } = string.Empty;

    /// <summary>
    /// Определённый формат штрихкода: EAN-8, EAN-13, UPC-A, Code128, UUID, QR и т.д.
    /// </summary>
    public string Format { get; set; } = "Unknown";

    /// <summary>
    /// Флаг валидности штрихкода. Определяется на основании формата и содержимого.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Имя сканера, который считал данные.
    /// </summary>
    public string ScannerName { get; set; } = string.Empty;

    /// <summary>
    /// UTC-метка времени сканирования.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Тип содержимого QR-кода: Text, Url, Json, VCard, Wifi.
    /// </summary>
    public string ContentType { get; set; } = "Unknown";

    /// <summary>
    /// Распарсированное содержимое QR-кода (форматированное JSON, информация о Wi-Fi и т.д.).
    /// </summary>
    public string? ParsedContent { get; set; }

    /// <summary>
    /// Дополнительные метаданные, например результат обогащения данных.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Идентификатор триггера (имя сканера, route, путь к файлу и т.д.).
    /// </summary>
    public string TriggerSource { get; set; } = string.Empty;

    /// <summary>
    /// Тип триггера: Scanner, Http, Schedule, FileWatcher.
    /// </summary>
    public string TriggerType { get; set; } = "Scanner";
}
