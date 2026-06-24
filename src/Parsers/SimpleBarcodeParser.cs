using System.Text.RegularExpressions;
using ScanBridge.Models;

namespace ScanBridge.Parsers;

public partial class SimpleBarcodeParser : IBarcodeParser
{
    private readonly QRContentDetector _qrDetector = new();

    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex UuidRegex();

    public ScanResult Parse(string rawData)
    {
        var trimmed = rawData.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return new ScanResult
            {
                RawData = rawData,
                ParsedData = string.Empty,
                Format = "Empty",
                IsValid = false
            };
        }

        var isUuid = UuidRegex().IsMatch(trimmed);
        var isAllDigits = trimmed.All(char.IsDigit);
        var isAlphanumeric = trimmed.All(c => char.IsLetterOrDigit(c));
        var format = DetectFormat(trimmed);

        var (contentType, parsedContent) = _qrDetector.Detect(trimmed);

        var isBarcode = isUuid || isAllDigits || (isAlphanumeric && trimmed.Length >= 4);

        if (!isBarcode && contentType != "Text")
        {
            format = "QR";
        }

        return new ScanResult
        {
            RawData = rawData,
            ParsedData = trimmed,
            Format = format,
            IsValid = isBarcode || contentType != "Text",
            ContentType = contentType,
            ParsedContent = parsedContent
        };
    }

    private static string DetectFormat(string data)
    {
        if (UuidRegex().IsMatch(data))
            return "UUID";

        var isAllDigits = data.All(char.IsDigit);
        if (isAllDigits)
        {
            return data.Length switch
            {
                8 => "EAN-8",
                12 => "UPC-A",
                13 => "EAN-13",
                14 => "GTIN-14",
                _ => "Numeric"
            };
        }

        return data.Length switch
        {
            22 => "GS1-128",
            <= 64 => "Code128",
            _ => "Unknown"
        };
    }
}
