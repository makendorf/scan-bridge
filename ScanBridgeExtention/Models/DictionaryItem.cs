using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanBridgeExtention.Models
{
    [Table("dictionaries")]
    public class DictionaryItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [MaxLength(255)]
        [Column("dict_name")]
        public string DictName { get; set; } = string.Empty;

        [MaxLength(255)]
        [Column("value")]
        public string Value { get; set; } = string.Empty;

        [Column("sort_order")]
        public int? SortOrder { get; set; }
    }
}
