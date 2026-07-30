using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Data.Entities;

namespace ScanBridge.Services;

public class LogCollector
{
    private readonly IServiceScopeFactory _scopeFactory;
    // Используем потокобезопасную очередь вместо lock + Queue
    private readonly ConcurrentQueue<LogRecord> _pending = new();

    public LogCollector(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
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
            // КРИТИЧЕСКИ ВАЖНО: Создаем НОВЫЙ scope и НОВЫЙ DbContext для каждой операции.
            // Это гарантирует отсутствие конфликтов потоков и чистый ChangeTracker.
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Сначала пытаемся сохранить все накопленные ошибочные записи
            while (_pending.TryDequeue(out var pendingRecord))
            {
                db.Logs.Add(pendingRecord);
            }

            // Добавляем текущую запись
            db.Logs.Add(record);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            // Если сохранить не удалось, кладем запись в очередь для следующей попытки
            _pending.Enqueue(record);

            // ВАЖНО: Здесь НЕЛЬЗЯ использовать ILogger или Serilog! 
            // Это мгновенно создаст бесконечную рекурсию (StackOverflow).
            // Оставляем Debug.WriteLine или запись в текстовый файл.
            System.Diagnostics.Debug.WriteLine($"[LogCollector] Save failed: {ex.Message}");
        }
    }

    public void Clear()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Logs.ExecuteDelete();
            db.SaveChanges();

            // Очищаем и оперативную очередь
            while (_pending.TryDequeue(out _)) { }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LogCollector] Clear failed: {ex.Message}");
        }
    }
}