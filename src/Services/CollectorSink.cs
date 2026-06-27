using Serilog.Core;
using Serilog.Events;

namespace ScanBridge.Services;

/// <summary>
/// Sink для Serilog, перенаправляющий логи в LogCollector для сохранения в БД.
/// Преобразует уровни Serilog в строковые обозначения (INF, WRN, ERR и т.д.).
/// </summary>
public class CollectorSink : ILogEventSink
{
    private readonly Func<LogCollector> _getCollector;

    /// <summary>
    /// Создаёт экземпляр sink-а.
    /// </summary>
    /// <param name="getCollector">Функция получения экземпляра LogCollector (ленивая инициализация).</param>
    public CollectorSink(Func<LogCollector> getCollector) => _getCollector = getCollector;

    /// <summary>
    /// Обрабатывает событие лога: извлекает сообщение, исключение и уровень,
    /// передаёт их в LogCollector для сохранения в БД.
    /// </summary>
    /// <param name="logEvent">Событие лога Serilog.</param>
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
