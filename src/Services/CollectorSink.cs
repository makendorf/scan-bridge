using Serilog.Core;
using Serilog.Events;

namespace ScanBridge.Services;

public class CollectorSink : ILogEventSink
{
    private readonly Func<LogCollector> _getCollector;

    public CollectorSink(Func<LogCollector> getCollector) => _getCollector = getCollector;

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        var exception = logEvent.Exception?.ToString();
        var level = logEvent.Level switch
        {
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Fatal => "FTL",
            LogEventLevel.Verbose => "VRB",
            _ => logEvent.Level.ToString().ToUpperInvariant()
        };
        _getCollector().Add(level, message, exception);
    }
}
