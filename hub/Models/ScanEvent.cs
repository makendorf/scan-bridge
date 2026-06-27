namespace ScanBridgeHub.Models;

public class ScanEvent
{
    public long Id { get; set; }
    public int InstanceId { get; set; }
    public string InstanceName { get; set; } = string.Empty;
    public string ScannerName { get; set; } = string.Empty;
    public string RawData { get; set; } = string.Empty;
    public string ParsedData { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public record ScanHistoryQuery(
    int? InstanceId = null,
    string? ScannerName = null,
    DateTime? From = null,
    DateTime? To = null,
    int Limit = 100
);
