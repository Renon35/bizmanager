namespace BizManager.Models;

public class Product
{
    public int Id { get; set; }
    public int? CatalogId { get; set; }
    public int? CollectionId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public string? PackageType { get; set; }
    public int? UnitsPerCase { get; set; }   // Koli içi adet
    public int? UnitsPerPack { get; set; }   // Paket içi adet
    public string? ImageUrl { get; set; }
    public bool HasMissingImage { get; set; }
    
    public string? MoldCode { get; set; }
    public string? Barcode { get; set; }
    
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal ListPrice { get; set; }

    public Catalog? Catalog { get; set; }
    public Collection? Collection { get; set; }
    public ICollection<DealerProduct> DealerProducts { get; set; } = new List<DealerProduct>();
}
