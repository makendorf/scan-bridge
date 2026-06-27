using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Tests.Services;

public class AggregationActionTests : IDisposable
{
    private static ScanResult CreateScan(string data = "TEST123") => new()
    {
        RawData = data,
        ParsedData = data,
        Format = "Code128",
        IsValid = true,
        ScannerName = "Main",
        Timestamp = new DateTime(2025, 1, 15, 10, 30, 45, DateTimeKind.Utc)
    };

    [Fact]
    public async Task CountMode_TriggersOnThreshold()
    {
        var action = new AggregationAction(
            new Mock<ILogger<AggregationAction>>().Object,
            new Dictionary<string, string>
            {
                ["Mode"] = "count",
                ["CountThreshold"] = "3",
                ["BatchFormat"] = "json"
            });

        var scan1 = CreateScan("A");
        var scan2 = CreateScan("B");
        var scan3 = CreateScan("C");

        await action.ExecuteAsync(scan1, CancellationToken.None);
        await action.ExecuteAsync(scan2, CancellationToken.None);
        await action.ExecuteAsync(scan3, CancellationToken.None);

        Assert.Contains("[", scan3.ParsedData);
        Assert.Contains("\"data\":\"A\"", scan3.ParsedData);
        Assert.Contains("\"data\":\"B\"", scan3.ParsedData);
        Assert.Contains("\"data\":\"C\"", scan3.ParsedData);
    }

    [Fact]
    public async Task CountMode_NoTriggerBelowThreshold()
    {
        var action = new AggregationAction(
            new Mock<ILogger<AggregationAction>>().Object,
            new Dictionary<string, string>
            {
                ["Mode"] = "count",
                ["CountThreshold"] = "5"
            });

        var scan = CreateScan("A");
        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("A", scan.ParsedData);
    }

    [Fact]
    public async Task MaxBufferSize_ForceFlush()
    {
        var action = new AggregationAction(
            new Mock<ILogger<AggregationAction>>().Object,
            new Dictionary<string, string>
            {
                ["Mode"] = "count",
                ["CountThreshold"] = "100",
                ["MaxBufferSize"] = "3"
            });

        var scans = new List<ScanResult>();
        for (var i = 0; i < 5; i++)
        {
            var s = CreateScan($"Item{i}");
            scans.Add(s);
            await action.ExecuteAsync(s, CancellationToken.None);
        }

        var flushScan = scans[2];
        Assert.Contains("[", flushScan.ParsedData);
        Assert.Contains("\"data\":\"Item0\"", flushScan.ParsedData);
    }

    [Fact]
    public async Task CsvFormat_ProducesCsv()
    {
        var action = new AggregationAction(
            new Mock<ILogger<AggregationAction>>().Object,
            new Dictionary<string, string>
            {
                ["Mode"] = "count",
                ["CountThreshold"] = "2",
                ["BatchFormat"] = "csv"
            });

        var scan1 = CreateScan("A");
        var scan2 = CreateScan("B");

        await action.ExecuteAsync(scan1, CancellationToken.None);
        await action.ExecuteAsync(scan2, CancellationToken.None);

        Assert.Contains("data,format,scanner,timestamp", scan2.ParsedData);
        Assert.Contains("A,Code128,Main,", scan2.ParsedData);
    }

    [Fact]
    public async Task Dispose_FlushesRemaining()
    {
        var action = new AggregationAction(
            new Mock<ILogger<AggregationAction>>().Object,
            new Dictionary<string, string>
            {
                ["Mode"] = "count",
                ["CountThreshold"] = "100"
            });

        var scan = CreateScan("Remaining");
        await action.ExecuteAsync(scan, CancellationToken.None);

        action.Dispose();

        Assert.Contains("[", scan.ParsedData);
    }

    public void Dispose() { }
}
