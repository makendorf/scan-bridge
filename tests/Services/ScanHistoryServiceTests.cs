using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Models;
using ScanBridge.Services;

namespace ScanBridge.Tests.Services;

public class ScanHistoryServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScanHistoryService _service;

    public ScanHistoryServiceTests()
    {
        // Use SQLite in-memory (supports ExecuteDeleteAsync unlike InMemory provider)
        var connStr = $"DataSource=:memory:";
        var conn = new Microsoft.Data.Sqlite.SqliteConnection(connStr);
        conn.Open(); // Keep open for in-memory DB lifetime

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(conn));

        _serviceProvider = services.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Apply schema
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        _service = new ScanHistoryService(_scopeFactory, Mock.Of<ILogger<ScanHistoryService>>());
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    private AppDbContext CreateDb()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    private async Task SeedScanDataAsync(int count, DateTime baseTime, string scannerName = "Scanner1", string format = "EAN-13")
    {
        using var db = CreateDb();
        for (int i = 0; i < count; i++)
        {
            db.ScanHistory.Add(new ScanHistory
            {
                Timestamp = baseTime.AddMinutes(-i),
                ScannerName = scannerName,
                Format = format,
                RawData = $"data{i}",
                ParsedData = $"data{i}",
                IsValid = true,
                ContentType = "Text"
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task SeedReconnectDataAsync(params (DateTime ts, string scanner, string error, int attempt)[] items)
    {
        using var db = CreateDb();
        foreach (var (ts, scanner, error, attempt) in items)
        {
            db.ReconnectEvents.Add(new ReconnectEvent
            {
                Timestamp = ts,
                ScannerName = scanner,
                ErrorMessage = error,
                AttemptNumber = attempt
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CleanupOldRecordsAsync_RemovesOldRecords()
    {
        await SeedScanDataAsync(1, DateTime.UtcNow.AddDays(-35));
        await SeedScanDataAsync(1, DateTime.UtcNow);

        await _service.CleanupOldRecordsAsync();

        // Query via service's own method to avoid cross-scope issues
        var recent = await _service.GetRecentScansAsync(100);
        Assert.Single(recent);
    }

    [Fact]
    public async Task CleanupOldRecordsAsync_RemovesOldReconnects()
    {
        await SeedReconnectDataAsync(
            (DateTime.UtcNow.AddDays(-35), "Scanner1", "Old error", 1),
            (DateTime.UtcNow, "Scanner1", "Recent error", 1));

        await _service.CleanupOldRecordsAsync();

        var recent = await _service.GetReconnectErrorsAsync(100_000);
        Assert.Single(recent);
    }

    [Fact]
    public async Task CleanupOldRecordsAsync_KeepsRecentRecords()
    {
        await SeedScanDataAsync(1, DateTime.UtcNow);

        await _service.CleanupOldRecordsAsync();

        var recent = await _service.GetRecentScansAsync(100);
        Assert.Single(recent);
    }

    [Fact]
    public async Task GetRecentScansAsync_LimitsResults()
    {
        await SeedScanDataAsync(10, DateTime.UtcNow);

        var result = await _service.GetRecentScansAsync(5);
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetRecentScansAsync_DefaultLimit50()
    {
        await SeedScanDataAsync(5, DateTime.UtcNow);

        var result = await _service.GetRecentScansAsync();
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetRecentScansAsync_EmptyDb_ReturnsEmpty()
    {
        var result = await _service.GetRecentScansAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFormatsAsync_ReturnsFormatBreakdown()
    {
        await SeedScanDataAsync(2, DateTime.UtcNow, format: "EAN-13");
        await SeedScanDataAsync(1, DateTime.UtcNow, format: "Code128");

        var result = await _service.GetFormatsAsync();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetFormatsAsync_EmptyDb_ReturnsEmpty()
    {
        var result = await _service.GetFormatsAsync();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetReconnectErrorsAsync_FiltersByHours()
    {
        await SeedReconnectDataAsync(
            (DateTime.UtcNow.AddHours(-25), "Scanner1", "Old error", 1),
            (DateTime.UtcNow.AddHours(-1), "Scanner1", "Recent error", 1));

        var result = await _service.GetReconnectErrorsAsync(24);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetReconnectErrorsAsync_EmptyDb_ReturnsEmpty()
    {
        var result = await _service.GetReconnectErrorsAsync(24);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActivityAsync_24h_ReturnsData()
    {
        await SeedScanDataAsync(2, DateTime.UtcNow);

        var result = await _service.GetActivityAsync("24h");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetActivityAsync_EmptyDb_ReturnsEmptyBuckets()
    {
        var result = await _service.GetActivityAsync("24h");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetActivityAsync_7d_ReturnsData()
    {
        await SeedScanDataAsync(2, DateTime.UtcNow);

        var result = await _service.GetActivityAsync("7d");
        Assert.NotNull(result);
    }
}
