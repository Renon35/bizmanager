namespace BizManager.Models;

public class DealerProduct
{
    public int Id { get; set; }
    public int DealerId { get; set; }
    public int ProductId { get; set; }
    public int StockQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public Dealer? Dealer { get; set; }
    public Product? Product { get; set; }
}
