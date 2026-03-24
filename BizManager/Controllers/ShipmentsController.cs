using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/shipments")]
public class ShipmentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.Shipments
            .Include(s => s.Order).ThenInclude(o => o!.Dealer)
            .OrderByDescending(s => s.ShipmentDate)
            .ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var shipment = await db.Shipments.Include(s => s.Order).FirstOrDefaultAsync(s => s.Id == id);
        return shipment is null ? NotFound() : Ok(shipment);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Shipment shipment)
    {
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = shipment.Id }, shipment);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Shipment updated)
    {
        var shipment = await db.Shipments.FindAsync(id);
        if (shipment is null) return NotFound();
        shipment.OrderId = updated.OrderId;
        shipment.ShippingCompany = updated.ShippingCompany;
        shipment.TrackingNumber = updated.TrackingNumber;
        shipment.ShipmentDate = updated.ShipmentDate;
        shipment.DeliveryStatus = updated.DeliveryStatus;
        await db.SaveChangesAsync();
        return Ok(shipment);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var shipment = await db.Shipments.FindAsync(id);
        if (shipment is null) return NotFound();
        db.Shipments.Remove(shipment);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
