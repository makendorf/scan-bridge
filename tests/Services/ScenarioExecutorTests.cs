using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services;
using ScanBridge.Services.VisualScripting;

namespace Tests.Services;

public class ScenarioExecutorTests
{
    private readonly Mock<IPostScanActionFactory> _factoryMock = new();
    private readonly Mock<ILogger<ScenarioExecutor>> _loggerMock = new();
    private readonly ScenarioExecutor _executor;

    public ScenarioExecutorTests()
    {
        _executor = new ScenarioExecutor(_factoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Compile_LinearGraph_ReturnsSuccess()
    {
        var scenario = CreateLinearScenario();
        var result = _executor.Compile(scenario);
        Assert.NotNull(result);
        Assert.Equal("Start", result.StartNode.Config.Type);
    }

    [Fact]
    public void Compile_GraphWithCycle_ReturnsNull()
    {
        var scenario = new ScenarioConfig
        {
            Name = "Cycle",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Start", PositionX = 0, PositionY = 0 },
                new() { NodeId = "2", Type = "Action", PositionX = 200, PositionY = 0, ActionType = "Log" },
                new() { NodeId = "3", Type = "End", PositionX = 400, PositionY = 0 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "2" },
                new() { SourceNodeId = "2", TargetNodeId = "1" }, // cycle
                new() { SourceNodeId = "2", TargetNodeId = "3" },
            }
        };

        var result = _executor.Compile(scenario);
        Assert.Null(result);
    }

    [Fact]
    public async Task Execute_LinearGraph_ActionCalled()
    {
        var actionMock = new Mock<IPostScanAction>();
        actionMock.Setup(a => a.Type).Returns("Log");
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(actionMock.Object);

        var scenario = CreateLinearScenario();
        var compiled = _executor.Compile(scenario);
        Assert.NotNull(compiled);

        var scan = new ScanResult { ParsedData = "TEST123", IsValid = true };
        await _executor.ExecuteAsync(compiled, scan, CancellationToken.None);

        actionMock.Verify(a => a.ExecuteAsync(scan, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ConditionTrue_PathTaken()
    {
        var action1Mock = new Mock<IPostScanAction>();
        action1Mock.Setup(a => a.Type).Returns("Log");
        var action2Mock = new Mock<IPostScanAction>();
        action2Mock.Setup(a => a.Type).Returns("Telegram");

        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(action1Mock.Object);
        _factoryMock.Setup(f => f.Create("Telegram", It.IsAny<Dictionary<string, string>>()))
            .Returns(action2Mock.Object);

        var scenario = new ScenarioConfig
        {
            Name = "Condition",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Start", PositionX = 0, PositionY = 0 },
                new() { NodeId = "cond", Type = "Condition", PositionX = 200, PositionY = 0,
                    Settings = new Dictionary<string, string> { ["operator"] = "contains", ["field"] = "data", ["value"] = "TEST" } },
                new() { NodeId = "2", Type = "Action", PositionX = 400, PositionY = -100, ActionType = "Log" },
                new() { NodeId = "3", Type = "Action", PositionX = 400, PositionY = 100, ActionType = "Telegram" },
                new() { NodeId = "end", Type = "End", PositionX = 600, PositionY = 0 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "cond", SourcePort = "output_1", TargetPort = "input_1" },
                new() { SourceNodeId = "cond", TargetNodeId = "2", SourcePort = "output_1", TargetPort = "input_1" },
                new() { SourceNodeId = "cond", TargetNodeId = "3", SourcePort = "output_2", TargetPort = "input_1" },
                new() { SourceNodeId = "2", TargetNodeId = "end", SourcePort = "output_1", TargetPort = "input_1" },
                new() { SourceNodeId = "3", TargetNodeId = "end", SourcePort = "output_1", TargetPort = "input_1" },
            }
        };

        var compiled = _executor.Compile(scenario);
        Assert.NotNull(compiled);

        var scan = new ScanResult { ParsedData = "TEST123", IsValid = true };
        await _executor.ExecuteAsync(compiled, scan, CancellationToken.None);

        action1Mock.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Once);
        action2Mock.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancellationToken_StopsExecution()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var scenario = CreateLinearScenario();
        var compiled = _executor.Compile(scenario);
        Assert.NotNull(compiled);

        var scan = new ScanResult { ParsedData = "TEST123", IsValid = true };
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _executor.ExecuteAsync(compiled, scan, cts.Token));
    }

    [Fact]
    public void Compile_EmptyGraph_ReturnsNull()
    {
        var scenario = new ScenarioConfig { Name = "Empty", Nodes = new(), Connections = new() };
        var result = _executor.Compile(scenario);
        Assert.Null(result);
    }

    [Fact]
    public void Compile_NoStartNode_ReturnsNull()
    {
        var scenario = new ScenarioConfig
        {
            Name = "NoStart",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "End", PositionX = 0, PositionY = 0 }
            },
            Connections = new()
        };
        var result = _executor.Compile(scenario);
        Assert.Null(result);
    }

    private static ScenarioConfig CreateLinearScenario()
    {
        return new ScenarioConfig
        {
            Name = "Linear",
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
