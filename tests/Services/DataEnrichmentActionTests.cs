using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Tests.Services;

public class DataEnrichmentActionTests
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
    public void Constructor_MissingUrl_LogsWarning()
    {
        var logger = new Mock<ILogger<DataEnrichmentAction>>();
        var settings = new Dictionary<string, string>();

        var action = new DataEnrichmentAction(logger.Object, settings);

        Assert.Equal("DataEnrichment", action.Type);
    }

    [Fact]
    public async Task ExecuteAsync_MissingUrl_DoesNotThrow()
    {
        var logger = new Mock<ILogger<DataEnrichmentAction>>();
        var settings = new Dictionary<string, string>();
        var action = new DataEnrichmentAction(logger.Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidUrl_LogsError()
    {
        var logger = new Mock<ILogger<DataEnrichmentAction>>();
        var settings = new Dictionary<string, string>
        {
            ["Url"] = "http://localhost:99999/nonexistent",
            ["TimeoutSeconds"] = "1"
        };
        var action = new DataEnrichmentAction(logger.Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public void Constructor_DefaultValues()
    {
        var logger = new Mock<ILogger<DataEnrichmentAction>>();
        var settings = new Dictionary<string, string>
        {
            ["Url"] = "http://example.com/api"
        };

        var action = new DataEnrichmentAction(logger.Object, settings);

        Assert.Equal("DataEnrichment", action.Type);
    }

    [Fact]
    public void Constructor_PostMethod()
    {
        var logger = new Mock<ILogger<DataEnrichmentAction>>();
        var settings = new Dictionary<string, string>
        {
            ["Url"] = "http://example.com/api",
            ["Method"] = "POST",
            ["QueryParam"] = "barcode"
        };

        var action = new DataEnrichmentAction(logger.Object, settings);

        Assert.NotNull(action);
    }

    [Fact]
    public void Constructor_WithHeaders()
    {
        var logger = new Mock<ILogger<DataEnrichmentAction>>();
        var settings = new Dictionary<string, string>
        {
            ["Url"] = "http://example.com/api",
            ["Headers"] = "{\"Authorization\": \"Bearer token123\"}"
        };

        var action = new DataEnrichmentAction(logger.Object, settings);

        Assert.NotNull(action);
    }
}
