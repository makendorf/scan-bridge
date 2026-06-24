using Microsoft.EntityFrameworkCore;
using ScanBridge.Data.Entities;

namespace ScanBridge.Data;

public class AppDbContext : DbContext
{
    public DbSet<ScannerConfig> Scanners => Set<ScannerConfig>();
    public DbSet<PostScanAction> PostScanActions => Set<PostScanAction>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<LogRecord> Logs => Set<LogRecord>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

        modelBuilder.Entity<PostScanAction>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(50);
            e.Property(x => x.ScannerName).HasMaxLength(100);
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
    }
}
