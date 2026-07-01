using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Tests.Services;

public class ReplacementActionTests
{
    private readonly Mock<ILogger<ReplacementAction>> _loggerMock = new();

    private static ScanResult CreateScan(string data = "Hello World", bool isValid = true)
    {
        return new ScanResult
        {
            RawData = data,
            ParsedData = data,
            Format = "EAN-13",
            IsValid = isValid,
            ScannerName = "Scanner1"
        };
    }

    private static Dictionary<string, string> CreateSettings(string replacementsJson)
    {
        return new Dictionary<string, string>
        {
            ["Replacements"] = replacementsJson
        };
    }

    [Fact]
    public async Task ExecuteAsync_ModeAll_ReplacesAllOccurrences()
    {
        var rules = """[{"Find": "l", "Replace": "r", "Mode": "all"}]""";
        var action = new ReplacementAction(_loggerMock.Object, CreateSettings(rules));
        var scan = CreateScan("Hello World");

        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("Herro Worrd", scan.ParsedData);
    }

    [Fact]
    public async Task ExecuteAsync_ModeStart_ReplacesOnlyAtStart()
    {
        var rules = """[{"Find": "Hello", "Replace": "Hi", "Mode": "start"}]""";
        var action = new ReplacementAction(_loggerMock.Object, CreateSettings(rules));
        var scan = CreateScan("Hello World");

        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("Hi World", scan.ParsedData);
    }

    [Fact]
    public async Task ExecuteAsync_ModeEnd_ReplacesOnlyAtEnd()
    {
        var rules = """[{"Find": "World", "Replace": "Earth", "Mode": "end"}]""";
        var action = new ReplacementAction(_loggerMock.Object, CreateSettings(rules));
        var scan = CreateScan("Hello World");

        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("Hello Earth", scan.ParsedData);
    }

    [Fact]
    public async Task ExecuteAsync_ModeStart_NotAtStart_NoChange()
    {
        var rules = """[{"Find": "World", "Replace": "Earth", "Mode": "start"}]""";
        var action = new ReplacementAction(_loggerMock.Object, CreateSettings(rules));
        var scan = CreateScan("Hello World");

        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("Hello World", scan.ParsedData);
    }

    [Fact]
    public async Task ExecuteAsync_ModeEnd_NotAtEnd_NoChange()
    {
        var rules = """[{"Find": "Hello", "Replace": "Hi", "Mode": "end"}]""";
        var action = new ReplacementAction(_loggerMock.Object, CreateSettings(rules));
        var scan = CreateScan("Hello World");

        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("Hello World", scan.ParsedData);
    }

    [Fact]
    public async Task ExecuteAsync_NoMatchingRules_NoChange()
    {
        var rules = """[{"Find": "XYZ", "Replace": "ABC", "Mode": "all"}]""";
        var action = new ReplacementAction(_loggerMock.Object, CreateSettings(rules));
        var scan = CreateScan("Hello World");

        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("Hello World", scan.ParsedData);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleRules_AppliedInOrder()
    {
        var rules = """[{"Find": "Hello", "Replace": "Hi", "Mode": "all"}, {"Find": "World", "Replace": "Earth", "Mode": "all"}]""";
        var action = new ReplacementAction(_loggerMock.Object, CreateSettings(rules));
        var scan = CreateScan("Hello World");

        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("Hi Earth", scan.ParsedData);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyData_NoChange()
    {
        var rules = """[{"Find": "Hello", "Replace": "Hi", "Mode": "all"}]""";
        var action = new ReplacementAction(_loggerMock.Object, CreateSettings(rules));
        var scan = CreateScan("");

        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("", scan.ParsedData);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidScan_SkipsReplacement()
    {
        var rules = """[{"Find": "Hello", "Replace": "Hi", "Mode": "all"}]""";
        var action = new ReplacementAction(_loggerMock.Object, CreateSettings(rules));
        var scan = CreateScan("Hello World", isValid: false);

        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("Hello World", scan.ParsedData);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyReplacements_DoesNothing()
    {
        var action = new ReplacementAction(_loggerMock.Object, CreateSettings("[]"));
        var scan = CreateScan("Hello World");

        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("Hello World", scan.ParsedData);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_DoesNothing()
    {
        var action = new ReplacementAction(_loggerMock.Object, CreateSettings("not json"));
        var scan = CreateScan("Hello World");

        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.Equal("Hello World", scan.ParsedData);
    }
}
