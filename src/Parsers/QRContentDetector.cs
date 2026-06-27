using System.Text.Json;

namespace ScanBridge.Parsers;

/// <summary>
/// Детектор типа содержимого QR-кода.
/// Распознаёт URL, JSON, vCard, Wi-Fi конфигурации и простой текст.
/// </summary>
public class QRContentDetector
{
    /// <summary>
    /// Определяет тип содержимого QR-кода и возвращает его структурированное представление.
    /// </summary>
    /// <param name="rawData">Исходные данные QR-кода.</param>
    /// <returns>
    /// Кортеж (type, parsed): type — тип содержимого (Text, Url, Json, VCard, Wifi);
    /// parsed — форматированное представление или null.
    /// </returns>
    public (string type, string? parsed) Detect(string rawData)
    {
        if (string.IsNullOrWhiteSpace(rawData))
            return ("Text", null);

        var trimmed = rawData.Trim();

        if (IsUrl(trimmed))
            return ("Url", trimmed);

        if (IsJson(trimmed, out var formatted))
            return ("Json", formatted);

        if (IsVCard(trimmed))
            return ("VCard", trimmed);

        if (IsWifi(trimmed, out var wifiInfo))
            return ("Wifi", wifiInfo);

        return ("Text", trimmed);
    }

    /// <summary>
    /// Проверяет, является ли данные URL-адресом (http:// или https://).
    /// </summary>
    /// <param name="data">Проверяемая строка.</param>
    /// <returns>true если строка начинается с http:// или https://.</returns>
    private static bool IsUrl(string data)
    {
        return data.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               data.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет, является ли данные валидным JSON, и форматирует его.
    /// </summary>
    /// <param name="data">Проверяемая строка.</param>
    /// <param name="formatted">Форматированный JSON или null, если данные не являются JSON.</param>
    /// <returns>true если данные являются валидным JSON.</returns>
    private static bool IsJson(string data, out string? formatted)
    {
        formatted = null;
        if (data.Length < 2) return false;

        var first = data[0];
        if (first != '{' && first != '[') return false;

        try
        {
            using var doc = JsonDocument.Parse(data, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            formatted = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Проверяет, является ли данные визитной карточкой vCard.
    /// </summary>
    /// <param name="data">Проверяемая строка.</param>
    /// <returns>true если строка содержит "BEGIN:VCARD".</returns>
    private static bool IsVCard(string data)
    {
        return data.Contains("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет, является ли данные конфигурацией Wi-Fi сети (WIFI:...).
    /// </summary>
    /// <param name="data">Проверяемая строка.</param>
    /// <param name="info">Форматированная информация о сети (SSID, Auth, Password).</param>
    /// <returns>true если строка начинается с "WIFI:".</returns>
    private static bool IsWifi(string data, out string? info)
    {
        info = null;
        if (!data.StartsWith("WIFI:", StringComparison.OrdinalIgnoreCase)) return false;

        var props = ParseWifiProps(data);
        var ssid = props.GetValueOrDefault("S") ?? props.GetValueOrDefault("SSID");
        var auth = props.GetValueOrDefault("T") ?? props.GetValueOrDefault("AUTH");
        var hidden = props.GetValueOrDefault("H");

        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(ssid)) sb.Append($"SSID: {ssid}");
        if (!string.IsNullOrEmpty(auth)) sb.Append($", Auth: {auth}");
        if (!string.IsNullOrEmpty(hidden)) sb.Append($", Hidden: {hidden}");

        var password = props.GetValueOrDefault("P") ?? props.GetValueOrDefault("PASSWORD");
        if (!string.IsNullOrEmpty(password)) sb.Append($", Password: {password}");

        info = sb.ToString();
        return true;
    }

    /// <summary>
    /// Парсит параметры Wi-Fi строки формата WIFI:T:WPA;S:MyNetwork;P:password;;
    /// </summary>
    /// <param name="data">Исходная Wi-Fi строка.</param>
    /// <returns>Словарь ключ-значение параметров сети.</returns>
    private static Dictionary<string, string> ParseWifiProps(string data)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var content = data.Substring(5);

        foreach (var part in content.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIdx = part.IndexOf(':');
            var eqIdx = part.IndexOf('=');
            var sepIdx = colonIdx > 0 ? colonIdx : eqIdx;
            if (sepIdx > 0)
            {
                var key = part.Substring(0, sepIdx).Trim();
                var val = part.Substring(sepIdx + 1).Trim().Trim('"');
                props[key] = val;
            }
        }

        return props;
    }
}
