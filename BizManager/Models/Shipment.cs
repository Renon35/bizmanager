namespace BizManager.Models;

public class Shipment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string? ShippingCompany { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? ShipmentDate { get; set; }
    public string? DeliveryStatus { get; set; }

    public PurchaseOrder? Order { get; set; }
}
