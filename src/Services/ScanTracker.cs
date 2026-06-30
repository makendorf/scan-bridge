namespace ScanBridge.Services;

/// <summary>
/// Трекер времени последнего сканирования.
/// Потокобезопасно хранит метку времени и имя сканера последнего успешного сканирования.
/// </summary>
public class ScanTracker
{
    private DateTime _lastScanTime = DateTime.MinValue;
    private string _lastScannerName = string.Empty;
    private readonly object _lock = new();

    /// <summary>
    /// Записывает время сканирования для указанного сканера.
    /// </summary>
    /// <param name="scannerName">Имя сканера, выполнившего сканирование.</param>
    public void RecordScan(string scannerName)
    {
        lock (_lock)
        {
            _lastScanTime = DateTime.UtcNow;
            _lastScannerName = scannerName;
        }
    }

    /// <summary>
    /// Возвращает информацию о последнем сканировании.
    /// </summary>
    /// <returns>Кортеж (Time, ScannerName) — UTC-время и имя сканера.</returns>
    public (DateTime Time, string ScannerName) GetLastScan()
    {
        lock (_lock)
        {
            return (_lastScanTime, _lastScannerName);
        }
    }
}
