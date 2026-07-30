using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Models;
using ScanBridge.Services.PostScanActions.Export;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие экспорта результата сканирования в файл или внешний сервис.
/// Поддерживает локальное сохранение, FTP, SFTP и HTTP POST.
/// Делегирует загрузку IExportStrategy.
/// </summary>
public class ExportAction : IPostScanAction
{
    public string Type => "Export";

    private readonly ILogger<ExportAction> _logger;
    private readonly IExportStrategy _strategy;
    private readonly string _format;
    private readonly string _filenameTemplate;
    private readonly List<TagConfig> _tags = new();
    private readonly string _destination;
    private readonly string _rootKey;

    public ExportAction(ILogger<ExportAction> logger, Dictionary<string, string> settings, IServiceScopeFactory? scopeFactory = null)
    {
        _logger = logger;

        _format = settings.TryGetValue("Format", out var fmt) ? fmt.ToLowerInvariant() : "json";
        _filenameTemplate = settings.TryGetValue("FilenameTemplate", out var tpl) && !string.IsNullOrWhiteSpace(tpl)
            ? tpl : "{timestamp}_{scanner}_{data}";

        _destination = settings.TryGetValue("Destination", out var dest) ? dest.ToLowerInvariant() : "folder";
        _rootKey = settings.TryGetValue("RootKey", out var rk) ? rk ?? "" : "";

        _strategy = CreateStrategy(settings, logger, scopeFactory);

        if (settings.TryGetValue("Tags", out var json) && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<TagConfig>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null) _tags = parsed;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Export: ошибка разбора Tags"); }
        }

        if (_tags.Count == 0)
        {
            _tags =
            [
                new() { Key = "Timestamp", Source = "Timestamp" },
                new() { Key = "ScannerName", Source = "ScannerName" },
                new() { Key = "RawData", Source = "RawData" },
                new() { Key = "ParsedData", Source = "ParsedData" },
                new() { Key = "Format", Source = "Format" },
                new() { Key = "IsValid", Source = "IsValid" }
            ];
        }
    }

    public async Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        string content;
        string ext;

        if (_destination == "http-get")
        {
            // Для GET: теги формируют параметры URL, а не JSON
            content = BuildQueryString(scan);
            ext = "txt";
        }
        else
        {
            content = _format == "xml" ? BuildXml(scan) : BuildJson(scan);
            ext = _format == "xml" ? "xml" : "json";
        }

        var filename = MakeFilename(scan) + "." + ext;
        var bytes = Encoding.UTF8.GetBytes(content);

        try
        {
            await _strategy.UploadAsync(bytes, filename, ct);
            scan.Metadata["exportSuccess"] = "true";
            scan.Metadata["exportFilename"] = filename;
            _logger.LogInformation("Export [{Dest}]: {File}", _destination, filename);
        }
        catch (Exception ex)
        {
            scan.Metadata["exportSuccess"] = "false";
            scan.Metadata["exportError"] = ex.Message;
            _logger.LogError(ex, "Export [{Dest}]: ошибка", _destination);
        }
    }

    private IExportStrategy CreateStrategy(Dictionary<string, string> settings, ILogger logger, IServiceScopeFactory? scopeFactory)
    {
        return _destination switch
        {
            "ftp" => CreateFtpStrategy(settings, logger, scopeFactory),
            "sftp" => CreateSftpStrategy(settings, logger, scopeFactory),
            "http" => new HttpExportStrategy(
                Get(settings, "HttpUrl"),
                Get(settings, "HttpContentType", _format == "xml" ? "application/xml" : "application/json"),
                ParseHeaders(settings, logger),
                logger),
            "http-get" => new HttpGetExportStrategy(
                Get(settings, "HttpUrl"),
                ParseHeaders(settings, logger),
                logger),
            _ => new FolderExportStrategy(Get(settings, "FolderPath"), logger)
        };
    }

    private FtpExportStrategy CreateFtpStrategy(Dictionary<string, string> settings, ILogger logger, IServiceScopeFactory? scopeFactory)
    {
        if (settings.TryGetValue("CredentialId", out var credIdStr) && int.TryParse(credIdStr, out var credId) && scopeFactory != null)
        {
            var cred = LoadCredential(scopeFactory, credId);
            if (cred != null)
            {
                var remotePath = Get(settings, "FtpRemotePath", "/");
                return new FtpExportStrategy(cred.Host, cred.Port, cred.Username, cred.Password, remotePath, cred.PassiveMode, logger);
            }
        }
        // Fallback to manual settings
        return new FtpExportStrategy(
            Get(settings, "FtpHost"), GetInt(settings, "FtpPort", 21),
            Get(settings, "FtpUser"), Get(settings, "FtpPass"),
            Get(settings, "FtpRemotePath", "/"),
            !settings.TryGetValue("FtpPassive", out var pasv) || bool.TryParse(pasv, out var p) && p,
            logger);
    }

    private SftpExportStrategy CreateSftpStrategy(Dictionary<string, string> settings, ILogger logger, IServiceScopeFactory? scopeFactory)
    {
        if (settings.TryGetValue("CredentialId", out var credIdStr) && int.TryParse(credIdStr, out var credId) && scopeFactory != null)
        {
            var cred = LoadCredential(scopeFactory, credId);
            if (cred != null)
            {
                var remotePath = Get(settings, "FtpRemotePath", "/");
                return new SftpExportStrategy(cred.Host, cred.Port, cred.Username, cred.Password, remotePath, logger);
            }
        }
        // Fallback to manual settings
        return new SftpExportStrategy(
            Get(settings, "FtpHost"), GetInt(settings, "FtpPort", 22),
            Get(settings, "FtpUser"), Get(settings, "FtpPass"),
            Get(settings, "FtpRemotePath", "/"),
            logger);
    }

    private static CredentialConfig? LoadCredential(IServiceScopeFactory scopeFactory, int id)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Credentials.Find(id);
    }

    private static string Get(Dictionary<string, string> s, string key, string fallback = "")
        => s.TryGetValue(key, out var v) ? v : fallback;

    private static int GetInt(Dictionary<string, string> s, string key, int fallback)
        => s.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : fallback;

    private static Dictionary<string, string> ParseHeaders(Dictionary<string, string> settings, ILogger logger)
    {
        if (settings.TryGetValue("HttpHeaders", out var json) && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (headers != null) return headers;
            }
            catch (Exception ex) { logger.LogWarning(ex, "Export: ошибка разбора HTTP заголовков"); }
        }
        return new();
    }

    private string BuildJson(ScanResult scan)
    {
        var dict = new Dictionary<string, object>();
        foreach (var tag in _tags)
            dict[tag.Key] = ResolveValue(tag, scan);

        if (!string.IsNullOrEmpty(_rootKey))
        {
            var wrapper = new Dictionary<string, object> { [_rootKey] = new[] { dict } };
            return JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true });
        }

        return JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
    }

    private string BuildXml(ScanResult scan)
    {
        var root = new XElement("ScanResult");
        foreach (var tag in _tags)
            root.Add(new XElement(tag.Key, ResolveValue(tag, scan)));
        var doc = new XDocument(root);
        return doc.Declaration != null
            ? doc.Declaration + Environment.NewLine + doc
            : doc.ToString();
    }

    private string BuildQueryString(ScanResult scan)
    {
        var parts = new List<string>();
        foreach (var tag in _tags)
        {
            var value = ResolveValue(tag, scan);
            var strValue = value switch
            {
                bool b => b.ToString().ToLower(),
                double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                int i => i.ToString(),
                _ => value?.ToString() ?? string.Empty
            };
            parts.Add($"{Uri.EscapeDataString(tag.Key)}={Uri.EscapeDataString(strValue)}");
        }
        return string.Join("&", parts);
    }

    private static object ResolveValue(TagConfig tag, ScanResult scan)
    {
        return tag.Source switch
        {
            "Timestamp" => scan.Timestamp.ToString("o"),
            "ScannerName" => scan.ScannerName,
            "RawData" => scan.RawData,
            "ParsedData" => scan.ParsedData,
            "Format" => scan.Format,
            "IsValid" => scan.IsValid,
            "ContentType" => scan.ContentType,
            "ParsedContent" => scan.ParsedContent ?? string.Empty,
            "Custom" => TryParseBool(tag.Value, out var b) ? b : (object)(tag.Value ?? string.Empty),
            _ => string.Empty
        };
    }

    private string MakeFilename(ScanResult scan)
    {
        return _filenameTemplate
            .Replace("{timestamp}", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"))
            .Replace("{scanner}", Sanitize(scan.ScannerName))
            .Replace("{data}", Sanitize(scan.ParsedData))
            .Replace("{format}", Sanitize(scan.Format));
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return sb.ToString();
    }

    private static bool TryParseBool(string? value, out bool result)
    {
        result = false;
        if (string.IsNullOrEmpty(value)) return false;
        if (bool.TryParse(value, out result)) return true;
        // Also handle common variations
        var trimmed = value.Trim();
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)) { result = true; return true; }
        if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase)) { result = false; return true; }
        return false;
    }

    public class TagConfig
    {
        public string Key { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? Value { get; set; }
    }
}
