namespace BizManager.Models;

public class Collection
{
    public int Id { get; set; }
    public int CatalogId { get; set; }
    public string CollectionName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Catalog? Catalog { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
