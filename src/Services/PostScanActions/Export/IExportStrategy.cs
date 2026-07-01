namespace ScanBridge.Services.PostScanActions.Export;

/// <summary>
/// Стратегия экспорта данных в внешнее хранилище.
/// </summary>
public interface IExportStrategy
{
    /// <summary>
    /// Загружает данные в хранилище.
    /// </summary>
    /// <param name="data">Содержимое файла.</param>
    /// <param name="filename">Имя файла.</param>
    /// <param name="ct">Токен отмены.</param>
    Task UploadAsync(byte[] data, string filename, CancellationToken ct);
}
