using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanBridgeExtention.Models
{
    [Table("user_confirmations")]
    public class UserConfirmation
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [MaxLength(255)]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        // ВНИМАНИЕ: В схеме это nvarchar(max). Если там хранится дата, её нужно будет парсить.
        [Column("confirmed_at")]
        public string ConfirmedAt { get; set; } = string.Empty;

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }
    }
}
