namespace BizManager.Models;

public class PurchaseOrder
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int DealerId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    // "preparing" | "shipped" | "delivered"
    public string Status { get; set; } = "preparing";

    public Dealer? Dealer { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public Shipment? Shipment { get; set; }
    public DealerInvoice? DealerInvoice { get; set; }
}
