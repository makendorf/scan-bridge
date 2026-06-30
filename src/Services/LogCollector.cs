using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Data.Entities;

namespace ScanBridge.Services;

public class LogCollector
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Queue<LogRecord> _pending = new();
    private readonly object _lock = new();
    private AppDbContext? _db;

    public LogCollector(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private AppDbContext GetDbContext()
    {
        if (_db != null) return _db;
        lock (_lock)
        {
            if (_db != null) return _db;
            var scope = _serviceProvider.CreateScope();
            _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return _db;
        }
    }

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
            var db = GetDbContext();
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

    public void Clear()
    {
        lock (_lock) { _pending.Clear(); }

        try
        {
            var db = GetDbContext();
            db.Logs.ExecuteDelete();
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LogCollector clear failed: {ex.Message}");
        }
    }
}
