namespace BizManager.Models;

public class Quotation
{
    public int Id { get; set; }
    public string QuotationNumber { get; set; } = string.Empty;
    public int SalesRepId { get; set; }
    public int CustomerId { get; set; }
    public int? DealerId { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public decimal TotalPrice { get; set; } // Let's keep this or use GrandTotal. Will keep for backwards compat.
    public decimal Subtotal { get; set; }
    public decimal VatRate { get; set; } = 20.0m;
    public decimal VatAmount { get; set; }
    public decimal GrandTotal { get; set; }

    public SalesRep? SalesRep { get; set; }
    public Customer? Customer { get; set; }
    public Dealer? Dealer { get; set; }
    public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
    public Sale? Sale { get; set; }
}
