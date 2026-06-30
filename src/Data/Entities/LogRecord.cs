namespace ScanBridge.Data.Entities;

/// <summary>
/// Сущность записи лога в базе данных SQLite.
/// Хранит информацию о событиях, происходящих в системе.
/// </summary>
public class LogRecord
{
    /// <summary>
    /// Уникальный идентификатор записи.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// UTC-метка времени события.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Уровень логирования: INF, WRN, ERR, DBG, FTL, VRB.
    /// Ограничение: до 10 символов.
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Текст сообщения лога. Ограничение: до 4000 символов.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Текст исключения (если было). Может быть null.
    /// </summary>
    public string? Exception { get; set; }
}
