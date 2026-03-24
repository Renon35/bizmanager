using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/purchase-orders")]
public class PurchaseOrdersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.PurchaseOrders
            .Include(o => o.Dealer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Shipment)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var order = await db.PurchaseOrders
            .Include(o => o.Dealer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Shipment)
            .FirstOrDefaultAsync(o => o.Id == id);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PurchaseOrderRequest req)
    {
        var order = new PurchaseOrder
        {
            OrderNumber = req.OrderNumber,
            DealerId = req.DealerId,
            OrderDate = req.OrderDate,
            Status = req.Status
        };
        foreach (var item in req.Items)
        {
            item.TotalPrice = item.Quantity * item.UnitPrice;
            order.Items.Add(item);
        }
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] PurchaseOrderRequest req)
    {
        var order = await db.PurchaseOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        order.OrderNumber = req.OrderNumber;
        order.DealerId = req.DealerId;
        order.OrderDate = req.OrderDate;
        order.Status = req.Status;
        db.OrderItems.RemoveRange(order.Items);
        order.Items.Clear();
        foreach (var item in req.Items)
        {
            item.TotalPrice = item.Quantity * item.UnitPrice;
            order.Items.Add(item);
        }
        await db.SaveChangesAsync();
        return Ok(order);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await db.PurchaseOrders.FindAsync(id);
        if (order is null) return NotFound();
        db.PurchaseOrders.Remove(order);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record PurchaseOrderRequest(
    string OrderNumber,
    int DealerId,
    DateTime OrderDate,
    string Status,
    List<OrderItem> Items
);
