namespace ScanBridge.Data.Entities;

/// <summary>
/// Сущность события переподключения сканера.
/// Записывается при каждой ошибке COM-порта.
/// </summary>
public class ReconnectEvent
{
    /// <summary>
    /// Уникальный идентификатор записи.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// UTC-метка времени ошибки.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Имя сканера, произошедшего переподключение. Ограничение: до 100 символов.
    /// </summary>
    public string ScannerName { get; set; } = string.Empty;

    /// <summary>
    /// Текст ошибки. Ограничение: до 500 символов.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Номер попытки переподключения.
    /// </summary>
    public int AttemptNumber { get; set; }
}
