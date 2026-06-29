using Microsoft.EntityFrameworkCore;
using ScanBridge.Data.Entities;

namespace ScanBridge.Data;

/// <summary>
/// Контекст базы данных приложения ScanBridge.
/// Управляет подключением к SQLite и определяет модель данных.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Таблица конфигураций сканеров.
    /// </summary>
    public DbSet<ScannerConfig> Scanners => Set<ScannerConfig>();

    /// <summary>
    /// Таблица групп пост-скан действий.
    /// </summary>
    public DbSet<PostScanActionGroup> PostScanActionGroups => Set<PostScanActionGroup>();

    /// <summary>
    /// Таблица пост-скан действий (привязаны к группам).
    /// </summary>
    public DbSet<PostScanAction> PostScanActions => Set<PostScanAction>();

    /// <summary>
    /// Таблица связей групп со сканерами.
    /// </summary>
    public DbSet<PostScanActionGroupScanner> PostScanActionGroupScanners => Set<PostScanActionGroupScanner>();

    /// <summary>
    /// Таблица настроек приложения (пары ключ-значение).
    /// </summary>
    public DbSet<AppSetting> Settings => Set<AppSetting>();

    /// <summary>
    /// Таблица записей логов.
    /// </summary>
    public DbSet<LogRecord> Logs => Set<LogRecord>();

    /// <summary>
    /// Таблица истории сканирований.
    /// </summary>
    public DbSet<ScanHistory> ScanHistory => Set<ScanHistory>();

    /// <summary>
    /// Таблица событий переподключения сканеров.
    /// </summary>
    public DbSet<ReconnectEvent> ReconnectEvents => Set<ReconnectEvent>();

    /// <summary>
    /// Создаёт экземпляр контекста базы данных.
    /// </summary>
    /// <param name="options">Параметры подключения к БД.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>
    /// Настраивает модель данных: индексы, ограничения длины полей, связи.
    /// </summary>
    /// <param name="modelBuilder">Построитель модели Entity Framework.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScannerConfig>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.PortName).HasMaxLength(20);
            e.Property(x => x.Parity).HasMaxLength(20);
            e.Property(x => x.StopBits).HasMaxLength(20);
            e.Property(x => x.Handshake).HasMaxLength(30);
        });

        modelBuilder.Entity<PostScanActionGroup>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<PostScanActionGroupScanner>(e =>
        {
            e.HasIndex(x => new { x.GroupId, x.ScannerName }).IsUnique();
            e.Property(x => x.ScannerName).HasMaxLength(100);
            e.Ignore(x => x.Group);
        });

        modelBuilder.Entity<PostScanAction>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(50);
            e.Property(x => x.SettingsJson).HasMaxLength(4000);
        });

        modelBuilder.Entity<AppSetting>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(2000);
        });

        modelBuilder.Entity<LogRecord>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.Level);
            e.Property(x => x.Level).HasMaxLength(10);
            e.Property(x => x.Message).HasMaxLength(4000);
        });

        modelBuilder.Entity<ScanHistory>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.ScannerName);
            e.HasIndex(x => x.Format);
            e.Property(x => x.ScannerName).HasMaxLength(100);
            e.Property(x => x.Format).HasMaxLength(20);
            e.Property(x => x.RawData).HasMaxLength(500);
            e.Property(x => x.ParsedData).HasMaxLength(500);
            e.Property(x => x.ContentType).HasMaxLength(20);
        });

        modelBuilder.Entity<ReconnectEvent>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.ScannerName);
            e.Property(x => x.ScannerName).HasMaxLength(100);
            e.Property(x => x.ErrorMessage).HasMaxLength(500);
        });
    }
}
