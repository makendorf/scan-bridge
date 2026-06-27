using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Data.Entities;

namespace ScanBridge.Services;

/// <summary>
/// Коллектор логов — сохраняет записи логов в базу данных SQLite.
/// Использует очередь для буферизации при ошибках записи.
/// Потокобезопасен через объект блокировки.
/// </summary>
public class LogCollector
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Queue<LogRecord> _pending = new();
    private readonly object _lock = new();

    /// <summary>
    /// Создаёт экземпляр коллектора логов.
    /// </summary>
    /// <param name="serviceProvider">Провайдер зависимостей для создания scope БД.</param>
    public LogCollector(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Добавляет запись лога в базу данных. При ошибке записи — буферизует в очереди.
    /// При следующей успешной попытке буфер сбрасывается в БД.
    /// </summary>
    /// <param name="level">Уровень логирования: INF, WRN, ERR, DBG, FTL, VRB.</param>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="exception">Текст исключения (опционально).</param>
    public void Add(string level, string message, string? exception = null)
    {
        var record = new LogRecord
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message,
            Exception = exception
        };

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            LogRecord[] pending;
            lock (_lock)
            {
                pending = _pending.ToArray();
                _pending.Clear();
            }
            foreach (var p in pending)
                db.Logs.Add(p);
            db.Logs.Add(record);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            lock (_lock) _pending.Enqueue(record);
            System.Diagnostics.Debug.WriteLine($"LogCollector save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Очищает все записи логов из базы данных и буфера.
    /// </summary>
    public void Clear()
    {
        lock (_lock) { _pending.Clear(); }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Logs.ExecuteDelete();
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LogCollector clear failed: {ex.Message}");
        }
    }
}
