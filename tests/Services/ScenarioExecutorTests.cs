using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Tests.Services;

public class ScenarioExecutorTests
{
    private readonly Mock<IPostScanActionFactory> _factoryMock = new();
    private readonly Mock<ILogger<ScenarioExecutor>> _loggerMock = new();
    private readonly ScenarioExecutor _executor;

    public ScenarioExecutorTests()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var conditionEvaluator = new ConditionEvaluator(scopeFactoryMock.Object);
        _executor = new ScenarioExecutor(_factoryMock.Object, _loggerMock.Object, conditionEvaluator);
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

    [Fact]
    public void Compile_WithScannerNode_ReturnsSuccess()
    {
        var scenario = CreateScannerScenario();
        var result = _executor.Compile(scenario);
        Assert.NotNull(result);
        Assert.Single(result.ScannerNodes);
        Assert.Equal("Scanner1", result.ScannerNodes[0].ScannerName);
    }

    [Fact]
    public void Compile_WithMultipleScannerNodes_ReturnsAll()
    {
        var scenario = new ScenarioConfig
        {
            Name = "MultiScanner",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Scanner", PositionX = 0, PositionY = 0,
                    Settings = new Dictionary<string, string> { ["scannerName"] = "Scanner1" } },
                new() { NodeId = "2", Type = "Scanner", PositionX = 0, PositionY = 100,
                    Settings = new Dictionary<string, string> { ["scannerName"] = "Scanner2" } },
                new() { NodeId = "3", Type = "Log", PositionX = 200, PositionY = 50 },
                new() { NodeId = "4", Type = "End", PositionX = 400, PositionY = 50 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "3" },
                new() { SourceNodeId = "2", TargetNodeId = "3" },
                new() { SourceNodeId = "3", TargetNodeId = "4" },
            }
        };
        var result = _executor.Compile(scenario);
        Assert.NotNull(result);
        Assert.Equal(2, result.ScannerNodes.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ScannerNodeMatchesScannerName_CallsAction()
    {
        var actionMock = new Mock<IPostScanAction>();
        actionMock.Setup(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(actionMock.Object);

        var scenario = CreateScannerScenario();
        var compiled = _executor.Compile(scenario);
        Assert.NotNull(compiled);

        var scan = new ScanResult { ScannerName = "Scanner1", ParsedData = "test" };
        await _executor.ExecuteAsync(compiled, scan, CancellationToken.None);

        actionMock.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ScannerNodeDoesNotMatch_SkipsAction()
    {
        var actionMock = new Mock<IPostScanAction>();
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(actionMock.Object);

        var scenario = CreateScannerScenario();
        var compiled = _executor.Compile(scenario);

        var scan = new ScanResult { ScannerName = "OtherScanner", ParsedData = "test" };
        await _executor.ExecuteAsync(compiled, scan, CancellationToken.None);

        actionMock.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyScannerName_MatchesAll()
    {
        var actionMock = new Mock<IPostScanAction>();
        actionMock.Setup(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(actionMock.Object);

        var scenario = new ScenarioConfig
        {
            Name = "AllScanners",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Scanner", PositionX = 0, PositionY = 0,
                    Settings = new Dictionary<string, string> { ["scannerName"] = "" } },
                new() { NodeId = "2", Type = "Log", PositionX = 200, PositionY = 0 },
                new() { NodeId = "3", Type = "End", PositionX = 400, PositionY = 0 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "2" },
                new() { SourceNodeId = "2", TargetNodeId = "3" },
            }
        };
        var compiled = _executor.Compile(scenario);

        var scan = new ScanResult { ScannerName = "AnyScanner", ParsedData = "test" };
        await _executor.ExecuteAsync(compiled, scan, CancellationToken.None);

        actionMock.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Compile_StartNodeLegacy_MigratesToScanner()
    {
        var scenario = CreateLinearScenario();
        var result = _executor.Compile(scenario);
        Assert.NotNull(result);
        Assert.Single(result.ScannerNodes);
        Assert.Equal("", result.ScannerNodes[0].ScannerName); // Start -> Scanner("")
    }

    private static ScenarioConfig CreateScannerScenario()
    {
        return new ScenarioConfig
        {
            Name = "ScannerScenario",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Scanner", PositionX = 0, PositionY = 0,
                    Settings = new Dictionary<string, string> { ["scannerName"] = "Scanner1" } },
                new() { NodeId = "2", Type = "Log", PositionX = 200, PositionY = 0 },
                new() { NodeId = "3", Type = "End", PositionX = 400, PositionY = 0 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "2" },
                new() { SourceNodeId = "2", TargetNodeId = "3" },
            }
        };
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

    [Fact]
    public void Compile_WithFromScenarioNode_ReturnsSuccess()
    {
        var scenario = new ScenarioConfig
        {
            Id = 42,
            Name = "FromScenarioTest",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "FromScenario", PositionX = 0, PositionY = 0 },
                new() { NodeId = "2", Type = "Log", PositionX = 200, PositionY = 0 },
                new() { NodeId = "3", Type = "End", PositionX = 400, PositionY = 0 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "2" },
                new() { SourceNodeId = "2", TargetNodeId = "3" },
            }
        };
        var result = _executor.Compile(scenario);
        Assert.NotNull(result);
        Assert.Single(result.FromScenarioNodes);
        Assert.Equal(42, result.FromScenarioNodes[0].ScenarioId);
    }

    [Fact]
    public async Task ExecuteFromScenario_ReceivesTriggerData()
    {
        var actionMock = new Mock<IPostScanAction>();
        actionMock.Setup(a => a.Type).Returns("Log");
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(actionMock.Object);

        var scenario = new ScenarioConfig
        {
            Id = 42,
            Name = "FromScenarioExec",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "FromScenario", PositionX = 0, PositionY = 0 },
                new() { NodeId = "2", Type = "Log", PositionX = 200, PositionY = 0 },
                new() { NodeId = "3", Type = "End", PositionX = 400, PositionY = 0 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "2" },
                new() { SourceNodeId = "2", TargetNodeId = "3" },
            }
        };

        var compiled = _executor.Compile(scenario);
        Assert.NotNull(compiled);

        var scan = new ScanResult
        {
            ParsedData = "FROM_PARENT",
            TriggerType = "Scenario",
            TriggerSource = "42",
            IsValid = true
        };

        await _executor.ExecuteAsync(compiled, scan, CancellationToken.None);

        actionMock.Verify(a => a.ExecuteAsync(
            It.Is<ScanResult>(s => s.ParsedData == "FROM_PARENT"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteWithResultAsync_ReturnsEndNodeScanResult()
    {
        var scenario = new ScenarioConfig
        {
            Id = 99,
            Name = "ChildScenario",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Scanner", PositionX = 0, PositionY = 0,
                    Settings = new Dictionary<string, string> { ["scannerName"] = "" } },
                new() { NodeId = "2", Type = "Log", PositionX = 200, PositionY = 0 },
                new() { NodeId = "3", Type = "End", PositionX = 400, PositionY = 0 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "2" },
                new() { SourceNodeId = "2", TargetNodeId = "3" },
            }
        };

        var compiled = _executor.Compile(scenario);
        Assert.NotNull(compiled);

        var scan = new ScanResult { ParsedData = "CHILD_DATA", IsValid = true };
        var result = await _executor.ExecuteWithResultAsync(compiled, scan, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("CHILD_DATA", result.ParsedData);
    }

    [Fact]
    public async Task ExecuteWithResultAsync_ExceedsDepthLimit_ReturnsNull()
    {
        var scenario = new ScenarioConfig
        {
            Id = 1,
            Name = "Deep",
            Nodes = new List<ScenarioNodeConfig>
            {
                new() { NodeId = "1", Type = "Scanner", PositionX = 0, PositionY = 0,
                    Settings = new Dictionary<string, string> { ["scannerName"] = "" } },
                new() { NodeId = "2", Type = "End", PositionX = 200, PositionY = 0 },
            },
            Connections = new List<ScenarioConnectionConfig>
            {
                new() { SourceNodeId = "1", TargetNodeId = "2" },
            }
        };

        var compiled = _executor.Compile(scenario);
        Assert.NotNull(compiled);

        var scan = new ScanResult { ParsedData = "test", IsValid = true };
        var result = await _executor.ExecuteWithResultAsync(compiled, scan, CancellationToken.None, callDepth: 11);

        Assert.Null(result);
        Assert.True(scan.Metadata.ContainsKey("scenario_error"));
        Assert.Equal("max_depth_exceeded", scan.Metadata["scenario_error"]);
    }
}
