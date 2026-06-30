using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanBridge.Data.Entities;

/// <summary>
/// Узел визуального сценария (Drawflow node).
/// </summary>
public class ScenarioNode
{
    public int Id { get; set; }

    public int ScenarioId { get; set; }

    /// <summary>
    /// ID узла в Drawflow (строка).
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Тип узла: Start, Action, Condition, End.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Позиция X на canvas (пиксели).
    /// </summary>
    public double PositionX { get; set; }

    /// <summary>
    /// Позиция Y на canvas (пиксели).
    /// </summary>
    public double PositionY { get; set; }

    /// <summary>
    /// JSON настройки узла (типа действия, параметры условия и т.д.).
    /// </summary>
    public string SettingsJson { get; set; } = "{}";

    /// <summary>
    /// Тип действия (для Action узлов): Log, ClipboardPaste, SaveToFile и т.д.
    /// null для Start, End, Condition.
    /// </summary>
    [MaxLength(100)]
    public string? ActionType { get; set; }

    [NotMapped]
    public Scenario? Scenario { get; set; }
}
