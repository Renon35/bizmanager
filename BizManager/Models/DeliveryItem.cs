namespace BizManager.Models;

public class DeliveryItem
{
    public int Id { get; set; }
    public int SalesShipmentId { get; set; }
    public int ProductId { get; set; }
    
    public int OrderedQuantity { get; set; }
    public int DeliveredQuantity { get; set; }
    public int MissingQuantity { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Note { get; set; }

    // status options: "complete" | "partial" | "missing"
    public string Status { get; set; } = "complete";

    public SalesShipment? SalesShipment { get; set; }
    public Product? Product { get; set; }
}
