namespace BizManager.Models;

public class DealerInvoice
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public bool Issued { get; set; } = false;
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public string? FilePath { get; set; }

    public PurchaseOrder? Order { get; set; }
}
