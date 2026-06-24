using ScanBridge.Models;

namespace ScanBridge.Services;

public class ScanProcessorService
{
    private readonly PostScanManager _postScanManager;
    private readonly ILogger<ScanProcessorService> _logger;

    public ScanProcessorService(PostScanManager postScanManager, ILogger<ScanProcessorService> logger)
    {
        _postScanManager = postScanManager;
        _logger = logger;
    }

    public async Task ProcessAsync(ScanResult scan, CancellationToken ct)
    {
        if (!scan.IsValid)
        {
            _logger.LogWarning("Неверный штрихкод: {Raw}", scan.RawData);
        }

        await _postScanManager.ExecuteAllAsync(scan, ct);
    }
}
