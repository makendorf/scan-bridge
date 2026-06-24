using ScanBridge.Models;

namespace ScanBridge.Tests.Models;

public class ScanResultTests
{
    [Fact]
    public void DefaultValues_RawDataIsEmpty()
    {
        var result = new ScanResult();
        Assert.Equal(string.Empty, result.RawData);
    }

    [Fact]
    public void DefaultValues_ParsedDataIsEmpty()
    {
        var result = new ScanResult();
        Assert.Equal(string.Empty, result.ParsedData);
    }

    [Fact]
    public void DefaultValues_FormatIsUnknown()
    {
        var result = new ScanResult();
        Assert.Equal("Unknown", result.Format);
    }

    [Fact]
    public void DefaultValues_IsValidIsFalse()
    {
        var result = new ScanResult();
        Assert.False(result.IsValid);
    }

    [Fact]
    public void DefaultValues_TimestampIsUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var result = new ScanResult();
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(result.Timestamp, before, after);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var result = new ScanResult
        {
            RawData = "raw",
            ParsedData = "parsed",
            Format = "EAN-13",
            IsValid = true,
            Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        Assert.Equal("raw", result.RawData);
        Assert.Equal("parsed", result.ParsedData);
        Assert.Equal("EAN-13", result.Format);
        Assert.True(result.IsValid);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), result.Timestamp);
    }
}
