namespace BizManager.Models;

public class SalesShipment
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public DateTime ShipmentDate { get; set; } = DateTime.UtcNow;
    // status options: "partial" | "complete" | "missing"
    public string Status { get; set; } = "partial";
    public string? ShippingCompany { get; set; }
    public string? TrackingNumber { get; set; }

    public SalesOrder? SalesOrder { get; set; }
    public ICollection<DeliveryItem> DeliveryItems { get; set; } = new List<DeliveryItem>();
}
