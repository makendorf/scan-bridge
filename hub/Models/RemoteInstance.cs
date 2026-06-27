namespace ScanBridgeHub.Models;

/// <summary>
/// Модель удалённого экземпляра ScanBridge для хранения в БД Hub.
/// </summary>
public class RemoteInstance
{
    /// <summary>
    /// Уникальный идентификатор экземпляра.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Отображаемое имя экземпляра (например, "Касса 1").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Имя хоста или IP-адрес экземпляра (например, "192.168.1.100").
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Порт HTTP-сервера экземпляра (по умолчанию 5000).
    /// </summary>
    public int Port { get; set; } = 5000;

    /// <summary>
    /// Флаг активности: true — экземпляр включён и опрашивается Hub.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Порядок сортировки в интерфейсе Hub.
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// DTO-запрос для создания/обновления удалённого экземпляра.
/// </summary>
/// <param name="Name">Имя экземпляра.</param>
/// <param name="Host">Имя хоста или IP.</param>
/// <param name="Port">Порт.</param>
/// <param name="Enabled">Флаг активности.</param>
public record InstanceRequest(string Name, string Host, int Port, bool Enabled);
