namespace BizManager.Models;

public class CustomerInvoice
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public bool Issued { get; set; } = false;
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public string? FilePath { get; set; }

    public Sale? Sale { get; set; }
}
