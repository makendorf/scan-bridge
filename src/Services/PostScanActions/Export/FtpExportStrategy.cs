using FluentFTP;

namespace ScanBridge.Services.PostScanActions.Export;

/// <summary>
/// Стратегия загрузки файла на FTP-сервер.
/// </summary>
public class FtpExportStrategy : IExportStrategy
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _pass;
    private readonly string _remotePath;
    private readonly bool _passive;
    private readonly ILogger _logger;

    public FtpExportStrategy(string host, int port, string user, string pass,
        string remotePath, bool passive, ILogger logger)
    {
        _host = host;
        _port = port;
        _user = user;
        _pass = pass;
        _remotePath = remotePath;
        _passive = passive;
        _logger = logger;
    }

    public async Task UploadAsync(byte[] data, string filename, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_host))
        {
            _logger.LogWarning("Export FTP: хост не указан");
            return;
        }

        var remotePath = _remotePath.TrimEnd('/') + "/" + filename;

        using var client = new AsyncFtpClient(_host, _user, _pass, _port);
        client.Config.DataConnectionType = _passive
            ? FtpDataConnectionType.AutoPassive
            : FtpDataConnectionType.AutoActive;

        await client.Connect(ct);
        await client.UploadBytes(data, remotePath, FtpRemoteExists.Overwrite, true, null, ct);
        await client.Disconnect(ct);
    }
}
