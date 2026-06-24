using ScanBridge.Models;

namespace ScanBridge.Services;

public interface IPostScanAction
{
    string Type { get; }
    Task ExecuteAsync(ScanResult scan, CancellationToken ct);
}
