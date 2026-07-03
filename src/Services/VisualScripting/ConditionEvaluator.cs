using System.Diagnostics;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentFTP;
using Renci.SshNet;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Models;

namespace ScanBridge.Services.VisualScripting;

/// <summary>
/// Evaluator условий для Condition и While узлов.
/// Поддерживает: данные скана, файловую систему (local/ftp/sftp), время, системные проверки.
/// </summary>
public class ConditionEvaluator
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ConditionEvaluator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Оценивает одно условие.
    /// </summary>
    public bool Evaluate(Dictionary<string, string> settings, ScanResult scan)
    {
        if (!settings.TryGetValue("operator", out var op))
            return false;

        var field = settings.GetValueOrDefault("field", "data");
        var value = settings.GetValueOrDefault("value", "");

        return EvaluateSingle(field, op, value, settings, scan);
    }

    /// <summary>
    /// Оценивает несколько условий для While.
    /// </summary>
    public bool EvaluateMultiple(string conditionsJson, ScanResult scan, string logic = "and")
    {
        if (string.IsNullOrEmpty(conditionsJson)) return false;

        try
        {
            var conditions = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(conditionsJson);
            if (conditions == null || conditions.Count == 0) return false;

            if (string.Equals(logic, "or", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var condition in conditions)
                {
                    if (Evaluate(condition, scan))
                        return true;
                }
                return false;
            }
            else
            {
                foreach (var condition in conditions)
                {
                    if (!Evaluate(condition, scan))
                        return false;
                }
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private bool EvaluateSingle(string field, string op, string value, Dictionary<string, string> settings, ScanResult scan)
    {
        // Специальный случай: оператор isValid проверяет scan.IsValid напрямую
        if (op == "isValid")
            return scan.IsValid;

        string data;

        switch (field)
        {
            // ── Данные скана ──
            case "data":
            case "raw":
            case "format":
            case "scanner":
            case "isValid":
                data = GetScanField(field, scan);
                break;

            // ── Файловая система ──
            case "fileExists":
            case "fileContains":
            case "fileSize":
            case "dirExists":
                data = EvaluateFileCondition(field, value, settings);
                break;

            // ── Время ──
            case "timeOfDay":
                data = DateTime.Now.ToString("HH:mm");
                break;
            case "dayOfWeek":
                data = DateTime.Now.DayOfWeek.ToString().ToLower();
                break;
            case "date":
                data = DateTime.Now.ToString("yyyy-MM-dd");
                break;

            // ── Системные ──
            case "envVar":
                data = Environment.GetEnvironmentVariable(value) ?? "";
                break;
            case "processRunning":
                data = Process.GetProcessesByName(value).Length > 0 ? "true" : "false";
                break;
            case "serviceRunning":
                data = IsServiceRunning(value) ? "true" : "false";
                break;
            case "hostAvailable":
                data = CheckHost(value) ? "true" : "false";
                break;
            case "diskFreeMB":
                data = GetDiskFreeMB(value).ToString();
                break;

            // ── Метаданные ──
            case "metadata":
                data = scan.Metadata.TryGetValue(value, out var metaVal) ? metaVal : "";
                break;

            // ── Обработка данных ──
            case "jsonPath":
                data = ExtractJsonPath(scan.ParsedData, value);
                break;
            case "stringLength":
                data = (scan.ParsedData ?? "").Length.ToString();
                break;
            case "startsWith":
                data = (scan.ParsedData ?? "").StartsWith(value, StringComparison.OrdinalIgnoreCase) ? "true" : "false";
                break;
            case "endsWith":
                data = (scan.ParsedData ?? "").EndsWith(value, StringComparison.OrdinalIgnoreCase) ? "true" : "false";
                break;

            default:
                data = "";
                break;
        }

        return CompareValue(data, op, value);
    }

    private static string GetScanField(string field, ScanResult scan)
    {
        return field switch
        {
            "data" => scan.ParsedData ?? "",
            "raw" => scan.RawData ?? "",
            "format" => scan.Format ?? "",
            "scanner" => scan.ScannerName ?? "",
            "isValid" => scan.IsValid.ToString().ToLower(),
            _ => ""
        };
    }

    private string EvaluateFileCondition(string field, string filePath, Dictionary<string, string> settings)
    {
        var fileType = settings.GetValueOrDefault("fileType", "windows");
        var credentialIdStr = settings.GetValueOrDefault("credentialId", "");

        try
        {
            switch (field)
            {
                case "fileExists":
                    if (fileType == "windows")
                        return File.Exists(filePath).ToString().ToLower();
                    else
                        return RemoteFileExists(filePath, fileType, credentialIdStr).ToString().ToLower();

                case "dirExists":
                    if (fileType == "windows")
                        return Directory.Exists(filePath).ToString().ToLower();
                    else
                        return RemoteDirExists(filePath, fileType, credentialIdStr).ToString().ToLower();

                case "fileContains":
                    var value = settings.GetValueOrDefault("value", "");
                    var content = fileType == "windows"
                        ? File.ReadAllText(filePath)
                        : ReadRemoteFile(filePath, fileType, credentialIdStr);
                    return (content != null && content.Contains(value, StringComparison.OrdinalIgnoreCase)).ToString().ToLower();

                case "fileSize":
                    if (fileType == "windows")
                        return File.Exists(filePath) ? new FileInfo(filePath).Length.ToString() : "0";
                    else
                        return GetRemoteFileSize(filePath, fileType, credentialIdStr).ToString();
            }
        }
        catch
        {
            return field == "fileSize" ? "0" : "false";
        }

        return field == "fileSize" ? "0" : "false";
    }

    private bool RemoteFileExists(string path, string fileType, string credentialIdStr)
    {
        if (!int.TryParse(credentialIdStr, out var credId)) return false;
        var cred = GetCredential(credId);
        if (cred == null) return false;

        if (fileType == "ftp")
        {
            using var client = new AsyncFtpClient(cred.Host, cred.Username, cred.Password, cred.Port);
            client.Connect().GetAwaiter().GetResult();
            var exists = client.FileExists(path).GetAwaiter().GetResult();
            client.Disconnect().GetAwaiter().GetResult();
            return exists;
        }
        else if (fileType == "sftp")
        {
            using var client = new SftpClient(cred.Host, cred.Port, cred.Username, cred.Password);
            client.Connect();
            var exists = client.Exists(path);
            client.Disconnect();
            return exists;
        }
        return false;
    }

    private bool RemoteDirExists(string path, string fileType, string credentialIdStr)
    {
        if (!int.TryParse(credentialIdStr, out var credId)) return false;
        var cred = GetCredential(credId);
        if (cred == null) return false;

        if (fileType == "ftp")
        {
            using var client = new AsyncFtpClient(cred.Host, cred.Username, cred.Password, cred.Port);
            client.Connect().GetAwaiter().GetResult();
            var exists = client.DirectoryExists(path).GetAwaiter().GetResult();
            client.Disconnect().GetAwaiter().GetResult();
            return exists;
        }
        else if (fileType == "sftp")
        {
            using var client = new SftpClient(cred.Host, cred.Port, cred.Username, cred.Password);
            client.Connect();
            var exists = client.Exists(path);
            client.Disconnect();
            return exists;
        }
        return false;
    }

    private string? ReadRemoteFile(string path, string fileType, string credentialIdStr)
    {
        if (!int.TryParse(credentialIdStr, out var credId)) return null;
        var cred = GetCredential(credId);
        if (cred == null) return null;

        if (fileType == "ftp")
        {
            using var client = new AsyncFtpClient(cred.Host, cred.Username, cred.Password, cred.Port);
            client.Connect().GetAwaiter().GetResult();
            using var stream = new MemoryStream();
            client.DownloadStream(stream, path).GetAwaiter().GetResult();
            client.Disconnect().GetAwaiter().GetResult();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        else if (fileType == "sftp")
        {
            using var client = new SftpClient(cred.Host, cred.Port, cred.Username, cred.Password);
            client.Connect();
            using var stream = client.OpenRead(path);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            client.Disconnect();
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        return null;
    }

    private long GetRemoteFileSize(string path, string fileType, string credentialIdStr)
    {
        if (!int.TryParse(credentialIdStr, out var credId)) return 0;
        var cred = GetCredential(credId);
        if (cred == null) return 0;

        if (fileType == "ftp")
        {
            using var client = new AsyncFtpClient(cred.Host, cred.Username, cred.Password, cred.Port);
            client.Connect().GetAwaiter().GetResult();
            var size = client.GetFileSize(path).GetAwaiter().GetResult();
            client.Disconnect().GetAwaiter().GetResult();
            return size;
        }
        else if (fileType == "sftp")
        {
            using var client = new SftpClient(cred.Host, cred.Port, cred.Username, cred.Password);
            client.Connect();
            using var file = client.Open(path, FileMode.Open);
            var size = file.Length;
            client.Disconnect();
            return size;
        }
        return 0;
    }

    private CredentialConfig? GetCredential(int id)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Credentials.Find(id);
    }

    private static bool CheckHost(string hostPort)
    {
        var parts = hostPort.Split(':');
        var host = parts[0];
        var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 80;

        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
            if (success) client.EndConnect(result);
            return success;
        }
        catch { return false; }
    }

    private static long GetDiskFreeMB(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path) ?? "C:\\");
            return drive.AvailableFreeSpace / (1024 * 1024);
        }
        catch { return 0; }
    }

    private static string ExtractJsonPath(string? json, string path)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(path)) return "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            JsonElement current = doc.RootElement;

            foreach (var part in parts)
            {
                if (current.TryGetProperty(part, out var next))
                    current = next;
                else
                    return "";
            }

            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString() ?? "",
                JsonValueKind.Number => current.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "",
                _ => current.GetRawText()
            };
        }
        catch { return ""; }
    }

    private static bool IsServiceRunning(string serviceName)
    {
        try
        {
            var service = System.ServiceProcess.ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            if (service == null) return false;
            service.Refresh();
            return service.Status == System.ServiceProcess.ServiceControllerStatus.Running;
        }
        catch { return false; }
    }

    private static bool CompareValue(string data, string op, string value)
    {
        return op switch
        {
            "equals" => string.Equals(data, value, StringComparison.OrdinalIgnoreCase),
            "notEquals" => !string.Equals(data, value, StringComparison.OrdinalIgnoreCase),
            "contains" => data.Contains(value, StringComparison.OrdinalIgnoreCase),
            "notContains" => !data.Contains(value, StringComparison.OrdinalIgnoreCase),
            "regex" => Regex.IsMatch(data, value),
            "greaterThan" => CompareValues(data, value) > 0,
            "lessThan" => CompareValues(data, value) < 0,
            "isValid" => data == "true",
            _ => false
        };
    }

    private static int CompareValues(string a, string b)
    {
        if (double.TryParse(a, out var aNum) && double.TryParse(b, out var bNum))
            return aNum.CompareTo(bNum);

        return string.Compare(a, b, StringComparison.Ordinal);
    }
}
