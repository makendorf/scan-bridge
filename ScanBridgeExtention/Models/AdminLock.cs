using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanBridgeExtention.Models
{
    [Table("admin_locks")]
    public class AdminLock
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        // ВНИМАНИЕ: В схеме это nvarchar(max), а не datetime!
        [Column("locked_at")]
        public string LockedAt { get; set; } = string.Empty;
        [Column("locked_by")]
        public string LockedBy { get; set; } = string.Empty;

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }
    }
}
