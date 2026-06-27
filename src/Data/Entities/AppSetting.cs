namespace ScanBridge.Data.Entities;

/// <summary>
/// Сущность настройки приложения в базе данных SQLite.
/// Хранит пары ключ-значение для различных конфигураций.
/// </summary>
public class AppSetting
{
    /// <summary>
    /// Уникальный идентификатор записи.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Уникальный ключ настройки. Ограничение: до 100 символов.
    /// Примеры: "ReconnectMode", "ReconnectDelayMs", "ReconnectMaxRetries".
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Значение настройки в строковом представлении. Ограничение: до 2000 символов.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
