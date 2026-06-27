namespace ScanBridge.Data.Entities;

/// <summary>
/// Группа пост-скан действий — именованный шаблон, привязанный к сканерам.
/// Действия внутри группы выполняются последовательно.
/// Несколько групп, привязанных к одному сканеру, выполняются параллельно.
/// </summary>
public class PostScanActionGroup
{
    /// <summary>
    /// Уникальный идентификатор группы.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Имя группы (например, «Уведомления Telegram»).
    /// Ограничение: до 100 символов.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Флаг активности группы. Если false, группа не выполняется.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Порядок сортировки группы в интерфейсе.
    /// </summary>
    public int SortOrder { get; set; }
}
