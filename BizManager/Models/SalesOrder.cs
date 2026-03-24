namespace BizManager.Models;

public class SalesOrder
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int SalesRepId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    // status options: "pending" | "partial" | "complete"
    public string Status { get; set; } = "pending";

    public Customer? Customer { get; set; }
    public SalesRep? SalesRep { get; set; }
    public ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
    public ICollection<SalesShipment> Shipments { get; set; } = new List<SalesShipment>();
}
