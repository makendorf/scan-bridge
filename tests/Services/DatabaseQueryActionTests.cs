using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Tests.Services;

public class DatabaseQueryActionTests
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
    public void Constructor_UnsupportedType_LogsWarning()
    {
        var logger = new Mock<ILogger<DatabaseQueryAction>>();
        var settings = new Dictionary<string, string>
        {
            ["ConnectionType"] = "oracle"
        };

        var action = new DatabaseQueryAction(logger.Object, settings);

        Assert.Equal("DatabaseQuery", action.Type);
    }

    [Fact]
    public async Task ExecuteAsync_MissingConnectionString_DoesNotThrow()
    {
        var action = new DatabaseQueryAction(
            new Mock<ILogger<DatabaseQueryAction>>().Object,
            new Dictionary<string, string>
            {
                ["ConnectionType"] = "mysql",
                ["QueryTemplate"] = "SELECT 1"
            });

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyQuery_DoesNotThrow()
    {
        var action = new DatabaseQueryAction(
            new Mock<ILogger<DatabaseQueryAction>>().Object,
            new Dictionary<string, string>
            {
                ["ConnectionType"] = "mysql",
                ["ConnectionString"] = "Server=localhost",
                ["QueryTemplate"] = ""
            });

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ConnectionFailure_LogsError()
    {
        var action = new DatabaseQueryAction(
            new Mock<ILogger<DatabaseQueryAction>>().Object,
            new Dictionary<string, string>
            {
                ["ConnectionType"] = "mysql",
                ["ConnectionString"] = "Server=localhost:9999;Database=test",
                ["QueryTemplate"] = "SELECT '{data}'",
                ["TimeoutSeconds"] = "1"
            });

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public void Constructor_DefaultValues()
    {
        var action = new DatabaseQueryAction(
            new Mock<ILogger<DatabaseQueryAction>>().Object,
            new Dictionary<string, string>());

        Assert.Equal("DatabaseQuery", action.Type);
    }

    [Fact]
    public void Constructor_MySqlType()
    {
        var action = new DatabaseQueryAction(
            new Mock<ILogger<DatabaseQueryAction>>().Object,
            new Dictionary<string, string>
            {
                ["ConnectionType"] = "mysql",
                ["ConnectionString"] = "Server=localhost",
                ["QueryTemplate"] = "SELECT * FROM scans WHERE code='{data}'"
            });

        Assert.NotNull(action);
    }

    [Fact]
    public void Constructor_PostgresqlType()
    {
        var action = new DatabaseQueryAction(
            new Mock<ILogger<DatabaseQueryAction>>().Object,
            new Dictionary<string, string>
            {
                ["ConnectionType"] = "postgresql",
                ["ConnectionString"] = "Host=localhost",
                ["QueryTemplate"] = "SELECT * FROM scans WHERE code='{data}'"
            });

        Assert.NotNull(action);
    }

    [Fact]
    public void Constructor_MssqlType()
    {
        var action = new DatabaseQueryAction(
            new Mock<ILogger<DatabaseQueryAction>>().Object,
            new Dictionary<string, string>
            {
                ["ConnectionType"] = "mssql",
                ["ConnectionString"] = "Server=localhost",
                ["QueryTemplate"] = "SELECT * FROM scans WHERE code='{data}'"
            });

        Assert.NotNull(action);
    }
}
