using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace ScanBridgeExtention.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Product> Products { get; set; }
        public DbSet<AdminLock> AdminLocks { get; set; }
        public DbSet<DictionaryItem> Dictionaries { get; set; }
        public DbSet<NkSetting> NkSettings { get; set; }
        public DbSet<UserConfirmation> UserConfirmations { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Уникальное ограничение для admin_locks (UQ_admin_locks_0)
            modelBuilder.Entity<AdminLock>()
                .HasIndex(al => al.ProductId)
                .IsUnique()
                .HasDatabaseName("UQ_admin_locks_0");

            // 2. Уникальное ограничение для user_confirmations (UQ_user_confirmations_0)
            modelBuilder.Entity<UserConfirmation>()
                .HasIndex(uc => new { uc.ProductId, uc.Username })
                .IsUnique()
                .HasDatabaseName("UQ_user_confirmations_0");

            // 3. Уникальное ограничение для dictionaries (UQ_dictionaries_0)
            modelBuilder.Entity<DictionaryItem>()
                .HasIndex(d => new { d.DictName, d.Value })
                .IsUnique()
                .HasDatabaseName("UQ_dictionaries_0");

            // 4. Уникальное ограничение для users (UQ_users_0)
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique()
                .HasDatabaseName("UQ_users_0");

            // Опционально: настройка поведения при удалении (каскадное удаление)
            // Если продукт удаляется, удалять его блокировки и подтверждения
            modelBuilder.Entity<AdminLock>()
                .HasOne(al => al.Product)
                .WithMany()
                .HasForeignKey(al => al.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserConfirmation>()
                .HasOne(uc => uc.Product)
                .WithMany()
                .HasForeignKey(uc => uc.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
