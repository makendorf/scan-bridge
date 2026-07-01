using ScanBridge.Models;

namespace ScanBridge.Tests.Models;

public class PostScanActionConfigTests
{
    [Fact]
    public void Defaults_CorrectValues()
    {
        var config = new PostScanActionConfig();

        Assert.Equal(string.Empty, config.Type);
        Assert.False(config.Enabled);
        Assert.Null(config.Settings);
    }

    [Fact]
    public void Settings_WithValues_Deserialized()
    {
        var config = new PostScanActionConfig
        {
            Type = "Log",
            Enabled = true,
            Settings = new Dictionary<string, string>
            {
                ["Key1"] = "Value1",
                ["Key2"] = "Value2"
            }
        };

        Assert.Equal("Log", config.Type);
        Assert.True(config.Enabled);
        Assert.NotNull(config.Settings);
        Assert.Equal(2, config.Settings!.Count);
        Assert.Equal("Value1", config.Settings["Key1"]);
    }

    [Fact]
    public void Settings_EmptyDictionary_DefaultsEmpty()
    {
        var config = new PostScanActionConfig
        {
            Settings = new Dictionary<string, string>()
        };

        Assert.NotNull(config.Settings);
        Assert.Empty(config.Settings);
    }
}
