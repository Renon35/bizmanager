namespace BizManager.Models;

public class QuotationItem
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? ImageUrl { get; set; }

    public Quotation? Quotation { get; set; }
}
