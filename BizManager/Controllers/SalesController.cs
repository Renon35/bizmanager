using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/sales")]
public class SalesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.Sales
            .Include(s => s.Customer)
            .Include(s => s.SalesRep)
            .Include(s => s.Quotation)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var sale = await db.Sales
            .Include(s => s.Customer)
            .Include(s => s.SalesRep)
            .Include(s => s.Quotation)
            .FirstOrDefaultAsync(s => s.Id == id);
        return sale is null ? NotFound() : Ok(sale);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Sale sale)
    {
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = sale.Id }, sale);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Sale updated)
    {
        var sale = await db.Sales.FindAsync(id);
        if (sale is null) return NotFound();
        sale.CustomerId = updated.CustomerId;
        sale.SalesRepId = updated.SalesRepId;
        sale.SaleDate = updated.SaleDate;
        sale.TotalPrice = updated.TotalPrice;
        sale.QuotationId = updated.QuotationId;
        await db.SaveChangesAsync();
        return Ok(sale);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var sale = await db.Sales.FindAsync(id);
        if (sale is null) return NotFound();
        db.Sales.Remove(sale);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
