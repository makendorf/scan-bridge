using ScanBridge.Models;

namespace ScanBridge.Services;

/// <summary>
/// Унифицированный диспетчер триггеров сценариев.
/// Создаёт ScanResult с нужными полями и делегирует в ScanDispatcher.
/// </summary>
public class TriggerDispatcher
{
    private readonly ScanDispatcher _scanDispatcher;

    public TriggerDispatcher(ScanDispatcher scanDispatcher)
    {
        _scanDispatcher = scanDispatcher;
    }

    /// <summary>
    /// Диспетчеризация триггера сканера (существующий поток).
    /// </summary>
    public Task DispatchScannerTriggerAsync(ScanResult scan, CancellationToken ct)
    {
        scan.TriggerType = "Scanner";
        scan.TriggerSource = scan.ScannerName;
        return _scanDispatcher.ExecuteAllAsync(scan, ct);
    }

    /// <summary>
    /// Диспетчеризация HTTP триггера.
    /// </summary>
    public Task DispatchHttpTriggerAsync(string routePath, string body, Dictionary<string, string> headers, CancellationToken ct)
    {
        var scan = new ScanResult
        {
            ParsedData = body,
            RawData = body,
            Format = "Http",
            IsValid = true,
            TriggerType = "Http",
            TriggerSource = routePath,
            Timestamp = DateTime.UtcNow
        };

        foreach (var h in headers)
            scan.Metadata[h.Key] = h.Value;

        return _scanDispatcher.ExecuteAllAsync(scan, ct);
    }

    /// <summary>
    /// Диспетчеризация cron триггера.
    /// </summary>
    public Task DispatchScheduleTriggerAsync(string payload, CancellationToken ct)
    {
        var scan = new ScanResult
        {
            ParsedData = payload ?? "",
            RawData = payload ?? "",
            Format = "Schedule",
            IsValid = true,
            TriggerType = "Schedule",
            TriggerSource = "cron",
            Timestamp = DateTime.UtcNow
        };

        return _scanDispatcher.ExecuteAllAsync(scan, ct);
    }

    /// <summary>
    /// Диспетчеризация файлового триггера.
    /// </summary>
    public Task DispatchFileTriggerAsync(string filePath, string content, CancellationToken ct)
    {
        var scan = new ScanResult
        {
            ParsedData = content,
            RawData = content,
            Format = "File",
            IsValid = true,
            TriggerType = "FileWatcher",
            TriggerSource = filePath,
            Timestamp = DateTime.UtcNow
        };

        scan.Metadata["filePath"] = filePath;

        return _scanDispatcher.ExecuteAllAsync(scan, ct);
    }
}
