using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanBridgeExtention.Models
{
    [Table("nk_settings")]
    public class NkSetting
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("api_key")]
        public string? ApiKey { get; set; }
        [Column("base_url")]
        public string? BaseUrl { get; set; }
        [Column("enabled")]
        public int? Enabled { get; set; }
        [Column("last_check")]
        public string? LastCheck { get; set; }
        [Column("last_error")]
        public string? LastError { get; set; }
        [Column("last_checked_count")]
        public int? LastCheckedCount { get; set; }
    }
}
