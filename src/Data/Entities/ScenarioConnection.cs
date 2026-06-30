using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanBridge.Data.Entities;

/// <summary>
/// Связь между узлами сценария (Drawflow connection).
/// </summary>
public class ScenarioConnection
{
    public int Id { get; set; }

    public int ScenarioId { get; set; }

    /// <summary>
    /// ID исходного узла.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string SourceNodeId { get; set; } = string.Empty;

    /// <summary>
    /// ID целевого узла.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TargetNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Порт исходного узла: "output_1".
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string SourcePort { get; set; } = "output_1";

    /// <summary>
    /// Порт целевого узла: "input_1", "true_1", "false_1".
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TargetPort { get; set; } = "input_1";

    [NotMapped]
    public Scenario? Scenario { get; set; }
}
