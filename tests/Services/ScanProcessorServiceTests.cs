using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services;

namespace ScanBridge.Tests.Services;

public class ScanProcessorServiceTests
{
    private readonly Mock<ILogger<ScanProcessorService>> _loggerMock = new();
    private readonly Mock<PostScanManager> _postScanMock;
    private readonly ScanProcessorService _processor;
    private readonly ScanTracker _scanTracker;

    public ScanProcessorServiceTests()
    {
        _postScanMock = new Mock<PostScanManager>(
            new Mock<IPostScanActionFactory>().Object,
            new Mock<ILogger<PostScanManager>>().Object);
        _scanTracker = new ScanTracker();
        var historyService = new ScanHistoryService(
            new Mock<IServiceScopeFactory>().Object,
            new Mock<ILogger<ScanHistoryService>>().Object);
        _processor = new ScanProcessorService(_postScanMock.Object, _scanTracker, historyService, _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessAsync_InvalidBarcode_LogsWarning()
    {
        var scan = new ScanResult
        {
            RawData = "XYZ",
            ParsedData = "XYZ",
            Format = "Unknown",
            IsValid = false
        };

        await _processor.ProcessAsync(scan, CancellationToken.None);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Неверный штрихкод")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_InvalidBarcode_DoesNotCallPostScan()
    {
        var scan = new ScanResult
        {
            RawData = "XYZ",
            IsValid = false
        };

        await _processor.ProcessAsync(scan, CancellationToken.None);

        _postScanMock.Verify(
            x => x.ExecuteAllAsync(scan, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ValidBarcode_CallsPostScan()
    {
        var scan = new ScanResult
        {
            RawData = "1234567890123",
            ParsedData = "1234567890123",
            Format = "EAN-13",
            IsValid = true
        };

        await _processor.ProcessAsync(scan, CancellationToken.None);

        _postScanMock.Verify(
            x => x.ExecuteAllAsync(scan, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsCompletedTask()
    {
        var scan = new ScanResult
        {
            RawData = "12345678",
            ParsedData = "12345678",
            Format = "EAN-8",
            IsValid = true
        };

        await _processor.ProcessAsync(scan, CancellationToken.None);
    }

    [Fact]
    public async Task ProcessAsync_Invalid_ReturnsCompletedTask()
    {
        var scan = new ScanResult { IsValid = false };

        await _processor.ProcessAsync(scan, CancellationToken.None);
    }

    [Fact]
    public async Task ProcessAsync_Cancellation_DoesNotThrow()
    {
        var scan = new ScanResult
        {
            RawData = "12345678",
            IsValid = true
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await _processor.ProcessAsync(scan, cts.Token);
    }

    [Fact]
    public async Task ProcessAsync_RecordsScanTime()
    {
        var scan = new ScanResult
        {
            RawData = "1234567890123",
            ScannerName = "TestScanner",
            IsValid = true
        };

        await _processor.ProcessAsync(scan, CancellationToken.None);

        var (time, scannerName) = _scanTracker.GetLastScan();
        Assert.True(time > DateTime.MinValue);
        Assert.Equal("TestScanner", scannerName);
    }

    [Fact]
    public async Task ProcessAsync_InvalidBarcode_DoesNotRecordScanTime()
    {
        var scan = new ScanResult
        {
            RawData = "XYZ",
            ScannerName = "BadScanner",
            IsValid = false
        };

        await _processor.ProcessAsync(scan, CancellationToken.None);

        var (time, scannerName) = _scanTracker.GetLastScan();
        Assert.Equal(DateTime.MinValue, time);
        Assert.Equal(string.Empty, scannerName);
    }
}
