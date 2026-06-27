namespace ScanBridgeHub.Models;

public class AlertRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string SettingsJson { get; set; } = "{}";
    public DateTime? LastTriggered { get; set; }
}

public record AlertRuleRequest(string Name, string Type, bool Enabled, Dictionary<string, string> Settings);

public record AlertNotification(
    int InstanceId,
    string InstanceName,
    string AlertType,
    string Message,
    DateTime Timestamp
);
