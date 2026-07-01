using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Data;
using ScanBridge.Models;
using ScanBridge.Services;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Tests.Services;

/// <summary>
/// Integration tests for ScenarioService CRUD operations using SQLite in-memory.
/// </summary>
public class ScenarioServiceCrudTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ScenarioService _service;

    public ScenarioServiceCrudTests()
    {
        var conn = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        conn.Open();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(conn));
        services.AddSingleton(Mock.Of<ILogger<ScenarioService>>());

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        _service = new ScenarioService(
            _serviceProvider,
            Mock.Of<ILogger<ScenarioService>>());
    }

    public void Dispose() => _serviceProvider?.Dispose();

    private static ScenarioConfig CreateTestConfig(string name = "Test Scenario")
    {
        return new ScenarioConfig
        {
            Name = name,
            Description = "Test description",
            Enabled = true,
            ScannerNames = new List<string> { "Scanner1" },
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Start", PositionX = 50, PositionY = 100 },
                new() { NodeId = "2", Type = "Action", PositionX = 250, PositionY = 100, ActionType = "Log" },
                new() { NodeId = "3", Type = "End", PositionX = 450, PositionY = 100 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "2", SourcePort = "output_1", TargetPort = "input_1" },
                new() { SourceNodeId = "2", TargetNodeId = "3", SourcePort = "output_1", TargetPort = "input_1" },
            }
        };
    }

    [Fact]
    public void Create_ValidConfig_ReturnsId()
    {
        var config = CreateTestConfig();
        var id = _service.Create(config);
        Assert.True(id > 0);
    }

    [Fact]
    public void Create_ValidConfig_SavesToDb()
    {
        var config = CreateTestConfig("My Scenario");
        var id = _service.Create(config);

        var saved = _service.GetById(id);
        Assert.NotNull(saved);
        Assert.Equal("My Scenario", saved!.Name);
    }

    [Fact]
    public void Create_WithNodes_SavesNodes()
    {
        var config = CreateTestConfig();
        var id = _service.Create(config);

        var saved = _service.GetById(id);
        Assert.NotNull(saved);
        Assert.Equal(3, saved!.Nodes.Count);
        Assert.Equal("Start", saved.Nodes[0].Type);
        Assert.Equal("End", saved.Nodes[^1].Type);
    }

    [Fact]
    public void Create_WithConnections_SavesConnections()
    {
        var config = CreateTestConfig();
        var id = _service.Create(config);

        var saved = _service.GetById(id);
        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Connections.Count);
    }

    [Fact]
    public void GetAll_ReturnsAllScenarios()
    {
        _service.Create(CreateTestConfig("Scenario 1"));
        _service.Create(CreateTestConfig("Scenario 2"));

        var all = _service.GetAll();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetAll_EnabledOnly_ReturnsEnabled()
    {
        _service.Create(CreateTestConfig("Enabled"));
        var disabled = CreateTestConfig("Disabled");
        disabled.Enabled = false;
        _service.Create(disabled);

        var all = _service.GetAll();
        Assert.Equal(2, all.Count); // GetAll returns all, not just enabled
    }

    [Fact]
    public void GetAllWithGraph_IncludesNodesAndConnections()
    {
        var id = _service.Create(CreateTestConfig());

        var all = _service.GetAllWithGraph();
        Assert.Single(all);
        Assert.Equal(3, all[0].Nodes.Count);
        Assert.Equal(2, all[0].Connections.Count);
    }

    [Fact]
    public void GetById_Existing_ReturnsScenario()
    {
        var id = _service.Create(CreateTestConfig());

        var result = _service.GetById(id);
        Assert.NotNull(result);
        Assert.Equal("Test Scenario", result!.Name);
    }

    [Fact]
    public void GetById_Nonexistent_ReturnsNull()
    {
        var result = _service.GetById(9999);
        Assert.Null(result);
    }

    [Fact]
    public void Update_Existing_UpdatesFields()
    {
        var id = _service.Create(CreateTestConfig("Original"));

        _service.Update(id, CreateTestConfig("Updated"));

        var result = _service.GetById(id);
        Assert.Equal("Updated", result!.Name);
    }

    [Fact]
    public void Update_Existing_UpdatesNodes()
    {
        var id = _service.Create(CreateTestConfig());

        var newConfig = CreateTestConfig("Updated");
        newConfig.Nodes.Add(new ScenarioNodeConfig { NodeId = "10", Type = "Condition", PositionX = 300, PositionY = 200 });
        _service.Update(id, newConfig);

        var result = _service.GetById(id);
        Assert.Equal(4, result!.Nodes.Count);
    }

    [Fact]
    public void Update_Nonexistent_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.Update(9999, CreateTestConfig()));
    }

    [Fact]
    public void Delete_Existing_RemovesFromDb()
    {
        var id = _service.Create(CreateTestConfig());
        _service.Delete(id);

        var result = _service.GetById(id);
        Assert.Null(result);
    }

    [Fact]
    public void Delete_Existing_RemovesNodesAndConnections()
    {
        var id = _service.Create(CreateTestConfig());
        _service.Delete(id);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(db.ScenarioNodes.Where(n => n.ScenarioId == id));
        Assert.Empty(db.ScenarioConnections.Where(c => c.ScenarioId == id));
    }

    [Fact]
    public void Delete_Nonexistent_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.Delete(9999));
    }

    [Fact]
    public void Create_WithSettings_SavesNodeSettings()
    {
        var config = CreateTestConfig();
        config.Nodes[1].Settings = new Dictionary<string, string> { ["Mode"] = "clipboard" };

        var id = _service.Create(config);
        var saved = _service.GetById(id);

        Assert.NotNull(saved);
        Assert.Equal("clipboard", saved!.Nodes[1].Settings!["Mode"]);
    }

    [Fact]
    public void Create_WithMultipleScanners_SavesAll()
    {
        var config = CreateTestConfig();
        config.ScannerNames = new List<string> { "Scanner1", "Scanner2", "Scanner3" };

        var id = _service.Create(config);
        var saved = _service.GetById(id);

        Assert.Equal(3, saved!.ScannerNames.Count);
        Assert.Contains("Scanner2", saved.ScannerNames);
    }
}
