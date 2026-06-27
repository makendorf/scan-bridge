namespace ScanBridge.Models;

/// <summary>
/// Конфигурация пост-скан действия.
/// Привязка к сканеру осуществляется на уровне группы (PostScanActionGroupConfig).
/// </summary>
public class PostScanActionConfig
{
    /// <summary>
    /// Тип действия: Log, ClipboardPaste, Export, Replacement и т.д.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Флаг активности действия. Если false, действие пропускается.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Дополнительные параметры действия в виде словаря ключ-значение.
    /// Содержимое зависит от типа действия.
    /// </summary>
    public Dictionary<string, string>? Settings { get; set; }
}
