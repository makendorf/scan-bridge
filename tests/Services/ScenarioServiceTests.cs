using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Data;
using ScanBridge.Models;
using ScanBridge.Services;
using ScanBridge.Services.VisualScripting;

namespace Tests.Services;

public class ScenarioServiceTests
{
    private readonly Mock<ILogger<ScenarioService>> _loggerMock = new();

    [Fact]
    public void Validate_ValidGraph_ReturnsSuccess()
    {
        var service = CreateService();
        var config = CreateValidScenarioConfig();

        var result = service.Validate(config);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_EmptyGraph_ReturnsError()
    {
        var service = CreateService();
        var config = new ScenarioConfig { Name = "Empty", Nodes = new(), Connections = new() };

        var result = service.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains("Сценарий не содержит узлов", result.Errors);
    }

    [Fact]
    public void Validate_NoStartNode_ReturnsError()
    {
        var service = CreateService();
        var config = new ScenarioConfig
        {
            Name = "NoStart",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "End", PositionX = 0, PositionY = 0 }
            },
            Connections = new()
        };

        var result = service.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains("Отсутствует узел Start", result.Errors);
    }

    [Fact]
    public void Validate_NoEndNode_ReturnsError()
    {
        var service = CreateService();
        var config = new ScenarioConfig
        {
            Name = "NoEnd",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Start", PositionX = 0, PositionY = 0 }
            },
            Connections = new()
        };

        var result = service.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains("Отсутствует узел End", result.Errors);
    }

    [Fact]
    public void Validate_DisconnectedNode_ReturnsError()
    {
        var service = CreateService();
        var config = new ScenarioConfig
        {
            Name = "Disconnected",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Start", PositionX = 0, PositionY = 0 },
                new() { NodeId = "2", Type = "Action", PositionX = 200, PositionY = 0 },
                new() { NodeId = "3", Type = "End", PositionX = 400, PositionY = 0 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "3" },
            }
        };

        var result = service.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("не связан"));
    }

    [Fact]
    public void MigrateGroup_CreatesLinearScenario()
    {
        var service = CreateService();
        var group = new PostScanActionGroupConfig
        {
            Name = "TestGroup",
            Enabled = true,
            ScannerNames = new List<string> { "Scanner1" },
            Actions = new List<PostScanActionConfig>
            {
                new() { Type = "Log", Enabled = true },
                new() { Type = "ClipboardPaste", Enabled = true },
            }
        };

        var scenario = service.MigrateGroup(group);

        Assert.Equal("TestGroup", scenario.Name);
        Assert.Equal(4, scenario.Nodes.Count); // Start + 2 Actions + End
        Assert.Equal(3, scenario.Connections.Count);
        Assert.Equal("Start", scenario.Nodes[0].Type);
        Assert.Equal("End", scenario.Nodes[^1].Type);
    }

    private ScenarioService CreateService()
    {
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(s => s.GetService(typeof(AppDbContext)))
            .Throws(new InvalidOperationException("DB not available in unit tests"));

        return new ScenarioService(serviceProviderMock.Object, _loggerMock.Object);
    }

    private static ScenarioConfig CreateValidScenarioConfig()
    {
        return new ScenarioConfig
        {
            Name = "Valid",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Start", PositionX = 0, PositionY = 0 },
                new() { NodeId = "2", Type = "Action", PositionX = 200, PositionY = 0, ActionType = "Log" },
                new() { NodeId = "3", Type = "End", PositionX = 400, PositionY = 0 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "2" },
                new() { SourceNodeId = "2", TargetNodeId = "3" },
            }
        };
    }
}
