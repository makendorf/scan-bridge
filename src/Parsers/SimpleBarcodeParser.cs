using System.Text.RegularExpressions;
using ScanBridge.Models;

namespace ScanBridge.Parsers;

/// <summary>
/// Парсер штрихкодов, поддерживающий форматы EAN-8, EAN-13, UPC-A, GTIN-14,
/// Code128, GS1-128, UUID и QR-коды с определением типа содержимого.
/// </summary>
public partial class SimpleBarcodeParser : IBarcodeParser
{
    private readonly QRContentDetector _qrDetector = new();

    /// <summary>
    /// Регулярное выражение для распознавания UUID формата
    /// (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).
    /// </summary>
    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex UuidRegex();

    /// <summary>
    /// Удаляет скрытые управляющие символы из строки сканера.
    /// Оставляет табуляцию (0x09), LF (0x0A), CR (0x0D) и печатные символы —
    /// они используются в структурированных форматах (vCard, WiFi).
    /// Удаляет: NULL (0x00-0x08), VT/FF (0x0B-0x0C), 0x0E-0x1F, DEL (0x7F).
    /// </summary>
    /// <param name="input">Исходная строка от сканера.</param>
    /// <returns>Строка без скрытых контрольных символов.</returns>
    private static string StripControlChars(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (ch >= ' ' || ch == '\t' || ch == '\n' || ch == '\r')
                sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Парсит сырые данные штрихкода, определяет формат и анализирует содержимое.
    /// </summary>
    /// <param name="rawData">Исходные данные от сканера.</param>
    /// <returns>Результат с определённым форматом, типом содержимого и флагом валидности.</returns>
    public ScanResult Parse(string rawData, int controlCharMode = 0)
    {
        var trimmed = rawData.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return new ScanResult
            {
                RawData = rawData,
                ParsedData = string.Empty,
                Format = "Empty",
                IsValid = true
            };
        }

        var forValidation = controlCharMode == 2 ? trimmed : StripControlChars(trimmed);

        if (string.IsNullOrEmpty(forValidation))
        {
            return new ScanResult
            {
                RawData = rawData,
                ParsedData = string.Empty,
                Format = "Empty",
                IsValid = true
            };
        }
        var isUuid = UuidRegex().IsMatch(forValidation);
        var isAllDigits = forValidation.All(char.IsDigit);
        var isAlphanumeric = forValidation.All(c => char.IsLetterOrDigit(c));
        var format = DetectFormat(forValidation);

        var (contentType, parsedContent) = _qrDetector.Detect(forValidation);

        var isBarcode = isUuid || isAllDigits || (isAlphanumeric && forValidation.Length >= 4);

        if (!isBarcode && contentType != "Text")
        {
            format = "QR";
        }

        var parsedData = controlCharMode == 0 ? StripControlChars(trimmed) : trimmed;

        return new ScanResult
        {
            RawData = rawData,
            ParsedData = parsedData,
            Format = format,
            IsValid = true,
            ContentType = contentType,
            ParsedContent = parsedContent
        };
    }

    /// <summary>
    /// Определяет формат штрихкода по его содержимому и длине.
    /// </summary>
    /// <param name="data">Очищенные данные штрихкода.</param>
    /// <returns>Строковое обозначение формата: EAN-8, EAN-13, UPC-A, Code128, UUID и т.д.</returns>
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
