using Renci.SshNet;

namespace ScanBridge.Services.PostScanActions.Export;

/// <summary>
/// Стратегия загрузки файла на SFTP-сервер.
/// </summary>
public class SftpExportStrategy : IExportStrategy
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _pass;
    private readonly string _remotePath;
    private readonly ILogger _logger;

    public SftpExportStrategy(string host, int port, string user, string pass,
        string remotePath, ILogger logger)
    {
        _host = host;
        _port = port;
        _user = user;
        _pass = pass;
        _remotePath = remotePath;
        _logger = logger;
    }

    public async Task UploadAsync(byte[] data, string filename, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_host))
        {
            _logger.LogWarning("Export SFTP: хост не указан");
            return;
        }

        var remotePath = _remotePath.TrimEnd('/') + "/" + filename;

        using var client = new SftpClient(_host, _port, _user, _pass);
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

        using var ms = new MemoryStream(data);
        using var stream = client.OpenWrite(remotePath);
        await ms.CopyToAsync(stream, ct);
        await stream.FlushAsync(ct);
        client.Disconnect();
    }
}
