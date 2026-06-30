namespace ScanBridge.Models;

/// <summary>
/// Конфигурация группы пост-скан действий.
/// Группа — именованный шаблон действий, привязанный к сканерам.
/// Действия внутри группы выполняются последовательно.
/// </summary>
public class PostScanActionGroupConfig
{
    /// <summary>
    /// Идентификатор группы (0 = новая).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Имя группы.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Флаг активности группы.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Имена сканеров, к которым привязана группа.
    /// Пустой список = привязка ко всем сканерам.
    /// </summary>
    public List<string> ScannerNames { get; set; } = new();

    /// <summary>
    /// Действия в этой группе (выполняются последовательно).
    /// </summary>
    public List<PostScanActionConfig> Actions { get; set; } = new();
}
