namespace ScanBridge.Data.Entities;

/// <summary>
/// Связь группы пост-скан действий с конкретным COM-сканером.
/// Одна группа может быть привязана к нескольким сканерам.
/// Пустой список связей означает привязку ко всем сканерам.
/// </summary>
public class PostScanActionGroupScanner
{
    /// <summary>
    /// Уникальный идентификатор записи.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор группы.
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// Имя COM-сканера.
    /// Ограничение: до 100 символов.
    /// </summary>
    public string ScannerName { get; set; } = string.Empty;

    /// <summary>
    /// Группа, к которой привязан сканер.
    /// </summary>
    public PostScanActionGroup Group { get; set; } = null!;
}
