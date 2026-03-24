namespace BizManager.Models;

public class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string CodeStructure { get; set; } = "single_code";
    public string? WebsiteDomain { get; set; }

    public ICollection<Dealer> Dealers { get; set; } = new List<Dealer>();
    
    [System.Text.Json.Serialization.JsonIgnore]
    public ICollection<BrandCatalog> Catalogs { get; set; } = new List<BrandCatalog>();
}
