using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/dealer-stock")]
public class DealerStockController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.DealerProducts
            .Include(dp => dp.Dealer)
            .Include(dp => dp.Product).ThenInclude(p => p!.Catalog).ThenInclude(c => c!.Brand)
            .OrderBy(dp => dp.Dealer!.Name)
            .ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var dp = await db.DealerProducts
            .Include(x => x.Dealer)
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        return dp is null ? NotFound() : Ok(dp);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DealerProduct dp)
    {
        dp.LastUpdated = DateTime.UtcNow;
        db.DealerProducts.Add(dp);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = dp.Id }, dp);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] DealerProduct updated)
    {
        var dp = await db.DealerProducts.FindAsync(id);
        if (dp is null) return NotFound();
        dp.DealerId = updated.DealerId;
        dp.ProductId = updated.ProductId;
        dp.StockQuantity = updated.StockQuantity;
        dp.UnitPrice = updated.UnitPrice;
        dp.LastUpdated = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(dp);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var dp = await db.DealerProducts.FindAsync(id);
        if (dp is null) return NotFound();
        db.DealerProducts.Remove(dp);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
