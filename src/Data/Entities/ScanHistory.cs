namespace ScanBridge.Data.Entities;

/// <summary>
/// Сущность записи сканирования в базе данных SQLite.
/// Хранит результат каждого считывания штрихкода/QR-кода.
/// </summary>
public class ScanHistory
{
    /// <summary>
    /// Уникальный идентификатор записи.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// UTC-метка времени сканирования.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Имя сканера, выполнившего считывание. Ограничение: до 100 символов.
    /// </summary>
    public string ScannerName { get; set; } = string.Empty;

    /// <summary>
    /// Формат штрихкода: EAN-13, QR, Code128 и т.д. Ограничение: до 20 символов.
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Исходные данные от сканера. Ограничение: до 500 символов.
    /// </summary>
    public string RawData { get; set; } = string.Empty;

    /// <summary>
    /// Очищенные/нормализованные данные. Ограничение: до 500 символов.
    /// </summary>
    public string ParsedData { get; set; } = string.Empty;

    /// <summary>
    /// Результат валидации штрихкода.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Тип содержимого QR-кода: Text, Url, Json, VCard, Wifi. Ограничение: до 20 символов.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
}
