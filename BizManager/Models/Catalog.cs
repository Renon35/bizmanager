namespace BizManager.Models;

public class Catalog
{
    public int Id { get; set; }
    public int BrandId { get; set; }
    public string CatalogName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Brand? Brand { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Collection> Collections { get; set; } = new List<Collection>();
}
