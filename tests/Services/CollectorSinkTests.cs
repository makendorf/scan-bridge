using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Serilog.Parsing;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Services;

namespace ScanBridge.Tests.Services;

public class CollectorSinkTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly LogCollector _collector;
    private readonly CollectorSink _sink;
    private readonly string _dbPath;

    public CollectorSinkTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_sink_{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        services.AddSingleton<LogCollector>();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        _collector = _provider.GetRequiredService<LogCollector>();
        _sink = new CollectorSink(() => _collector);
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
    }

    private static LogEvent CreateLogEvent(LogEventLevel level, string messageTemplate, Exception? exception = null)
    {
        var template = new MessageTemplate(messageTemplate, []);
        return new LogEvent(DateTimeOffset.Now, level, exception, template, []);
    }

    private List<LogRecord> GetEntries()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Logs.OrderBy(l => l.Id).ToList();
    }

    [Fact]
    public void Emit_Information_MapsToINF()
    {
        _sink.Emit(CreateLogEvent(LogEventLevel.Information, "Info message"));
        Assert.Equal("INF", GetEntries()[0].Level);
    }

    [Fact]
    public void Emit_Warning_MapsToWRN()
    {
        _sink.Emit(CreateLogEvent(LogEventLevel.Warning, "Warning message"));
        Assert.Equal("WRN", GetEntries()[0].Level);
    }

    [Fact]
    public void Emit_Error_MapsToERR()
    {
        _sink.Emit(CreateLogEvent(LogEventLevel.Error, "Error message"));
        Assert.Equal("ERR", GetEntries()[0].Level);
    }

    [Fact]
    public void Emit_Debug_MapsToDBG()
    {
        _sink.Emit(CreateLogEvent(LogEventLevel.Debug, "Debug message"));
        Assert.Equal("DBG", GetEntries()[0].Level);
    }

    [Fact]
    public void Emit_Fatal_MapsToFTL()
    {
        _sink.Emit(CreateLogEvent(LogEventLevel.Fatal, "Fatal message"));
        Assert.Equal("FTL", GetEntries()[0].Level);
    }

    [Fact]
    public void Emit_Verbose_MapsToVRB()
    {
        _sink.Emit(CreateLogEvent(LogEventLevel.Verbose, "Verbose message"));
        Assert.Equal("VRB", GetEntries()[0].Level);
    }

    [Fact]
    public void Emit_WithException_IncludesExceptionText()
    {
        var ex = new InvalidOperationException("test error");
        _sink.Emit(CreateLogEvent(LogEventLevel.Error, "Failed", ex));
        var entry = GetEntries()[0];
        Assert.Contains("InvalidOperationException", entry.Exception!);
        Assert.Contains("test error", entry.Exception!);
    }

    [Fact]
    public void Emit_WithoutException_ExceptionIsNull()
    {
        _sink.Emit(CreateLogEvent(LogEventLevel.Information, "Clean"));
        Assert.Null(GetEntries()[0].Exception);
    }
}
