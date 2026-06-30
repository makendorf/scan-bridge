using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Tests.Services;

public class ValidationActionTests
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
    public async Task Regex_ValidMatch_Passes()
    {
        var action = new ValidationAction(
            new Mock<ILogger<ValidationAction>>().Object,
            new Dictionary<string, string> { ["Pattern"] = "^[A-Z0-9]+$" });

        var scan = CreateScan("ABC123");
        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.True(scan.IsValid);
    }

    [Fact]
    public async Task Regex_InvalidMatch_SetsIsValidFalse()
    {
        var action = new ValidationAction(
            new Mock<ILogger<ValidationAction>>().Object,
            new Dictionary<string, string> { ["Pattern"] = "^[A-Z0-9]+$" });

        var scan = CreateScan("abc-123");
        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.False(scan.IsValid);
    }

    [Fact]
    public async Task Regex_MinLength_Enforced()
    {
        var action = new ValidationAction(
            new Mock<ILogger<ValidationAction>>().Object,
            new Dictionary<string, string>
            {
                ["Pattern"] = ".*",
                ["MinLength"] = "5"
            });

        var scan = CreateScan("AB");
        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.False(scan.IsValid);
    }

    [Fact]
    public async Task Regex_MaxLength_Enforced()
    {
        var action = new ValidationAction(
            new Mock<ILogger<ValidationAction>>().Object,
            new Dictionary<string, string>
            {
                ["Pattern"] = ".*",
                ["MaxLength"] = "3"
            });

        var scan = CreateScan("ABCDEF");
        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.False(scan.IsValid);
    }

    [Fact]
    public async Task Dictionary_KnownValue_Passes()
    {
        var tmpFile = Path.GetTempFileName();
        File.WriteAllLines(tmpFile, ["AAA", "BBB", "CCC"]);
        try
        {
            var action = new ValidationAction(
                new Mock<ILogger<ValidationAction>>().Object,
                new Dictionary<string, string>
                {
                    ["ValidationType"] = "dictionary",
                    ["DictionaryPath"] = tmpFile
                });

            var scan = CreateScan("BBB");
            await action.ExecuteAsync(scan, CancellationToken.None);

            Assert.True(scan.IsValid);
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public async Task Dictionary_UnknownValue_Fails()
    {
        var tmpFile = Path.GetTempFileName();
        File.WriteAllLines(tmpFile, ["AAA", "BBB", "CCC"]);
        try
        {
            var action = new ValidationAction(
                new Mock<ILogger<ValidationAction>>().Object,
                new Dictionary<string, string>
                {
                    ["ValidationType"] = "dictionary",
                    ["DictionaryPath"] = tmpFile
                });

            var scan = CreateScan("XXX");
            await action.ExecuteAsync(scan, CancellationToken.None);

            Assert.False(scan.IsValid);
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public async Task Range_WithinBounds_Passes()
    {
        var action = new ValidationAction(
            new Mock<ILogger<ValidationAction>>().Object,
            new Dictionary<string, string>
            {
                ["ValidationType"] = "range",
                ["MinValue"] = "1",
                ["MaxValue"] = "100"
            });

        var scan = CreateScan("50");
        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.True(scan.IsValid);
    }

    [Fact]
    public async Task Range_OutsideBounds_Fails()
    {
        var action = new ValidationAction(
            new Mock<ILogger<ValidationAction>>().Object,
            new Dictionary<string, string>
            {
                ["ValidationType"] = "range",
                ["MinValue"] = "1",
                ["MaxValue"] = "100"
            });

        var scan = CreateScan("150");
        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.False(scan.IsValid);
    }

    [Fact]
    public async Task OnFailure_Warn_DoesNotModifyIsValid()
    {
        var action = new ValidationAction(
            new Mock<ILogger<ValidationAction>>().Object,
            new Dictionary<string, string>
            {
                ["Pattern"] = "^[A-Z]+$",
                ["OnFailure"] = "warn"
            });

        var scan = CreateScan("123");
        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.True(scan.IsValid);
    }

    [Fact]
    public async Task OnFailure_Skip_SetsIsValidFalse()
    {
        var action = new ValidationAction(
            new Mock<ILogger<ValidationAction>>().Object,
            new Dictionary<string, string>
            {
                ["Pattern"] = "^[A-Z]+$",
                ["OnFailure"] = "skip"
            });

        var scan = CreateScan("123");
        await action.ExecuteAsync(scan, CancellationToken.None);

        Assert.False(scan.IsValid);
    }

    [Fact]
    public void Constructor_InvalidRegex_DoesNotThrow()
    {
        var action = new ValidationAction(
            new Mock<ILogger<ValidationAction>>().Object,
            new Dictionary<string, string> { ["Pattern"] = "[invalid" });

        Assert.NotNull(action);
    }
}
