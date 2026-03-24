using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BizManager.Models;

[Table("products_import_preview")]
public class ProductImportPreview
{
    [Key]
    public int Id { get; set; }

    [Column("product_code")]
    [MaxLength(100)]
    public string? ProductCode { get; set; }

    [Column("mold_code")]
    [MaxLength(100)]
    public string? MoldCode { get; set; }

    [Column("barcode")]
    [MaxLength(100)]
    public string? Barcode { get; set; }

    [Column("product_name")]
    [MaxLength(255)]
    public string? ProductName { get; set; }

    [Column("collection")]
    [MaxLength(255)]
    public string? Collection { get; set; }

    [Column("price")]
    public decimal Price { get; set; }

    [Column("stock")]
    public int Stock { get; set; }

    [Column("is_header")]
    public bool IsHeader { get; set; }

    [Column("status")]
    [MaxLength(255)]
    public string Status { get; set; } = "Ready";
    
    // Links to identify the import session context
    [Column("brand_id")]
    public int BrandId { get; set; }
    
    [Column("catalog_id")]
    public int? CatalogId { get; set; }
    
    [Column("dealer_id")]
    public int? DealerId { get; set; }
    
    [Column("price_type")]
    [MaxLength(50)]
    public string PriceType { get; set; } = "purchase_price";
}
