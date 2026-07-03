namespace ScanBridge.Models;

/// <summary>
/// DTO-модель для API.
/// </summary>
public class ScenarioConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<string> ScannerNames { get; set; } = new();
    public List<ScenarioNodeConfig> Nodes { get; set; } = new();
    public List<ScenarioConnectionConfig> Connections { get; set; } = new();

    /// <summary>
    /// Тип триггера сценария (Scanner, Http, Schedule, FileWatcher).
    /// </summary>
    public TriggerType TriggerType { get; set; } = TriggerType.Scanner;

    /// <summary>
    /// Настройки триггера (HTTP route, cron, file path и т.д.).
    /// </summary>
    public TriggerSettingsConfig? TriggerSettings { get; set; }
}

/// <summary>
/// Настройки триггера сценария.
/// </summary>
public class TriggerSettingsConfig
{
    // HTTP триггер
    public string? HttpMethod { get; set; }
    public string? RoutePath { get; set; }
    public string? AuthToken { get; set; }

    // Cron триггер
    public string? CronExpression { get; set; }
    public string? SchedulePayload { get; set; }

    // File watcher триггер
    public string? WatchPath { get; set; }
    public string? WatchFilter { get; set; }
    public string? WatchChangeTypes { get; set; }
}

public class ScenarioNodeConfig
{
    public string NodeId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public Dictionary<string, string>? Settings { get; set; }
    public string? ActionType { get; set; }
}

public class ScenarioConnectionConfig
{
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string SourcePort { get; set; } = "output_1";
    public string TargetPort { get; set; } = "input_1";
}
