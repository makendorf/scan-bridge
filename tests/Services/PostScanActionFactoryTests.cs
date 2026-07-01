using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Services;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Tests.Services;

public class PostScanActionFactoryTests
{
    private readonly PostScanActionFactory _factory;

    public PostScanActionFactoryTests()
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());
        _factory = new PostScanActionFactory(loggerFactory.Object);
    }

    private static Dictionary<string, string> DefaultSettings() => new();

    [Fact]
    public void Create_Log_ReturnsLogAction()
    {
        var result = _factory.Create("Log", DefaultSettings());
        Assert.NotNull(result);
        Assert.IsType<LogAction>(result);
        Assert.Equal("Log", result!.Type);
    }

    [Fact]
    public void Create_Replacement_ReturnsReplacementAction()
    {
        var result = _factory.Create("Replacement", DefaultSettings());
        Assert.NotNull(result);
        Assert.IsType<ReplacementAction>(result);
        Assert.Equal("Replacement", result!.Type);
    }

    [Fact]
    public void Create_ClipboardPaste_ReturnsClipboardPasteAction()
    {
        var result = _factory.Create("ClipboardPaste", DefaultSettings());
        Assert.NotNull(result);
        Assert.IsType<ClipboardPasteAction>(result);
        Assert.Equal("ClipboardPaste", result!.Type);
    }

    [Fact]
    public void Create_Export_ReturnsExportAction()
    {
        var result = _factory.Create("Export", DefaultSettings());
        Assert.NotNull(result);
        Assert.IsType<ExportAction>(result);
        Assert.Equal("Export", result!.Type);
    }

    [Fact]
    public void Create_Telegram_ReturnsTelegramNotificationAction()
    {
        var result = _factory.Create("Telegram", DefaultSettings());
        Assert.NotNull(result);
        Assert.IsType<TelegramNotificationAction>(result);
        Assert.Equal("Telegram", result!.Type);
    }

    [Fact]
    public void Create_Email_ReturnsEmailNotificationAction()
    {
        var result = _factory.Create("Email", DefaultSettings());
        Assert.NotNull(result);
        Assert.IsType<EmailNotificationAction>(result);
        Assert.Equal("Email", result!.Type);
    }

    [Fact]
    public void Create_DataEnrichment_ReturnsDataEnrichmentAction()
    {
        var result = _factory.Create("DataEnrichment", DefaultSettings());
        Assert.NotNull(result);
        Assert.IsType<DataEnrichmentAction>(result);
        Assert.Equal("DataEnrichment", result!.Type);
    }

    [Fact]
    public void Create_Validation_ReturnsValidationAction()
    {
        var result = _factory.Create("Validation", DefaultSettings());
        Assert.NotNull(result);
        Assert.IsType<ValidationAction>(result);
        Assert.Equal("Validation", result!.Type);
    }

    [Fact]
    public void Create_Aggregation_ReturnsAggregationAction()
    {
        var result = _factory.Create("Aggregation", DefaultSettings());
        Assert.NotNull(result);
        Assert.IsType<AggregationAction>(result);
        Assert.Equal("Aggregation", result!.Type);
    }

    [Fact]
    public void Create_DatabaseQuery_ReturnsDatabaseQueryAction()
    {
        var result = _factory.Create("DatabaseQuery", DefaultSettings());
        Assert.NotNull(result);
        Assert.IsType<DatabaseQueryAction>(result);
        Assert.Equal("DatabaseQuery", result!.Type);
    }

    [Fact]
    public void Create_Pause_ReturnsPauseAction()
    {
        var result = _factory.Create("Pause", DefaultSettings());
        Assert.NotNull(result);
        Assert.IsType<PauseAction>(result);
        Assert.Equal("Pause", result!.Type);
    }

    [Fact]
    public void Create_Unknown_ReturnsNull()
    {
        var result = _factory.Create("UnknownType", DefaultSettings());
        Assert.Null(result);
    }
}
