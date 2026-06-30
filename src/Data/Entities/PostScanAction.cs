namespace ScanBridge.Data.Entities;

/// <summary>
/// Сущность пост-скан действия в базе данных SQLite.
/// Хранит конфигурацию обработки результатов сканирования.
/// Привязана к группе через GroupId.
/// </summary>
public class PostScanAction
{
    /// <summary>
    /// Уникальный идентификатор записи.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор группы, к которой относится действие.
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// Тип действия: Log, ClipboardPaste, Export, Replacement и т.д.
    /// Ограничение: до 50 символов.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Флаг активности действия.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Параметры действия в формате JSON.
    /// Ограничение: до 4000 символов.
    /// </summary>
    public string SettingsJson { get; set; } = "{}";

    /// <summary>
    /// Порядок сортировки действия внутри группы.
    /// </summary>
    public int SortOrder { get; set; }
}
