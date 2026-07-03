using Microsoft.EntityFrameworkCore;
using ScanBridge.Data.Entities;

namespace ScanBridge.Data;

/// <summary>
/// Контекст базы данных приложения ScanBridge.
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<ScannerConfig> Scanners => Set<ScannerConfig>();
    public DbSet<CredentialConfig> Credentials => Set<CredentialConfig>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<LogRecord> Logs => Set<LogRecord>();
    public DbSet<ScanHistory> ScanHistory => Set<ScanHistory>();
    public DbSet<ReconnectEvent> ReconnectEvents => Set<ReconnectEvent>();
    public DbSet<Scenario> Scenarios => Set<Scenario>();
    public DbSet<ScenarioNode> ScenarioNodes => Set<ScenarioNode>();
    public DbSet<ScenarioConnection> ScenarioConnections => Set<ScenarioConnection>();

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

        modelBuilder.Entity<CredentialConfig>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Type).HasMaxLength(20);
            e.Property(x => x.Domain).HasMaxLength(100);
            e.Property(x => x.Username).HasMaxLength(100);
            e.Property(x => x.Password).HasMaxLength(200);
            e.Property(x => x.Host).HasMaxLength(200);
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

        modelBuilder.Entity<Scenario>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.ScannerNamesJson).HasMaxLength(4000);
        });

        modelBuilder.Entity<ScenarioNode>(e =>
        {
            e.HasIndex(x => x.ScenarioId);
            e.Property(x => x.NodeId).HasMaxLength(50);
            e.Property(x => x.Type).HasMaxLength(50);
            e.Property(x => x.SettingsJson).HasMaxLength(4000);
            e.Property(x => x.ActionType).HasMaxLength(100);
            e.Ignore(x => x.Scenario);
        });

        modelBuilder.Entity<ScenarioConnection>(e =>
        {
            e.HasIndex(x => x.ScenarioId);
            e.Property(x => x.SourceNodeId).HasMaxLength(50);
            e.Property(x => x.TargetNodeId).HasMaxLength(50);
            e.Property(x => x.SourcePort).HasMaxLength(50);
            e.Property(x => x.TargetPort).HasMaxLength(50);
            e.Ignore(x => x.Scenario);
        });
    }
}
