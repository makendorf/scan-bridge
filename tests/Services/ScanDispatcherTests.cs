using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services;
using ScanBridge.Services.PostScanActions;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Tests.Services;

public class ScanDispatcherTests
{
    private readonly Mock<IPostScanActionFactory> _factoryMock = new();
    private readonly Mock<ILogger<ScanDispatcher>> _loggerMock = new();
    private readonly ScanDispatcher _dispatcher;

    public ScanDispatcherTests()
    {
        var logActionMock = new Mock<IPostScanAction>();
        logActionMock.Setup(a => a.Type).Returns("Log");
        logActionMock.Setup(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(logActionMock.Object);

        var replacementMock = new Mock<IPostScanAction>();
        replacementMock.Setup(a => a.Type).Returns("Replacement");
        _factoryMock.Setup(f => f.Create("Replacement", It.IsAny<Dictionary<string, string>>()))
            .Returns(replacementMock.Object);

        var exportMock = new Mock<IPostScanAction>();
        exportMock.Setup(a => a.Type).Returns("Export");
        _factoryMock.Setup(f => f.Create("Export", It.IsAny<Dictionary<string, string>>()))
            .Returns(exportMock.Object);

        _dispatcher = new ScanDispatcher(_factoryMock.Object, _loggerMock.Object);
    }

    private static ScanResult CreateScan(string scannerName = "Scanner1", bool isValid = true)
    {
        return new ScanResult
        {
            RawData = "12345", ParsedData = "12345", Format = "EAN-13",
            IsValid = isValid, ScannerName = scannerName, Timestamp = DateTime.UtcNow
        };
    }

    private static PostScanActionGroupConfig CreateGroup(
        string name = "TestGroup", bool enabled = true,
        List<string>? scannerNames = null, List<PostScanActionConfig>? actions = null)
    {
        return new PostScanActionGroupConfig
        {
            Id = 1, Name = name, Enabled = enabled,
            ScannerNames = scannerNames ?? new(),
            Actions = actions ?? new()
        };
    }

    private static PostScanActionConfig CreateActionConfig(
        string type = "Log", bool enabled = true, Dictionary<string, string>? settings = null)
    {
        return new PostScanActionConfig { Type = type, Enabled = enabled, Settings = settings };
    }

    [Fact]
    public void ConfigureGroups_EmptyList_NoGroupsLoaded()
    {
        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>());
        Assert.Empty(_dispatcher.GetEnabledActions());
    }

    [Fact]
    public void ConfigureGroups_GroupsLoaded_CorrectCount()
    {
        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>
        {
            CreateGroup("G1", actions: new() { CreateActionConfig("Log") }),
            CreateGroup("G2", actions: new() { CreateActionConfig("Log") })
        });
        Assert.Equal(2, _dispatcher.GetEnabledActions().Count);
    }

    [Fact]
    public void ConfigureGroups_DisabledGroup_Skipped()
    {
        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>
        {
            CreateGroup("Disabled", enabled: false, actions: new() { CreateActionConfig("Log") })
        });
        Assert.Empty(_dispatcher.GetEnabledActions());
    }

    [Fact]
    public void ConfigureGroups_DisabledAction_Filtered()
    {
        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>
        {
            CreateGroup("G1", actions: new()
            {
                CreateActionConfig("Log", enabled: true),
                CreateActionConfig("Replacement", enabled: false)
            })
        });
        var actions = _dispatcher.GetEnabledActions();
        Assert.Single(actions);
        Assert.Equal("Log", actions[0]);
    }

    [Fact]
    public void ConfigureGroups_UnknownActionType_Skipped()
    {
        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>
        {
            CreateGroup("G1", actions: new() { CreateActionConfig("UnknownType") })
        });
        Assert.Empty(_dispatcher.GetEnabledActions());
    }

    [Fact]
    public async Task ExecuteAllAsync_NoGroups_DoesNothing()
    {
        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>());
        await _dispatcher.ExecuteAllAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAllAsync_MatchingGroup_CallsActions()
    {
        var actionMock = new Mock<IPostScanAction>();
        actionMock.Setup(a => a.Type).Returns("Log");
        actionMock.Setup(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(actionMock.Object);

        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>
        {
            CreateGroup("G1", actions: new() { CreateActionConfig("Log") })
        });

        await _dispatcher.ExecuteAllAsync(CreateScan(), CancellationToken.None);
        actionMock.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAllAsync_NonMatchingGroup_Skipped()
    {
        var actionMock = new Mock<IPostScanAction>();
        actionMock.Setup(a => a.Type).Returns("Log");
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(actionMock.Object);

        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>
        {
            CreateGroup("G1", scannerNames: new() { "OtherScanner" }, actions: new() { CreateActionConfig("Log") })
        });

        await _dispatcher.ExecuteAllAsync(CreateScan("Scanner1"), CancellationToken.None);
        actionMock.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAllAsync_ActionThrows_ContinuesOtherGroups()
    {
        var throwingAction = new Mock<IPostScanAction>();
        throwingAction.Setup(a => a.Type).Returns("Throwing");
        throwingAction.Setup(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test"));

        var normalAction = new Mock<IPostScanAction>();
        normalAction.Setup(a => a.Type).Returns("Log");
        normalAction.Setup(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _factoryMock.Setup(f => f.Create("Throwing", It.IsAny<Dictionary<string, string>>()))
            .Returns(throwingAction.Object);
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(normalAction.Object);

        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>
        {
            CreateGroup("G1", actions: new() { CreateActionConfig("Throwing") }),
            CreateGroup("G2", actions: new() { CreateActionConfig("Log") })
        });

        await _dispatcher.ExecuteAllAsync(CreateScan(), CancellationToken.None);
        throwingAction.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Once);
        normalAction.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAllAsync_Cancellation_StopsExecution()
    {
        var actionMock = new Mock<IPostScanAction>();
        actionMock.Setup(a => a.Type).Returns("Log");
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(actionMock.Object);

        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>
        {
            CreateGroup("G1", actions: new() { CreateActionConfig("Log") })
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _dispatcher.ExecuteAllAsync(CreateScan(), cts.Token));
    }

    [Fact]
    public void GetEnabledActions_ReturnsActionTypes()
    {
        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>
        {
            CreateGroup("G1", actions: new()
            {
                CreateActionConfig("Log"), CreateActionConfig("Replacement"), CreateActionConfig("Export")
            })
        });
        var actions = _dispatcher.GetEnabledActions();
        Assert.Equal(3, actions.Count);
        Assert.Contains("Log", actions);
    }

    [Fact]
    public void ConfigureScenarios_EmptyList_NoScenarios()
    {
        var executor = new ScenarioExecutor(Mock.Of<IPostScanActionFactory>(), Mock.Of<ILogger<ScenarioExecutor>>());
        _dispatcher.ConfigureScenarios(new List<ScenarioConfig>(), executor);
        Assert.Equal(0, _dispatcher.GetScenarioCount());
    }

    [Fact]
    public async Task MatchesScanner_EmptyList_MatchesAll()
    {
        var logActionMock = new Mock<IPostScanAction>();
        logActionMock.Setup(a => a.Type).Returns("Log");
        logActionMock.Setup(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(logActionMock.Object);

        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>
        {
            CreateGroup("G1", scannerNames: new(), actions: new() { CreateActionConfig("Log") })
        });

        await _dispatcher.ExecuteAllAsync(CreateScan("AnyScanner"), CancellationToken.None);
        logActionMock.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MatchesScanner_CaseInsensitive_NoMatch()
    {
        var logActionMock = new Mock<IPostScanAction>();
        logActionMock.Setup(a => a.Type).Returns("Log");
        _factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(logActionMock.Object);

        _dispatcher.ConfigureGroups(new List<PostScanActionGroupConfig>
        {
            CreateGroup("G1", scannerNames: new() { "scanner1" }, actions: new() { CreateActionConfig("Log") })
        });

        await _dispatcher.ExecuteAllAsync(CreateScan("Scanner1"), CancellationToken.None);
        logActionMock.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
