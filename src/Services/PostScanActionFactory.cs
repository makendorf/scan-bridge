using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Services;

public class PostScanActionFactory : IPostScanActionFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public PostScanActionFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IPostScanAction? Create(string type, Dictionary<string, string> settings)
    {
        return type switch
        {
            "Log" => new LogAction(_loggerFactory.CreateLogger<LogAction>()),
            "ClipboardPaste" => new ClipboardPasteAction(
                _loggerFactory.CreateLogger<ClipboardPasteAction>(),
                settings),
            "Replacement" => new ReplacementAction(
                _loggerFactory.CreateLogger<ReplacementAction>(),
                settings),
            "Export" => new ExportAction(
                _loggerFactory.CreateLogger<ExportAction>(),
                settings),
            "WindowPaste" => new WindowPasteAction(
                _loggerFactory.CreateLogger<WindowPasteAction>(),
                settings),
            "Telegram" => new TelegramNotificationAction(
                _loggerFactory.CreateLogger<TelegramNotificationAction>(),
                settings),
            "Email" => new EmailNotificationAction(
                _loggerFactory.CreateLogger<EmailNotificationAction>(),
                settings),
            "DataEnrichment" => new DataEnrichmentAction(
                _loggerFactory.CreateLogger<DataEnrichmentAction>(),
                settings),
            "Validation" => new ValidationAction(
                _loggerFactory.CreateLogger<ValidationAction>(),
                settings),
            "Aggregation" => new AggregationAction(
                _loggerFactory.CreateLogger<AggregationAction>(),
                settings),
            "DatabaseQuery" => new DatabaseQueryAction(
                _loggerFactory.CreateLogger<DatabaseQueryAction>(),
                settings),
            "Pause" => new PauseAction(
                _loggerFactory.CreateLogger<PauseAction>(),
                settings),
            _ => null
        };
    }
}
