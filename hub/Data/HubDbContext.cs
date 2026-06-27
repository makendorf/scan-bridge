using Microsoft.EntityFrameworkCore;
using ScanBridgeHub.Models;

namespace ScanBridgeHub.Data;

/// <summary>
/// Контекст базы данных Hub-компонента ScanBridge.
/// Управляет подключением к SQLite и хранит конфигурацию удалённых экземпляров.
/// </summary>
public class HubDbContext : DbContext
{
    /// <summary>
    /// Таблица удалённых экземпляров ScanBridge.
    /// </summary>
    public DbSet<RemoteInstance> Instances => Set<RemoteInstance>();

    /// <summary>
    /// Таблица правил алертов.
    /// </summary>
    public DbSet<AlertRule> Alerts => Set<AlertRule>();

    /// <summary>
    /// Таблица событий сканирования.
    /// </summary>
    public DbSet<ScanEvent> ScanEvents => Set<ScanEvent>();

    private readonly string? _connectionString;

    /// <summary>
    /// Путь к файлу БД по умолчанию (относительно директории приложения).
    /// </summary>
    private static string DefaultDbPath => Path.Combine(AppContext.BaseDirectory, "hub.db");

    /// <summary>
    /// Конструктор по умолчанию (для DI).
    /// </summary>
    public HubDbContext() { }

    /// <summary>
    /// Создаёт контекст с указанным строкой подключения.
    /// </summary>
    /// <param name="connectionString">Строка подключения к SQLite.</param>
    public HubDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Создаёт контекст с опциями подключения (для DI).
    /// </summary>
    /// <param name="options">Параметры подключения.</param>
    public HubDbContext(DbContextOptions<HubDbContext> options) : base(options) { }

    /// <summary>
    /// Настраивает подключение к БД, если не было настроено через DI.
    /// </summary>
    /// <param name="options">Построитель опций.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
            options.UseSqlite(_connectionString ?? $"Data Source={DefaultDbPath}");
    }

    /// <summary>
    /// Настраивает модель данных: первичные ключи, ограничения длины полей.
    /// </summary>
    /// <param name="modelBuilder">Построитель модели.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RemoteInstance>().HasKey(e => e.Id);
        modelBuilder.Entity<RemoteInstance>().Property(e => e.Name).HasMaxLength(200);
        modelBuilder.Entity<RemoteInstance>().Property(e => e.Host).HasMaxLength(500);

        modelBuilder.Entity<AlertRule>().HasKey(e => e.Id);
        modelBuilder.Entity<AlertRule>().Property(e => e.Name).HasMaxLength(200);
        modelBuilder.Entity<AlertRule>().Property(e => e.Type).HasMaxLength(50);
        modelBuilder.Entity<AlertRule>().Property(e => e.SettingsJson).HasMaxLength(4000);

        modelBuilder.Entity<ScanEvent>().HasKey(e => e.Id);
        modelBuilder.Entity<ScanEvent>().Property(e => e.InstanceName).HasMaxLength(200);
        modelBuilder.Entity<ScanEvent>().Property(e => e.ScannerName).HasMaxLength(100);
        modelBuilder.Entity<ScanEvent>().Property(e => e.RawData).HasMaxLength(2000);
        modelBuilder.Entity<ScanEvent>().Property(e => e.ParsedData).HasMaxLength(2000);
        modelBuilder.Entity<ScanEvent>().Property(e => e.Format).HasMaxLength(50);
        modelBuilder.Entity<ScanEvent>().Property(e => e.ContentType).HasMaxLength(50);
        modelBuilder.Entity<ScanEvent>().HasIndex(e => e.Timestamp);
        modelBuilder.Entity<ScanEvent>().HasIndex(e => e.InstanceId);
        modelBuilder.Entity<ScanEvent>().HasIndex(e => e.ScannerName);
    }
}
