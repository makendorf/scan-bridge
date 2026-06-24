using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FluentFTP;
using Renci.SshNet;
using ScanBridge.Models;

namespace ScanBridge.Services.PostScanActions;

public class ExportAction : IPostScanAction
{
    public string Type => "Export";

    private readonly ILogger<ExportAction> _logger;
    private readonly string _format;
    private readonly string _filenameTemplate;
    private readonly List<TagConfig> _tags = new();

    private readonly string _destination;
    private readonly string _folderPath;
    private readonly string _ftpHost;
    private readonly int _ftpPort;
    private readonly string _ftpUser;
    private readonly string _ftpPass;
    private readonly string _ftpRemotePath;
    private readonly bool _ftpPassive;
    private readonly string _httpUrl;
    private readonly string _httpContentType;
    private readonly Dictionary<string, string> _httpHeaders = new();

    public ExportAction(ILogger<ExportAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        _format = settings.TryGetValue("Format", out var fmt) ? fmt.ToLowerInvariant() : "json";
        _filenameTemplate = settings.TryGetValue("FilenameTemplate", out var tpl) && !string.IsNullOrWhiteSpace(tpl)
            ? tpl
            : "{timestamp}_{scanner}_{data}";

        _destination = settings.TryGetValue("Destination", out var dest) ? dest.ToLowerInvariant() : "folder";

        _folderPath = settings.TryGetValue("FolderPath", out var path) ? path : string.Empty;

        _ftpHost = settings.TryGetValue("FtpHost", out var host) ? host : string.Empty;
        _ftpPort = settings.TryGetValue("FtpPort", out var portStr) && int.TryParse(portStr, out var port) ? port : 21;
        _ftpUser = settings.TryGetValue("FtpUser", out var user) ? user : string.Empty;
        _ftpPass = settings.TryGetValue("FtpPass", out var pass) ? pass : string.Empty;
        _ftpRemotePath = settings.TryGetValue("FtpRemotePath", out var rp) ? rp : "/";
        _ftpPassive = !settings.TryGetValue("FtpPassive", out var pasv) || bool.TryParse(pasv, out var p) && p;

        _httpUrl = settings.TryGetValue("HttpUrl", out var url) ? url : string.Empty;
        _httpContentType = settings.TryGetValue("HttpContentType", out var ct) && !string.IsNullOrWhiteSpace(ct)
            ? ct
            : (_format == "xml" ? "application/xml" : "application/json");

        if (settings.TryGetValue("HttpHeaders", out var headersJson) && !string.IsNullOrWhiteSpace(headersJson))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (headers != null) _httpHeaders = headers;
            }
            catch { }
        }

        if (settings.TryGetValue("Tags", out var json) && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<TagConfig>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null) _tags = parsed;
            }
            catch { }
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
        var content = _format == "xml" ? BuildXml(scan) : BuildJson(scan);
        var ext = _format == "xml" ? "xml" : "json";
        var filename = MakeFilename(scan) + "." + ext;
        var bytes = Encoding.UTF8.GetBytes(content);

        try
        {
            switch (_destination)
            {
                case "ftp":
                    await UploadFtp(bytes, filename, ct);
                    break;
                case "sftp":
                    await UploadSftp(bytes, filename, ct);
                    break;
                case "http":
                    await PostHttp(content, ct);
                    break;
                default:
                    SaveLocal(bytes, filename);
                    break;
            }

            _logger.LogInformation("Export [{Dest}]: {File}", _destination, filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export [{Dest}]: ошибка", _destination);
        }
    }

    private void SaveLocal(byte[] bytes, string filename)
    {
        if (string.IsNullOrWhiteSpace(_folderPath))
        {
            _logger.LogWarning("Export: папка не указана");
            return;
        }
        Directory.CreateDirectory(_folderPath);
        File.WriteAllBytes(Path.Combine(_folderPath, filename), bytes);
    }

    private async Task UploadFtp(byte[] bytes, string filename, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_ftpHost))
        {
            _logger.LogWarning("Export FTP: хост не указан");
            return;
        }

        var remotePath = _ftpRemotePath.TrimEnd('/') + "/" + filename;

        using var client = new AsyncFtpClient(_ftpHost, _ftpUser, _ftpPass, _ftpPort);
        client.Config.DataConnectionType = _ftpPassive
            ? FtpDataConnectionType.AutoPassive
            : FtpDataConnectionType.AutoActive;

        await client.Connect(ct);
        await client.UploadBytes(bytes, remotePath, FtpRemoteExists.Overwrite, true, null, ct);
        await client.Disconnect(ct);
    }

    private async Task UploadSftp(byte[] bytes, string filename, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_ftpHost))
        {
            _logger.LogWarning("Export SFTP: хост не указан");
            return;
        }

        var remotePath = _ftpRemotePath.TrimEnd('/') + "/" + filename;

        using var client = new SftpClient(_ftpHost, _ftpPort, _ftpUser, _ftpPass);
        client.Connect();

        var dir = Path.GetDirectoryName(remotePath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(dir))
        {
            var parts = dir.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = "";
            foreach (var part in parts)
            {
                current += "/" + part;
                if (!client.Exists(current))
                    client.CreateDirectory(current);
            }
        }

        using var ms = new MemoryStream(bytes);
        await ms.CopyToAsync(client.OpenWrite(remotePath), ct);
        client.Disconnect();
    }

    private async Task PostHttp(string content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_httpUrl))
        {
            _logger.LogWarning("Export HTTP: URL не указан");
            return;
        }

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        foreach (var header in _httpHeaders)
        {
            if (!client.DefaultRequestHeaders.Contains(header.Key))
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        var body = new StringContent(content, Encoding.UTF8, _httpContentType);
        var response = await client.PostAsync(_httpUrl, body, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Export HTTP: {Url} → {Status}", _httpUrl, (int)response.StatusCode);
    }

    private string BuildJson(ScanResult scan)
    {
        var dict = new Dictionary<string, object>();
        foreach (var tag in _tags)
            dict[tag.Key] = ResolveValue(tag, scan);
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
            "Custom" => tag.Value ?? string.Empty,
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

    public class TagConfig
    {
        public string Key { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? Value { get; set; }
    }
}
