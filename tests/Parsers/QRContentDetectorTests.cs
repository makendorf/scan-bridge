using ScanBridge.Parsers;

namespace ScanBridge.Tests.Parsers;

public class QRContentDetectorTests
{
    private readonly QRContentDetector _detector = new();

    [Fact]
    public void Detect_Empty_ReturnsText()
    {
        var (type, parsed) = _detector.Detect("");
        Assert.Equal("Text", type);
        Assert.Null(parsed);
    }

    [Fact]
    public void Detect_Null_ReturnsText()
    {
        var (type, parsed) = _detector.Detect(null!);
        Assert.Equal("Text", type);
        Assert.Null(parsed);
    }

    [Fact]
    public void Detect_Whitespace_ReturnsText()
    {
        var (type, parsed) = _detector.Detect("   ");
        Assert.Equal("Text", type);
        Assert.Null(parsed);
    }

    [Fact]
    public void Detect_Url_ReturnsUrl()
    {
        var (type, parsed) = _detector.Detect("https://example.com");
        Assert.Equal("Url", type);
        Assert.Equal("https://example.com", parsed);
    }

    [Fact]
    public void Detect_UrlHttp_ReturnsUrl()
    {
        var (type, parsed) = _detector.Detect("http://example.com/path?q=1");
        Assert.Equal("Url", type);
        Assert.Equal("http://example.com/path?q=1", parsed);
    }

    [Fact]
    public void Detect_Json_ReturnsJson()
    {
        var (type, parsed) = _detector.Detect("{\"key\":\"value\"}");
        Assert.Equal("Json", type);
        Assert.NotNull(parsed);
        Assert.Contains("key", parsed);
        Assert.Contains("value", parsed);
    }

    [Fact]
    public void Detect_JsonArray_ReturnsJson()
    {
        var (type, parsed) = _detector.Detect("[1,2,3]");
        Assert.Equal("Json", type);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void Detect_InvalidJson_ReturnsText()
    {
        var (type, parsed) = _detector.Detect("{not valid json}");
        Assert.Equal("Text", type);
    }

    [Fact]
    public void Detect_VCard_ReturnsVCard()
    {
        var vcard = "BEGIN:VCARD\nVERSION:3.0\nFN:John Doe\nEND:VCARD";
        var (type, parsed) = _detector.Detect(vcard);
        Assert.Equal("VCard", type);
        Assert.Equal(vcard, parsed);
    }

    [Fact]
    public void Detect_VCardCaseInsensitive_ReturnsVCard()
    {
        var (type, _) = _detector.Detect("begin:vcard\nversion:3.0\nfn:John\nend:vcard");
        Assert.Equal("VCard", type);
    }

    [Fact]
    public void Detect_Wifi_ReturnsWifi()
    {
        var (type, parsed) = _detector.Detect("WIFI:T:WPA;S:MyNetwork;P:password123;;");
        Assert.Equal("Wifi", type);
        Assert.Contains("MyNetwork", parsed!);
        Assert.Contains("WPA", parsed!);
        Assert.Contains("password123", parsed!);
    }

    [Fact]
    public void Detect_WifiWithHidden_ReturnsWifi()
    {
        var (type, parsed) = _detector.Detect("WIFI:T:WPA2;S:HiddenNet;P:pass;H:true;;");
        Assert.Equal("Wifi", type);
        Assert.Contains("HiddenNet", parsed!);
        Assert.Contains("Hidden: true", parsed!);
    }

    [Fact]
    public void Detect_PlainText_ReturnsText()
    {
        var (type, parsed) = _detector.Detect("Hello World 12345");
        Assert.Equal("Text", type);
        Assert.Equal("Hello World 12345", parsed);
    }

    [Fact]
    public void Detect_Barcode_ReturnsText()
    {
        var (type, parsed) = _detector.Detect("1234567890128");
        Assert.Equal("Text", type);
        Assert.Equal("1234567890128", parsed);
    }
}
