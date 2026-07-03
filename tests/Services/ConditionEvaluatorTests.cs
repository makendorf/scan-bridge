using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Tests.Services;

public class ConditionEvaluatorTests
{
    private readonly ConditionEvaluator _evaluator;

    public ConditionEvaluatorTests()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _evaluator = new ConditionEvaluator(scopeFactoryMock.Object);
    }

    [Fact]
    public void Equals_MatchingValue_ReturnsTrue()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "equals",
            ["field"] = "data",
            ["value"] = "hello"
        };
        var scan = new ScanResult { ParsedData = "hello" };

        Assert.True(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void Equals_NonMatchingValue_ReturnsFalse()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "equals",
            ["field"] = "data",
            ["value"] = "hello"
        };
        var scan = new ScanResult { ParsedData = "world" };

        Assert.False(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void Contains_MatchingSubstring_ReturnsTrue()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "contains",
            ["field"] = "data",
            ["value"] = "ell"
        };
        var scan = new ScanResult { ParsedData = "hello" };

        Assert.True(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void Contains_NonMatchingSubstring_ReturnsFalse()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "contains",
            ["field"] = "data",
            ["value"] = "xyz"
        };
        var scan = new ScanResult { ParsedData = "hello" };

        Assert.False(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void Regex_MatchingPattern_ReturnsTrue()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "regex",
            ["field"] = "data",
            ["value"] = @"^\d{3}$"
        };
        var scan = new ScanResult { ParsedData = "123" };

        Assert.True(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void Regex_NonMatchingPattern_ReturnsFalse()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "regex",
            ["field"] = "data",
            ["value"] = @"^\d{3}$"
        };
        var scan = new ScanResult { ParsedData = "abc" };

        Assert.False(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void IsValid_Check_ReturnsTrue()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "isValid",
            ["field"] = "data"
        };
        var scan = new ScanResult { IsValid = true };

        Assert.True(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void IsValid_Check_ReturnsFalse()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "isValid",
            ["field"] = "data"
        };
        var scan = new ScanResult { IsValid = false };

        Assert.False(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void InvalidExpression_ReturnsFalse()
    {
        var settings = new Dictionary<string, string>();
        var scan = new ScanResult { ParsedData = "test" };

        Assert.False(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void NotEquals_DifferentValue_ReturnsTrue()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "notEquals",
            ["field"] = "data",
            ["value"] = "hello"
        };
        var scan = new ScanResult { ParsedData = "world" };

        Assert.True(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void NotContains_NoSubstring_ReturnsTrue()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "notContains",
            ["field"] = "data",
            ["value"] = "xyz"
        };
        var scan = new ScanResult { ParsedData = "hello" };

        Assert.True(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void GreaterThan_LargerValue_ReturnsTrue()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "greaterThan",
            ["field"] = "data",
            ["value"] = "10"
        };
        var scan = new ScanResult { ParsedData = "20" };

        Assert.True(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void LessThan_SmallerValue_ReturnsTrue()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "lessThan",
            ["field"] = "data",
            ["value"] = "10"
        };
        var scan = new ScanResult { ParsedData = "5" };

        Assert.True(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void Field_Raw_UsesRawData()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "equals",
            ["field"] = "raw",
            ["value"] = "RAW123"
        };
        var scan = new ScanResult { RawData = "RAW123", ParsedData = "PARSED" };

        Assert.True(_evaluator.Evaluate(settings, scan));
    }

    [Fact]
    public void Field_Scanner_UsesScannerName()
    {
        var settings = new Dictionary<string, string>
        {
            ["operator"] = "equals",
            ["field"] = "scanner",
            ["value"] = "Scanner1"
        };
        var scan = new ScanResult { ScannerName = "Scanner1" };

        Assert.True(_evaluator.Evaluate(settings, scan));
    }
}
