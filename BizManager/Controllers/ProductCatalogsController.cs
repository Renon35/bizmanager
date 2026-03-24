using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/catalogs")]
public class ProductCatalogsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? brandId)
    {
        var query = db.Catalogs.AsQueryable();
        
        if (brandId.HasValue)
        {
            query = query.Where(c => c.BrandId == brandId.Value);
        }

        var catalogs = await query
            .Include(c => c.Brand)
            .OrderBy(c => c.CatalogName)
            .Select(c => new
            {
                c.Id,
                c.CatalogName,
                c.Description,
                c.BrandId,
                c.CreatedAt,
                BrandName = c.Brand != null ? c.Brand.Name : null
            })
            .ToListAsync();

        return Ok(catalogs);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Catalog catalog)
    {
        var brand = await db.Brands.FindAsync(catalog.BrandId);
        if (brand == null) return BadRequest("Brand not found.");

        db.Catalogs.Add(catalog);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = catalog.Id }, catalog);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Catalog request)
    {
        var catalog = await db.Catalogs.FindAsync(id);
        if (catalog == null) return NotFound();

        catalog.CatalogName = request.CatalogName;
        catalog.Description = request.Description;
        catalog.BrandId = request.BrandId;

        await db.SaveChangesAsync();
        return Ok(catalog);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var catalog = await db.Catalogs.FindAsync(id);
        if (catalog == null) return NotFound();

        db.Catalogs.Remove(catalog);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
