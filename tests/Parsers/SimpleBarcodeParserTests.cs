using ScanBridge.Parsers;

namespace ScanBridge.Tests.Parsers;

public class SimpleBarcodeParserTests
{
    private readonly SimpleBarcodeParser _parser = new();

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Parse_EmptyOrWhitespace_ReturnsInvalid(string? input)
    {
        var result = _parser.Parse(input ?? string.Empty);

        Assert.False(result.IsValid);
        Assert.Equal("Empty", result.Format);
    }

    [Theory]
    [InlineData("12345678", "EAN-8")]
    [InlineData("123456789012", "UPC-A")]
    [InlineData("1234567890123", "EAN-13")]
    [InlineData("12345678901234", "GTIN-14")]
    [InlineData("1234567890", "Numeric")]
    [InlineData("12345", "Numeric")]
    public void Parse_AllDigits_ReturnsValidWithCorrectFormat(string input, string expectedFormat)
    {
        var result = _parser.Parse(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedFormat, result.Format);
        Assert.Equal(input, result.ParsedData);
    }

    [Theory]
    [InlineData("CB19H9FZ884784Y")]
    [InlineData("ABC123DEF456")]
    [InlineData("GS1128CODEHERE")]
    public void Parse_Alphanumeric_ReturnsValid(string input)
    {
        var result = _parser.Parse(input);

        Assert.True(result.IsValid);
        Assert.Equal(input, result.ParsedData);
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("X9")]
    public void Parse_ShortAlphanumeric_ReturnsInvalid(string input)
    {
        var result = _parser.Parse(input);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("test!@#")]
    public void Parse_NonAlphanumeric_ReturnsInvalid(string input)
    {
        var result = _parser.Parse(input);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Parse_Alphanumeric22Chars_DetectsGS1_128()
    {
        var input = "1A2B3C4D5E6F7G8H9I0J1K";

        var result = _parser.Parse(input);

        Assert.True(result.IsValid);
        Assert.Equal("GS1-128", result.Format);
    }

    [Fact]
    public void Parse_AlphanumericUpTo64Chars_DetectsCode128()
    {
        var input = new string('A', 30);

        var result = _parser.Parse(input);

        Assert.True(result.IsValid);
        Assert.Equal("Code128", result.Format);
    }

    [Fact]
    public void Parse_PreservesRawData()
    {
        var input = "  12345678  ";

        var result = _parser.Parse(input);

        Assert.Equal(input, result.RawData);
        Assert.Equal("12345678", result.ParsedData);
    }

    [Fact]
    public void Parse_ReturnsTimestamp()
    {
        var before = DateTime.UtcNow;

        var result = _parser.Parse("12345678");

        var after = DateTime.UtcNow;
        Assert.InRange(result.Timestamp, before, after);
    }

    [Theory]
    [InlineData("123456789012345", "Numeric")]
    [InlineData("123456789012345678901234567890", "Numeric")]
    public void Parse_LongNumeric_ReturnsValid(string input, string expectedFormat)
    {
        var result = _parser.Parse(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedFormat, result.Format);
    }

    [Theory]
    [InlineData("ABCDEF")]
    [InlineData("ABCDEF1")]
    public void Parse_Alphanumeric4To64Chars_ReturnsCode128(string input)
    {
        var result = _parser.Parse(input);

        Assert.True(result.IsValid);
        Assert.Equal("Code128", result.Format);
    }

    [Fact]
    public void Parse_NumericOver64Chars_ReturnsValidNumeric()
    {
        var input = "1234567890123456789012345678901234567890123456789012345678901234";

        var result = _parser.Parse(input);

        Assert.True(result.IsValid);
        Assert.Equal("Numeric", result.Format);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsInvalid()
    {
        var result = _parser.Parse("   ");

        Assert.False(result.IsValid);
        Assert.Equal("Empty", result.Format);
    }

    [Fact]
    public void Parse_TabsOnly_ReturnsInvalid()
    {
        var result = _parser.Parse("\t\t");

        Assert.False(result.IsValid);
        Assert.Equal("Empty", result.Format);
    }

    [Fact]
    public void Parse_NewlinesOnly_ReturnsInvalid()
    {
        var result = _parser.Parse("\n\n");

        Assert.False(result.IsValid);
        Assert.Equal("Empty", result.Format);
    }

    [Fact]
    public void Parse_SingleDigit_ReturnsValidNumeric()
    {
        var result = _parser.Parse("5");

        Assert.True(result.IsValid);
        Assert.Equal("Numeric", result.Format);
    }

    [Fact]
    public void Parse_7Digits_ReturnsValidNumeric()
    {
        var result = _parser.Parse("1234567");

        Assert.True(result.IsValid);
        Assert.Equal("Numeric", result.Format);
    }

    [Fact]
    public void Parse_11Digits_ReturnsValidNumeric()
    {
        var result = _parser.Parse("12345678901");

        Assert.True(result.IsValid);
        Assert.Equal("Numeric", result.Format);
    }

    [Fact]
    public void Parse_3AlphanumericChars_ReturnsInvalid()
    {
        var result = _parser.Parse("ABC");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Parse_4AlphanumericChars_ReturnsValid()
    {
        var result = _parser.Parse("ABCD");

        Assert.True(result.IsValid);
        Assert.Equal("Code128", result.Format);
    }

    [Fact]
    public void Parse_MixedDigitsAndLetters_Long_ReturnsCode128()
    {
        var result = _parser.Parse("ABC123DEF456GHI789");

        Assert.True(result.IsValid);
        Assert.Equal("Code128", result.Format);
    }

    [Fact]
    public void Parse_SpecialChars_ReturnsInvalid()
    {
        var result = _parser.Parse("Hello World!");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Parse_Unicode_ReturnsValid()
    {
        var result = _parser.Parse("Привет");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Parse_23Chars_ReturnsCode128()
    {
        var input = new string('A', 23);

        var result = _parser.Parse(input);

        Assert.Equal("Code128", result.Format);
    }

    [Fact]
    public void Parse_65Chars_ReturnsUnknownFormat()
    {
        var input = "A" + new string('B', 64);

        var result = _parser.Parse(input);

        Assert.Equal("Unknown", result.Format);
    }

    [Theory]
    [InlineData("123e4567-e89b-12d3-a456-426655448888")]
    [InlineData("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
    public void Parse_ValidUuid_ReturnsValidUuidFormat(string input)
    {
        var result = _parser.Parse(input);

        Assert.True(result.IsValid);
        Assert.Equal("UUID", result.Format);
        Assert.Equal(input, result.ParsedData);
    }

    [Theory]
    [InlineData("123e4567-e89b-12d3-a456-42665544888")]
    [InlineData("123e4567-e89b-12d3-a456-4266554488888")]
    [InlineData("123e4567e89b-12d3-a456-426655448888")]
    [InlineData("123e4567-e89b-12d3-a456")]
    [InlineData("123e4567-e89b-12d3-a456-42665544888g")]
    public void Parse_InvalidUuid_ReturnsInvalid(string input)
    {
        var result = _parser.Parse(input);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Parse_UuidWithUpperCase_ReturnsValid()
    {
        var result = _parser.Parse("123E4567-E89B-12D3-A456-426655448888");

        Assert.True(result.IsValid);
        Assert.Equal("UUID", result.Format);
    }

    [Fact]
    public void Parse_UuidWithWhitespace_TrimsAndParses()
    {
        var result = _parser.Parse("  123e4567-e89b-12d3-a456-426655448888  ");

        Assert.True(result.IsValid);
        Assert.Equal("UUID", result.Format);
        Assert.Equal("123e4567-e89b-12d3-a456-426655448888", result.ParsedData);
    }

    [Fact]
    public void Parse_Uuid_PreservesRawData()
    {
        var input = "  123e4567-e89b-12d3-a456-426655448888  ";

        var result = _parser.Parse(input);

        Assert.Equal(input, result.RawData);
    }

    [Theory]
    [InlineData("https://example.com", "Url")]
    [InlineData("http://localhost:5000/api", "Url")]
    public void Parse_Url_DetectsUrlContentType(string input, string expectedContentType)
    {
        var result = _parser.Parse(input);

        Assert.Equal(expectedContentType, result.ContentType);
        Assert.Equal(input, result.ParsedContent);
    }

    [Fact]
    public void Parse_Json_DetectsJsonContentType()
    {
        var input = """{"name":"test","value":123}""";

        var result = _parser.Parse(input);

        Assert.Equal("Json", result.ContentType);
        Assert.NotNull(result.ParsedContent);
        Assert.Contains("name", result.ParsedContent);
    }

    [Fact]
    public void Parse_JsonArray_DetectsJsonContentType()
    {
        var input = """[1,2,3]""";

        var result = _parser.Parse(input);

        Assert.Equal("Json", result.ContentType);
    }

    [Fact]
    public void Parse_VCard_DetectsVCardContentType()
    {
        var input = "BEGIN:VCARD\nVERSION:3.0\nFN:John Doe\nEND:VCARD";

        var result = _parser.Parse(input);

        Assert.Equal("VCard", result.ContentType);
        Assert.Equal(input, result.ParsedContent);
    }

    [Fact]
    public void Parse_Wifi_DetectsWifiContentType()
    {
        var input = "WIFI:T:WPA;S:MyNetwork;P:password123;;";

        var result = _parser.Parse(input);

        Assert.Equal("Wifi", result.ContentType);
        Assert.Contains("MyNetwork", result.ParsedContent);
        Assert.Contains("password123", result.ParsedContent);
    }

    [Fact]
    public void Parse_PlainText_DetectsTextContentType()
    {
        var result = _parser.Parse("Hello World");

        Assert.Equal("Text", result.ContentType);
    }

    [Fact]
    public void Parse_Url_SetsFormatToQR()
    {
        var result = _parser.Parse("https://example.com");

        Assert.Equal("QR", result.Format);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Parse_Json_SetsFormatToQR()
    {
        var result = _parser.Parse("""{"key":"value"}""");

        Assert.Equal("QR", result.Format);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Parse_EAN13_KeepsBarcodeFormat()
    {
        var result = _parser.Parse("1234567890123");

        Assert.Equal("EAN-13", result.Format);
        Assert.Equal("Text", result.ContentType);
    }
}
