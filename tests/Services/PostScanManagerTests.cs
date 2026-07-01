using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services;
using ScanBridge.Services.PostScanActions;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Tests.Services;

/// <summary>
/// Tests for PostScanManager as a thin facade over ScanDispatcher.
/// Uses a real ScanDispatcher with mocked dependencies.
/// </summary>
public class PostScanManagerTests
{
    private readonly PostScanManager _manager;

    public PostScanManagerTests()
    {
        var factoryMock = new Mock<IPostScanActionFactory>();
        var dispatcher = new ScanDispatcher(factoryMock.Object, Mock.Of<ILogger<ScanDispatcher>>());
        _manager = new PostScanManager(dispatcher);
    }

    [Fact]
    public void Configure_EmptyGroups_NoActions()
    {
        _manager.Configure(new List<PostScanActionGroupConfig>());
        Assert.Empty(_manager.GetEnabledActions());
    }

    [Fact]
    public void GetEnabledActions_WithGroups_ReturnsTypes()
    {
        var factoryMock = new Mock<IPostScanActionFactory>();
        var actionMock = new Mock<IPostScanAction>();
        actionMock.Setup(a => a.Type).Returns("Log");
        factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(actionMock.Object);

        var dispatcher = new ScanDispatcher(factoryMock.Object, Mock.Of<ILogger<ScanDispatcher>>());
        var manager = new PostScanManager(dispatcher);

        manager.Configure(new List<PostScanActionGroupConfig>
        {
            new() { Name = "G1", Enabled = true, Actions = new()
            {
                new PostScanActionConfig { Type = "Log", Enabled = true }
            }}
        });

        var actions = manager.GetEnabledActions();
        Assert.Single(actions);
        Assert.Equal("Log", actions[0]);
    }

    [Fact]
    public void GetScenarioCount_NoScenarios_ReturnsZero()
    {
        Assert.Equal(0, _manager.GetScenarioCount());
    }

    [Fact]
    public void ConfigureScenarios_EmptyList_NoScenarios()
    {
        var executor = new ScenarioExecutor(Mock.Of<IPostScanActionFactory>(), Mock.Of<ILogger<ScenarioExecutor>>());
        _manager.ConfigureScenarios(new List<ScenarioConfig>(), executor);
        Assert.Equal(0, _manager.GetScenarioCount());
    }

    [Fact]
    public async Task ExecuteAllAsync_DelegatesToDispatcher()
    {
        var factoryMock = new Mock<IPostScanActionFactory>();
        var actionMock = new Mock<IPostScanAction>();
        actionMock.Setup(a => a.Type).Returns("Log");
        actionMock.Setup(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        factoryMock.Setup(f => f.Create("Log", It.IsAny<Dictionary<string, string>>()))
            .Returns(actionMock.Object);

        var dispatcher = new ScanDispatcher(factoryMock.Object, Mock.Of<ILogger<ScanDispatcher>>());
        var manager = new PostScanManager(dispatcher);

        manager.Configure(new List<PostScanActionGroupConfig>
        {
            new() { Name = "G1", Enabled = true, Actions = new()
            {
                new PostScanActionConfig { Type = "Log", Enabled = true }
            }}
        });

        var scan = new ScanResult { RawData = "123", ParsedData = "123", Format = "EAN-13", IsValid = true, ScannerName = "Scanner1" };
        await manager.ExecuteAllAsync(scan, CancellationToken.None);

        actionMock.Verify(a => a.ExecuteAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
