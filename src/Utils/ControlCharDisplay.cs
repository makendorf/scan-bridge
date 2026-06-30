namespace ScanBridge.Utils;

/// <summary>
/// Форматирование управляющих символов для отображения в логах.
/// Преобразует невидимые символы (0x00-0x1F, 0x7F) в читаемые escape-последовательности.
/// </summary>
public static class ControlCharDisplay
{
    private static readonly Dictionary<char, string> SpecialNames = new()
    {
        ['\0'] = "\\0",
        ['\x01'] = "\\SOH",
        ['\x02'] = "\\STX",
        ['\x03'] = "\\ETX",
        ['\x04'] = "\\EOT",
        ['\x05'] = "\\ENQ",
        ['\x06'] = "\\ACK",
        ['\x07'] = "\\BEL",
        ['\x08'] = "\\BS",
        ['\t'] = "\\t",
        ['\n'] = "\\n",
        ['\x0B'] = "\\VT",
        ['\x0C'] = "\\FF",
        ['\r'] = "\\r",
        ['\x0E'] = "\\SO",
        ['\x0F'] = "\\SI",
        ['\x10'] = "\\DLE",
        ['\x11'] = "\\DC1",
        ['\x12'] = "\\DC2",
        ['\x13'] = "\\DC3",
        ['\x14'] = "\\DC4",
        ['\x15'] = "\\NAK",
        ['\x16'] = "\\SYN",
        ['\x17'] = "\\ETB",
        ['\x18'] = "\\CAN",
        ['\x19'] = "\\EM",
        ['\x1A'] = "\\SUB",
        ['\x1B'] = "\\ESC",
        ['\x1C'] = "\\FS",
        ['\x1D'] = "\\GS",
        ['\x1E'] = "\\RS",
        ['\x1F'] = "\\US",
        ['\x7F'] = "\\DEL",
    };

    /// <summary>
    /// Заменяет управляющие символы на читаемые escape-последовательности для логов.
    /// Печатные символы и пробел остаются как есть.
    /// </summary>
    public static string ForDisplay(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (SpecialNames.TryGetValue(ch, out var name))
                sb.Append(name);
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }
}
