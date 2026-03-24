namespace BizManager.Models;

public class Sale
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int SalesRepId { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.UtcNow;
    public decimal TotalPrice { get; set; }
    public int? QuotationId { get; set; }

    public Customer? Customer { get; set; }
    public SalesRep? SalesRep { get; set; }
    public Quotation? Quotation { get; set; }
    public CustomerInvoice? CustomerInvoice { get; set; }
}
