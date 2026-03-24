using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/sales-shipments")]
public class SalesShipmentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var shipments = await db.SalesShipments
            .Include(s => s.SalesOrder).ThenInclude(so => so.Customer)
            .Include(s => s.DeliveryItems).ThenInclude(di => di.Product)
            .OrderByDescending(s => s.ShipmentDate)
            .ToListAsync();
        return Ok(shipments);
    }

    [HttpGet("pending-deliveries")]
    public async Task<IActionResult> GetPendingDeliveries()
    {
        var pendingItems = await db.DeliveryItems
            .Include(di => di.Product)
            .Include(di => di.SalesShipment).ThenInclude(ss => ss.SalesOrder)
            .Where(di => di.MissingQuantity > 0 && di.Status != "complete")
            .OrderBy(di => di.ExpectedDeliveryDate)
            .ToListAsync();
            
        return Ok(pendingItems.Select(di => new {
            di.Id,
            ProductName = di.Product?.ProductName,
            di.MissingQuantity,
            di.ExpectedDeliveryDate,
            di.Note,
            OrderNumber = di.SalesShipment?.SalesOrder?.OrderNumber
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var shipment = await db.SalesShipments
            .Include(s => s.SalesOrder).ThenInclude(o => o.Customer)
            .Include(s => s.DeliveryItems).ThenInclude(di => di.Product)
            .FirstOrDefaultAsync(s => s.Id == id);
            
        return shipment is null ? NotFound() : Ok(shipment);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SalesShipmentRequest req)
    {
        var order = await db.SalesOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == req.SalesOrderId);
            
        if (order is null) return BadRequest("Sales Order not found.");

        var shipment = new SalesShipment
        {
            SalesOrderId = req.SalesOrderId,
            ShipmentDate = req.ShipmentDate,
            ShippingCompany = req.ShippingCompany,
            TrackingNumber = req.TrackingNumber,
            Status = "complete" // Start with assumption of complete
        };

        bool hasMissing = false;

        foreach (var itemReq in req.DeliveryItems)
        {
            var orderItem = order.Items.FirstOrDefault(i => i.ProductId == itemReq.ProductId);
            if (orderItem == null) continue; // product wasn't ordered

            int missingQty = itemReq.OrderedQuantity - itemReq.DeliveredQuantity;
            if (missingQty < 0) missingQty = 0; // Prevent negative missing if over-delivered somehow

            var status = missingQty > 0 ? "partial" : "complete";
            if (missingQty > 0) hasMissing = true;

            shipment.DeliveryItems.Add(new DeliveryItem
            {
                ProductId = itemReq.ProductId,
                OrderedQuantity = itemReq.OrderedQuantity,
                DeliveredQuantity = itemReq.DeliveredQuantity,
                MissingQuantity = missingQty,
                ExpectedDeliveryDate = itemReq.ExpectedDeliveryDate,
                Note = itemReq.Note,
                Status = status
            });
        }

        if (hasMissing)
        {
            shipment.Status = "partial";
            order.Status = "partial";
        }
        else
        {
            order.Status = "complete";
        }

        db.SalesShipments.Add(shipment);
        await db.SaveChangesAsync();
        
        return CreatedAtAction(nameof(Get), new { id = shipment.Id }, shipment);
    }

    [HttpPut("delivery-item/{id}")]
    public async Task<IActionResult> UpdateDeliveryItem(int id, [FromBody] UpdateDeliveryItemRequest req)
    {
        var item = await db.DeliveryItems.FindAsync(id);
        if (item == null) return NotFound();

        item.ExpectedDeliveryDate = req.ExpectedDeliveryDate;
        item.Note = req.Note;
        
        // If they updated delivered quantity directly to resolve a missing
        if (req.DeliveredQuantity.HasValue)
        {
            item.DeliveredQuantity = req.DeliveredQuantity.Value;
            item.MissingQuantity = item.OrderedQuantity - item.DeliveredQuantity;
            if (item.MissingQuantity <= 0)
            {
                item.MissingQuantity = 0;
                item.Status = "complete";
            }
        }

        await db.SaveChangesAsync();
        return Ok(item);
    }
}

public record SalesShipmentRequest(
    int SalesOrderId,
    DateTime ShipmentDate,
    string? ShippingCompany,
    string? TrackingNumber,
    List<DeliveryItemRequest> DeliveryItems
);

public record DeliveryItemRequest(
    int ProductId,
    int OrderedQuantity,
    int DeliveredQuantity,
    DateTime? ExpectedDeliveryDate,
    string? Note
);

public record UpdateDeliveryItemRequest(
    DateTime? ExpectedDeliveryDate,
    string? Note,
    int? DeliveredQuantity
);
