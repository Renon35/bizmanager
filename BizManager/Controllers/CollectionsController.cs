using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/catalogs/{catalogId}/collections")]
public class CollectionsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(int catalogId)
    {
        var collections = await db.Collections
            .Where(c => c.CatalogId == catalogId)
            .OrderBy(c => c.CollectionName)
            .ToListAsync();
        return Ok(collections);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int catalogId, int id)
    {
        var collection = await db.Collections
            .FirstOrDefaultAsync(c => c.CatalogId == catalogId && c.Id == id);
        return collection is null ? NotFound() : Ok(collection);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int catalogId, [FromBody] Collection collection)
    {
        var catalog = await db.Catalogs.FindAsync(catalogId);
        if (catalog is null) return NotFound("Catalog not found");

        collection.CatalogId = catalogId;
        db.Collections.Add(collection);
        await db.SaveChangesAsync();
        
        return CreatedAtAction(nameof(Get), new { catalogId, id = collection.Id }, collection);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int catalogId, int id, [FromBody] Collection updated)
    {
        var collection = await db.Collections
            .FirstOrDefaultAsync(c => c.CatalogId == catalogId && c.Id == id);
            
        if (collection is null) return NotFound();

        collection.CollectionName = updated.CollectionName;
        collection.Description = updated.Description;
        
        await db.SaveChangesAsync();
        return Ok(collection);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int catalogId, int id)
    {
        var collection = await db.Collections
            .FirstOrDefaultAsync(c => c.CatalogId == catalogId && c.Id == id);
            
        if (collection is null) return NotFound();

        db.Collections.Remove(collection);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
