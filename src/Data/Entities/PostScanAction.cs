namespace ScanBridge.Data.Entities;

public class PostScanAction
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string ScannerName { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = "{}";
    public int SortOrder { get; set; }
}
