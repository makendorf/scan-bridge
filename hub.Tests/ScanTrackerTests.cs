using ScanBridge.Services;

namespace ScanBridgeHub.Tests;

public class ScanTrackerTests
{
    [Fact]
    public void GetLastScan_Initial_DefaultValues()
    {
        var tracker = new ScanTracker();
        var (time, scannerName) = tracker.GetLastScan();
        Assert.Equal(DateTime.MinValue, time);
        Assert.Equal(string.Empty, scannerName);
    }

    [Fact]
    public void RecordScan_UpdatesTime()
    {
        var tracker = new ScanTracker();
        var before = DateTime.UtcNow;
        tracker.RecordScan("Scanner1");
        var (time, _) = tracker.GetLastScan();
        Assert.True(time >= before);
        Assert.True(time <= DateTime.UtcNow);
    }

    [Fact]
    public void RecordScan_UpdatesScannerName()
    {
        var tracker = new ScanTracker();
        tracker.RecordScan("MainScanner");
        var (_, scannerName) = tracker.GetLastScan();
        Assert.Equal("MainScanner", scannerName);
    }

    [Fact]
    public void RecordScan_MultipleCalls_KeepsLast()
    {
        var tracker = new ScanTracker();
        tracker.RecordScan("First");
        Thread.Sleep(10);
        tracker.RecordScan("Second");
        var (time, scannerName) = tracker.GetLastScan();
        Assert.Equal("Second", scannerName);
    }

    [Fact]
    public async Task RecordScan_ConcurrentAccess_DoesNotThrow()
    {
        var tracker = new ScanTracker();
        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() => tracker.RecordScan($"Scanner{i}")));
        await Task.WhenAll(tasks);
        var (_, name) = tracker.GetLastScan();
        Assert.StartsWith("Scanner", name);
    }

    [Fact]
    public void RecordScan_EmptyName_StoresEmpty()
    {
        var tracker = new ScanTracker();
        tracker.RecordScan("");
        var (_, scannerName) = tracker.GetLastScan();
        Assert.Equal("", scannerName);
    }
}
