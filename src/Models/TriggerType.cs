namespace ScanBridge.Models;

/// <summary>
/// Тип триггера сценария.
/// </summary>
public enum TriggerType
{
    Scanner = 0,
    Http = 1,
    Schedule = 2,
    FileWatcher = 3
}
