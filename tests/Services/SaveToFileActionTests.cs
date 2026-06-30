using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Tests.Services;

public class ExportActionTests : IDisposable
{
    private readonly string _testDir;

    public ExportActionTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private ExportAction CreateAction(string format = "json", string? template = null)
    {
        var settings = new Dictionary<string, string>
        {
            ["FolderPath"] = _testDir,
            ["Format"] = format
        };
        if (template != null) settings["FilenameTemplate"] = template;
        return new ExportAction(
            new Mock<ILogger<ExportAction>>().Object,
            settings);
    }

    private ScanResult CreateScan(string data = "TEST123", string scanner = "Main") => new()
    {
        RawData = data,
        ParsedData = data,
        Format = "Code128",
        IsValid = true,
        ScannerName = scanner,
        Timestamp = new DateTime(2025, 1, 15, 10, 30, 45, DateTimeKind.Utc)
    };

    [Fact]
    public async Task ExecuteAsync_WritesJsonFile()
    {
        var action = CreateAction("json");
        var scan = CreateScan();

        await action.ExecuteAsync(scan, CancellationToken.None);

        var files = Directory.GetFiles(_testDir, "*.json");
        Assert.Single(files);
        var content = await File.ReadAllTextAsync(files[0]);
        Assert.Contains("\"ParsedData\": \"TEST123\"", content);
        Assert.Contains("\"ScannerName\": \"Main\"", content);
        Assert.Contains("\"IsValid\": true", content);
    }

    [Fact]
    public async Task ExecuteAsync_WritesXmlFile()
    {
        var action = CreateAction("xml");
        var scan = CreateScan();

        await action.ExecuteAsync(scan, CancellationToken.None);

        var files = Directory.GetFiles(_testDir, "*.xml");
        Assert.Single(files);
        var content = await File.ReadAllTextAsync(files[0]);
        Assert.Contains("<ParsedData>TEST123</ParsedData>", content);
        Assert.Contains("<ScannerName>Main</ScannerName>", content);
        Assert.Contains("<IsValid>true</IsValid>", content);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCustomFilenameTemplate()
    {
        var action = CreateAction("json", "{data}_{scanner}");
        var scan = CreateScan();

        await action.ExecuteAsync(scan, CancellationToken.None);

        var files = Directory.GetFiles(_testDir, "*.json");
        Assert.Single(files);
        Assert.Contains("TEST123_Main", Path.GetFileName(files[0]));
    }

    [Fact]
    public async Task ExecuteAsync_MissingFolder_CreatesIt()
    {
        var subDir = Path.Combine(_testDir, "new_subdir");
        var settings = new Dictionary<string, string>
        {
            ["FolderPath"] = subDir,
            ["Format"] = "json"
        };
        var action = new ExportAction(
            new Mock<ILogger<ExportAction>>().Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);

        Assert.True(Directory.Exists(subDir));
        Assert.Single(Directory.GetFiles(subDir, "*.json"));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyFolderPath_DoesNotThrow()
    {
        var settings = new Dictionary<string, string>
        {
            ["FolderPath"] = "",
            ["Format"] = "json"
        };
        var action = new ExportAction(
            new Mock<ILogger<ExportAction>>().Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleScans_CreatesMultipleFiles()
    {
        var action = CreateAction("json", "{timestamp}_{data}");

        await action.ExecuteAsync(CreateScan("AAA"), CancellationToken.None);
        await action.ExecuteAsync(CreateScan("BBB"), CancellationToken.None);

        var files = Directory.GetFiles(_testDir, "*.json");
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public async Task ExecuteAsync_CustomTags_OnlySelectedTagsIncluded()
    {
        var tags = "[{\"Key\":\"Code\",\"Source\":\"ParsedData\"},{\"Key\":\"Status\",\"Source\":\"IsValid\"}]";
        var settings = new Dictionary<string, string>
        {
            ["FolderPath"] = _testDir,
            ["Format"] = "json",
            ["Tags"] = tags
        };
        var action = new ExportAction(
            new Mock<ILogger<ExportAction>>().Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);

        var content = await File.ReadAllTextAsync(Directory.GetFiles(_testDir, "*.json")[0]);
        Assert.Contains("\"Code\": \"TEST123\"", content);
        Assert.Contains("\"Status\": true", content);
        Assert.DoesNotContain("ScannerName", content);
        Assert.DoesNotContain("RawData", content);
    }

    [Fact]
    public async Task ExecuteAsync_CustomTagWithStaticValue()
    {
        var tags = "[{\"Key\":\"Source\",\"Source\":\"Custom\",\"Value\":\"xTrack\"},{\"Key\":\"Data\",\"Source\":\"ParsedData\"}]";
        var settings = new Dictionary<string, string>
        {
            ["FolderPath"] = _testDir,
            ["Format"] = "json",
            ["Tags"] = tags
        };
        var action = new ExportAction(
            new Mock<ILogger<ExportAction>>().Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);

        var content = await File.ReadAllTextAsync(Directory.GetFiles(_testDir, "*.json")[0]);
        Assert.Contains("\"Source\": \"xTrack\"", content);
        Assert.Contains("\"Data\": \"TEST123\"", content);
    }

    [Fact]
    public async Task ExecuteAsync_TagsXml_WritesSelectedTags()
    {
        var tags = "[{\"Key\":\"Barcode\",\"Source\":\"ParsedData\"}]";
        var settings = new Dictionary<string, string>
        {
            ["FolderPath"] = _testDir,
            ["Format"] = "xml",
            ["Tags"] = tags
        };
        var action = new ExportAction(
            new Mock<ILogger<ExportAction>>().Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);

        var content = await File.ReadAllTextAsync(Directory.GetFiles(_testDir, "*.xml")[0]);
        Assert.Contains("<Barcode>TEST123</Barcode>", content);
        Assert.DoesNotContain("ScannerName", content);
    }

    [Fact]
    public async Task ExecuteAsync_NoTags_UsesAllDefault()
    {
        var action = CreateAction("json");
        var scan = CreateScan();

        await action.ExecuteAsync(scan, CancellationToken.None);

        var content = await File.ReadAllTextAsync(Directory.GetFiles(_testDir, "*.json")[0]);
        Assert.Contains("Timestamp", content);
        Assert.Contains("ScannerName", content);
        Assert.Contains("ParsedData", content);
    }
}
