namespace ScanBridge.Models;

public class PostScanActionConfig
{
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string ScannerName { get; set; } = string.Empty;
    public Dictionary<string, string> Settings { get; set; } = new();
}
