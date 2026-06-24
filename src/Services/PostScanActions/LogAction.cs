using ScanBridge.Models;

namespace ScanBridge.Services.PostScanActions;

public class LogAction : IPostScanAction
{
    public string Type => "Log";

    private readonly ILogger<LogAction> _logger;

    public LogAction(ILogger<LogAction> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (!scan.IsValid)
        {
            _logger.LogWarning("Неверный штрихкод: {Raw}", scan.RawData);
        }
        else
        {
            _logger.LogInformation("Штрихкод {Format}: {Data}", scan.Format, scan.ParsedData);
        }

        return Task.CompletedTask;
    }
}
