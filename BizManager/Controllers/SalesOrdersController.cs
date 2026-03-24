using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/sales-orders")]
public class SalesOrdersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await db.SalesOrders
            .Include(o => o.Customer)
            .Include(o => o.SalesRep)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var order = await db.SalesOrders
            .Include(o => o.Customer)
            .Include(o => o.SalesRep)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Shipments).ThenInclude(s => s.DeliveryItems).ThenInclude(di => di.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
            
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SalesOrderRequest req)
    {
        var order = new SalesOrder
        {
            OrderNumber = req.OrderNumber,
            CustomerId = req.CustomerId,
            SalesRepId = req.SalesRepId,
            OrderDate = req.OrderDate,
            Status = "pending"
        };
        
        foreach (var item in req.Items)
        {
            var product = await db.Products.FindAsync(item.ProductId);
            if (product == null) return BadRequest($"Product {item.ProductId} not found");

            order.Items.Add(new SalesOrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.Quantity * item.UnitPrice
            });
        }

        db.SalesOrders.Add(order);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SalesOrderRequest req)
    {
        var order = await db.SalesOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
            
        if (order is null) return NotFound();

        order.OrderNumber = req.OrderNumber;
        order.CustomerId = req.CustomerId;
        order.SalesRepId = req.SalesRepId;
        order.OrderDate = req.OrderDate;

        db.SalesOrderItems.RemoveRange(order.Items);
        foreach (var item in req.Items)
        {
            order.Items.Add(new SalesOrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.Quantity * item.UnitPrice
            });
        }

        await db.SaveChangesAsync();
        return Ok(order);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await db.SalesOrders.FindAsync(id);
        if (order is null) return NotFound();
        db.SalesOrders.Remove(order);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record SalesOrderRequest(
    string OrderNumber, 
    int CustomerId, 
    int SalesRepId, 
    DateTime OrderDate, 
    List<SalesOrderItemRequest> Items);

public record SalesOrderItemRequest(int ProductId, int Quantity, decimal UnitPrice);
