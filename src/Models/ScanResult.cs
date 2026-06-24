namespace ScanBridge.Models;

public class ScanResult
{
    public string RawData { get; set; } = string.Empty;
    public string ParsedData { get; set; } = string.Empty;
    public string Format { get; set; } = "Unknown";
    public bool IsValid { get; set; }
    public string ScannerName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ContentType { get; set; } = "Unknown";
    public string? ParsedContent { get; set; }
}
