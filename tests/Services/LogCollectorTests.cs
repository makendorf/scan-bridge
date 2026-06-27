using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Services;

namespace ScanBridge.Tests.Services;

public class LogCollectorTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly LogCollector _collector;
    private readonly string _dbPath;

    public LogCollectorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_logs_{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        services.AddSingleton<LogCollector>();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        _collector = _provider.GetRequiredService<LogCollector>();
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
    }

    private List<LogRecord> GetEntries()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Logs.OrderBy(l => l.Id).ToList();
    }

    [Fact]
    public void Add_StoresEntry()
    {
        _collector.Add("INF", "Test message");

        var entries = GetEntries();
        Assert.Single(entries);
        Assert.Equal("INF", entries[0].Level);
        Assert.Equal("Test message", entries[0].Message);
        Assert.Null(entries[0].Exception);
    }

    [Fact]
    public void Add_StoresException()
    {
        _collector.Add("ERR", "Error occurred", "System.Exception: boom");

        var entries = GetEntries();
        Assert.Single(entries);
        Assert.Equal("System.Exception: boom", entries[0].Exception);
    }

    [Fact]
    public void Add_MultipleEntries_ReturnsAll()
    {
        _collector.Add("INF", "First");
        _collector.Add("WRN", "Second");
        _collector.Add("ERR", "Third");

        var entries = GetEntries();
        Assert.Equal(3, entries.Count);
        Assert.Equal("First", entries[0].Message);
        Assert.Equal("Second", entries[1].Message);
        Assert.Equal("Third", entries[2].Message);
    }

    [Fact]
    public void Add_SetsTimestamp()
    {
        var before = DateTime.UtcNow;
        _collector.Add("INF", "Test");
        var after = DateTime.UtcNow;

        var entries = GetEntries();
        Assert.InRange(entries[0].Timestamp, before, after);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        _collector.Add("INF", "One");
        _collector.Add("WRN", "Two");
        _collector.Add("ERR", "Three");

        _collector.Clear();

        Assert.Empty(GetEntries());
    }

    [Fact]
    public void Clear_OnEmptyCollector_DoesNotThrow()
    {
        _collector.Clear();
        Assert.Empty(GetEntries());
    }

    [Fact]
    public void Clear_AfterClear_NewAddsAreAccepted()
    {
        _collector.Add("INF", "Old");
        _collector.Clear();
        _collector.Add("INF", "New");

        var entries = GetEntries();
        Assert.Single(entries);
        Assert.Equal("New", entries[0].Message);
    }
}
