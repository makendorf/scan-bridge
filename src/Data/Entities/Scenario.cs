using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanBridge.Data.Entities;

/// <summary>
/// Визуальный сценарий обработки сканирований.
/// </summary>
public class Scenario
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// JSON массив имён сканеров, к которым привязан сценарий.
    /// Пустой массив = привязка ко всем сканерам.
    /// </summary>
    public string ScannerNamesJson { get; set; } = "[]";

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Тип триггера: Scanner, Http, Schedule, FileWatcher.
    /// </summary>
    [MaxLength(20)]
    public string TriggerType { get; set; } = "Scanner";

    /// <summary>
    /// JSON настройки триггера (route, cron, file path и т.д.).
    /// </summary>
    [MaxLength(4000)]
    public string? TriggerSettingsJson { get; set; }

    [NotMapped]
    public List<ScenarioNode> Nodes { get; set; } = new();

    [NotMapped]
    public List<ScenarioConnection> Connections { get; set; } = new();
}
