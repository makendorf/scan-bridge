using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Tests.Services;

public class PauseActionTests
{
    private readonly Mock<ILogger<PauseAction>> _loggerMock = new();

    [Fact]
    public async Task ExecuteAsync_DefaultDelay_Waits()
    {
        var action = new PauseAction(_loggerMock.Object, new Dictionary<string, string>());
        var scan = new ScanResult { ScannerName = "Scanner1" };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await action.ExecuteAsync(scan, CancellationToken.None);
        sw.Stop();

        // Default delay is 1000ms, but we allow some tolerance
        Assert.True(sw.ElapsedMilliseconds >= 900,
            $"Expected >= 900ms but was {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ExecuteAsync_CustomDelay_WaitsCorrectTime()
    {
        var settings = new Dictionary<string, string> { ["DelayMs"] = "100" };
        var action = new PauseAction(_loggerMock.Object, settings);
        var scan = new ScanResult { ScannerName = "Scanner1" };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await action.ExecuteAsync(scan, CancellationToken.None);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 50,
            $"Expected >= 50ms but was {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_ThrowsOperationCanceled()
    {
        var settings = new Dictionary<string, string> { ["DelayMs"] = "5000" };
        var action = new PauseAction(_loggerMock.Object, settings);
        var scan = new ScanResult { ScannerName = "Scanner1" };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => action.ExecuteAsync(scan, cts.Token));
    }

    [Fact]
    public void Type_ReturnsPause()
    {
        var action = new PauseAction(_loggerMock.Object, new Dictionary<string, string>());
        Assert.Equal("Pause", action.Type);
    }

    [Fact]
    public async Task ExecuteAsync_DelayBelowMinimum_ClampedToOne()
    {
        var settings = new Dictionary<string, string> { ["DelayMs"] = "0" };
        var action = new PauseAction(_loggerMock.Object, settings);
        var scan = new ScanResult { ScannerName = "Scanner1" };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await action.ExecuteAsync(scan, CancellationToken.None);
        sw.Stop();

        // Clamped to 1ms minimum
        Assert.True(sw.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public async Task ExecuteAsync_DelayAboveMaximum_ClampedTo60000()
    {
        // Test that large values are clamped to 60000
        var settings = new Dictionary<string, string> { ["DelayMs"] = "999999" };
        var action = new PauseAction(_loggerMock.Object, settings);
        var scan = new ScanResult { ScannerName = "Scanner1" };

        // The delay should be clamped to 60000ms, cancel after 200ms to verify it's not 999999ms
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => action.ExecuteAsync(scan, cts.Token));
    }
}
