namespace ScanBridge.Services.PostScanActions.Export;

/// <summary>
/// Стратегия сохранения файла в локальную папку.
/// </summary>
public class FolderExportStrategy : IExportStrategy
{
    private readonly string _folderPath;
    private readonly ILogger _logger;

    public FolderExportStrategy(string folderPath, ILogger logger)
    {
        _folderPath = folderPath;
        _logger = logger;
    }

    public Task UploadAsync(byte[] data, string filename, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_folderPath))
        {
            _logger.LogWarning("Export: папка не указана");
            return Task.CompletedTask;
        }

        Directory.CreateDirectory(_folderPath);
        File.WriteAllBytes(Path.Combine(_folderPath, filename), data);
        return Task.CompletedTask;
    }
}
