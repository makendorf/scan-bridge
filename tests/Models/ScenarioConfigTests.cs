using ScanBridge.Models;

namespace ScanBridge.Tests.Models;

public class ScenarioConfigTests
{
    [Fact]
    public void Defaults_CorrectValues()
    {
        var config = new ScenarioConfig();

        Assert.Equal(0, config.Id);
        Assert.Equal(string.Empty, config.Name);
        Assert.Equal(string.Empty, config.Description);
        Assert.True(config.Enabled);
        Assert.NotNull(config.ScannerNames);
        Assert.Empty(config.ScannerNames);
        Assert.NotNull(config.Nodes);
        Assert.Empty(config.Nodes);
        Assert.NotNull(config.Connections);
        Assert.Empty(config.Connections);
    }

    [Fact]
    public void ScannerNames_Empty_MatchesAll()
    {
        var config = new ScenarioConfig
        {
            ScannerNames = new List<string>()
        };

        Assert.Empty(config.ScannerNames);
    }

    [Fact]
    public void Nodes_WithValues_Deserialized()
    {
        var config = new ScenarioConfig
        {
            Id = 1,
            Name = "Test Scenario",
            Description = "A test scenario",
            Enabled = true,
            ScannerNames = new List<string> { "Scanner1", "Scanner2" },
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Start", PositionX = 100, PositionY = 100 },
                new() { NodeId = "2", Type = "Action", PositionX = 200, PositionY = 200, ActionType = "Log" }
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "2" }
            }
        };

        Assert.Equal(1, config.Id);
        Assert.Equal("Test Scenario", config.Name);
        Assert.Equal(2, config.ScannerNames.Count);
        Assert.Equal(2, config.Nodes.Count);
        Assert.Single(config.Connections);
    }

    [Fact]
    public void ScenarioNodeConfig_Defaults()
    {
        var node = new ScenarioNodeConfig();

        Assert.Equal(string.Empty, node.NodeId);
        Assert.Equal(string.Empty, node.Type);
        Assert.Equal(0, node.PositionX);
        Assert.Equal(0, node.PositionY);
        Assert.Null(node.Settings);
        Assert.Null(node.ActionType);
    }

    [Fact]
    public void ScenarioConnectionConfig_Defaults()
    {
        var conn = new ScenarioConnectionConfig();

        Assert.Equal(string.Empty, conn.SourceNodeId);
        Assert.Equal(string.Empty, conn.TargetNodeId);
        Assert.Equal("output_1", conn.SourcePort);
        Assert.Equal("input_1", conn.TargetPort);
    }
}
