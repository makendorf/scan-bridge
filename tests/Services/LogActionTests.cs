using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Tests.Services;

public class LogActionTests
{
    private readonly Mock<ILogger<LogAction>> _loggerMock = new();

    [Fact]
    public async Task ExecuteAsync_ValidScan_LogsInformation()
    {
        var action = new LogAction(_loggerMock.Object);
        var scan = new ScanResult
        {
            RawData = "12345",
            ParsedData = "12345",
            Format = "EAN-13",
            IsValid = true,
            ScannerName = "Scanner1"
        };

        await action.ExecuteAsync(scan, CancellationToken.None);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("EAN-13")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidScan_LogsWarning()
    {
        var action = new LogAction(_loggerMock.Object);
        var scan = new ScanResult
        {
            RawData = "12345",
            ParsedData = "12345",
            Format = "Unknown",
            IsValid = false,
            ScannerName = "Scanner1"
        };

        await action.ExecuteAsync(scan, CancellationToken.None);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Type_ReturnsLog()
    {
        var action = new LogAction(_loggerMock.Object);
        Assert.Equal("Log", action.Type);
    }
}
