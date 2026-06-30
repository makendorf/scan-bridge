using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Models;

namespace ScanBridge.Services;

/// <summary>
/// Сервис истории сканирований.
/// Сохраняет результаты сканирований и ошибки переподключения в SQLite.
/// Предоставляет методы для получения статистики дашборда.
/// </summary>
public class ScanHistoryService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScanHistoryService> _logger;
    private const int RetentionDays = 30;

    /// <summary>
    /// Создаёт экземпляр сервиса истории сканирований.
    /// </summary>
    /// <param name="scopeFactory">Фабрика scope для доступа к DbContext.</param>
    /// <param name="logger">Логгер.</param>
    public ScanHistoryService(IServiceScopeFactory scopeFactory, ILogger<ScanHistoryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Записывает результат сканирования в БД (fire-and-forget).
    /// </summary>
    /// <param name="scan">Результат сканирования.</param>
    public void RecordScan(ScanResult scan)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.ScanHistory.Add(new ScanHistory
                {
                    Timestamp = scan.Timestamp,
                    ScannerName = scan.ScannerName,
                    Format = scan.Format,
                    RawData = scan.RawData,
                    ParsedData = scan.ParsedData,
                    IsValid = scan.IsValid,
                    ContentType = scan.ContentType
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось сохранить запись сканирования");
            }
        });
    }

    /// <summary>
    /// Записывает событие переподключения в БД (fire-and-forget).
    /// </summary>
    /// <param name="scannerName">Имя сканера.</param>
    /// <param name="error">Текст ошибки.</param>
    /// <param name="attempt">Номер попытки.</param>
    public void RecordReconnect(string scannerName, string error, int attempt)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.ReconnectEvents.Add(new ReconnectEvent
                {
                    Timestamp = DateTime.UtcNow,
                    ScannerName = scannerName,
                    ErrorMessage = error,
                    AttemptNumber = attempt
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось сохранить событие переподключения для {Scanner}", scannerName);
            }
        });
    }

    /// <summary>
    /// Удаляет записи старше RetentionDays. Вызывается при старте.
    /// </summary>
    public async Task CleanupOldRecordsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            var deleted = await db.ScanHistory
                .Where(h => h.Timestamp < cutoff)
                .ExecuteDeleteAsync();
            var deletedReconnects = await db.ReconnectEvents
                .Where(r => r.Timestamp < cutoff)
                .ExecuteDeleteAsync();
            if (deleted > 0 || deletedReconnects > 0)
                _logger.LogInformation("Очищено {Scans} записей сканирований и {Reconnects} событий переподключения",
                    deleted, deletedReconnects);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при очистке старых записей");
        }
    }

    /// <summary>
    /// Получает KPI-статистику для дашборда.
    /// </summary>
    public async Task<object> GetStatsAsync(ScannerManager manager)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var today = DateTime.UtcNow.Date;
            var totalScans = await db.ScanHistory.CountAsync();
            var scansToday = await db.ScanHistory.CountAsync(h => h.Timestamp >= today);
            var validScans = await db.ScanHistory.CountAsync(h => h.IsValid);
            var successRate = totalScans > 0 ? (double)validScans / totalScans * 100 : 0;
            var dbSizeMb = await GetDbSizeAsync();

            return new
            {
                totalScanners = (await db.Scanners.CountAsync()),
                activeScanners = manager.GetRunning().Count,
                totalScans,
                scansToday,
                successRate = Math.Round(successRate, 1),
                dbSizeMb
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при получении статистики");
            return new { totalScanners = 0, activeScanners = 0, totalScans = 0, scansToday = 0, successRate = 0.0, dbSizeMb = 0.0 };
        }
    }

    /// <summary>
    /// Получает данные активности по часам/дням.
    /// </summary>
    /// <param name="period">Период: "24h", "7d", "30d".</param>
    public async Task<object> GetActivityAsync(string period)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            DateTime since;
            int buckets;
            bool truncateByDay;

            switch (period)
            {
                case "7d":
                    since = DateTime.UtcNow.AddDays(-7);
                    buckets = 7;
                    truncateByDay = true;
                    break;
                case "30d":
                    since = DateTime.UtcNow.AddDays(-30);
                    buckets = 30;
                    truncateByDay = true;
                    break;
                default: // "24h"
                    since = DateTime.UtcNow.AddHours(-24);
                    buckets = 24;
                    truncateByDay = false;
                    break;
            }

            var records = await db.ScanHistory
                .Where(h => h.Timestamp >= since)
                .Select(h => new { h.Timestamp })
                .ToListAsync();

            var grouped = records
                .GroupBy(h => truncateByDay
                    ? new DateTime(h.Timestamp.Year, h.Timestamp.Month, h.Timestamp.Day, 0, 0, 0, DateTimeKind.Utc)
                    : new DateTime(h.Timestamp.Year, h.Timestamp.Month, h.Timestamp.Day, h.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
                .Select(g => new { Bucket = g.Key, Count = g.Count() })
                .OrderBy(x => x.Bucket)
                .ToList();

            var labels = new List<string>();
            var counts = new List<int>();

            if (period == "24h")
            {
                var now = DateTime.UtcNow;
                for (var i = buckets - 1; i >= 0; i--)
                {
                    var hour = now.AddHours(-i);
                    labels.Add(hour.ToString("HH:00"));
                    var match = grouped.FirstOrDefault(d => d.Bucket.Hour == hour.Hour && d.Bucket.Date == hour.Date);
                    counts.Add(match?.Count ?? 0);
                }
            }
            else
            {
                var now = DateTime.UtcNow.Date;
                for (var i = buckets - 1; i >= 0; i--)
                {
                    var day = now.AddDays(-i);
                    labels.Add(day.ToString("dd.MM"));
                    var match = grouped.FirstOrDefault(d => d.Bucket.Date == day);
                    counts.Add(match?.Count ?? 0);
                }
            }

            return new { labels, data = counts };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при получении данных активности");
            return new { labels = new List<string>(), data = new List<int>() };
        }
    }

    /// <summary>
    /// Получает последние N сканирований.
    /// </summary>
    public async Task<List<object>> GetRecentScansAsync(int limit = 50)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.ScanHistory
                .OrderByDescending(h => h.Timestamp)
                .Take(limit)
                .Select(h => (object)new
                {
                    h.Timestamp,
                    h.ScannerName,
                    h.Format,
                    h.ParsedData,
                    h.IsValid
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при получении последних сканов");
            return new List<object>();
        }
    }

    /// <summary>
    /// Получает распределение по форматам штрихкодов.
    /// </summary>
    public async Task<object> GetFormatsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var data = await db.ScanHistory
                .GroupBy(h => h.Format)
                .Select(g => new { Format = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var colors = new[]
            {
                "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6",
                "#EC4899", "#06B6D4", "#84CC16", "#F97316", "#6366F1"
            };

            return new
            {
                labels = data.Select(d => d.Format).ToList(),
                data = data.Select(d => d.Count).ToList(),
                colors = data.Select((_, i) => colors[i % colors.Length]).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при получении распределения форматов");
            return new { labels = new List<string>(), data = new List<int>(), colors = new List<string>() };
        }
    }

    /// <summary>
    /// Получает статистику по каждому сканеру.
    /// </summary>
    public async Task<List<object>> GetPerScannerAsync(ScannerManager manager)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var running = manager.GetRunning();
            var uptime = manager.GetUptime();

            var scannerStats = await db.ScanHistory
                .GroupBy(h => h.ScannerName)
                .Select(g => new
                {
                    Name = g.Key,
                    Total = g.Count(),
                    Valid = g.Count(h => h.IsValid),
                    LastScan = g.Max(h => h.Timestamp)
                })
                .ToListAsync();

            return scannerStats.Select(s =>
            {
                var isActive = running.Contains(s.Name);
                var scannerUptime = uptime.TryGetValue(s.Name, out var up) ? up : TimeSpan.Zero;
                var hours = scannerUptime.TotalHours;
                var avgSpeed = hours > 0 ? Math.Round(s.Total / hours, 1) : 0;

                return (object)new
                {
                    s.Name,
                    s.Total,
                    s.Valid,
                    s.LastScan,
                    IsActive = isActive,
                    Uptime = FormatUptime(scannerUptime),
                    UptimeSeconds = (long)scannerUptime.TotalSeconds,
                    AvgScansPerHour = avgSpeed
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при получении статистики по сканерам");
            return new List<object>();
        }
    }

    /// <summary>
    /// Получает ошибки переподключения за N часов.
    /// </summary>
    public async Task<List<object>> GetReconnectErrorsAsync(int hours = 24)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var since = DateTime.UtcNow.AddHours(-hours);
            return await db.ReconnectEvents
                .Where(r => r.Timestamp >= since)
                .OrderByDescending(r => r.Timestamp)
                .Take(100)
                .Select(r => (object)new
                {
                    r.Timestamp,
                    r.ScannerName,
                    r.ErrorMessage,
                    r.AttemptNumber
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при получении ошибок переподключения");
            return new List<object>();
        }
    }

    /// <summary>
    /// Получает размер БД сканов в МБ.
    /// </summary>
    private async Task<double> GetDbSizeAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var connectionString = db.Database.GetConnectionString() ?? "";
            var dbPath = "scanbridge.db";

            foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase))
                {
                    dbPath = kv[1].Trim();
                    break;
                }
            }

            if (!Path.IsPathRooted(dbPath))
                dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);
            if (File.Exists(dbPath))
            {
                var size = new FileInfo(dbPath).Length;
                return Math.Round(size / 1024.0 / 1024.0, 2);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при определении размера БД");
        }
        return 0;
    }

    /// <summary>
    /// Форматирует TimeSpan в читаемую строку.
    /// </summary>
    private static string FormatUptime(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}д {ts.Hours}ч";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}ч {ts.Minutes}м";
        return $"{(int)ts.TotalMinutes}м";
    }
}
