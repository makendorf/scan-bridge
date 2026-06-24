using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Data.Entities;

namespace ScanBridge.Services;

public class LogCollector
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Queue<LogRecord> _pending = new();
    private readonly object _lock = new();

    public LogCollector(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Add(string level, string message, string? exception = null)
    {
        var record = new LogRecord
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Exception = exception
        };

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            lock (_lock) { while (_pending.Count > 0) db.Logs.Add(_pending.Dequeue()); }
            db.Logs.Add(record);
            db.SaveChanges();
        }
        catch { lock (_lock) _pending.Enqueue(record); }
    }

    public void Clear()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Logs.ExecuteDelete();
            db.SaveChanges();
        }
        catch { }
    }
}
