using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/brands/{brandId}/catalogs")]
public class CatalogsController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private string GetUploadsDir()
    {
        var path = Path.Combine(env.WebRootPath, "uploads", "catalogs");
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    [HttpGet]
    public async Task<IActionResult> GetByBrand(int brandId)
    {
        var brand = await db.Brands.FindAsync(brandId);
        if (brand is null) return NotFound("Brand not found");

        var catalogs = await db.BrandCatalogs
            .Where(c => c.BrandId == brandId)
            .OrderByDescending(c => c.UploadedAt)
            .ToListAsync();
            
        return Ok(catalogs);
    }

    [HttpPost]
    public async Task<IActionResult> Upload(int brandId, [FromForm] IFormFile file, [FromForm] string customFileName)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var brand = await db.Brands.FindAsync(brandId);
        if (brand is null) return NotFound("Brand not found");

        var ext = Path.GetExtension(file.FileName);
        var safeFileName = Guid.NewGuid().ToString("N") + ext;
        var filePath = Path.Combine(GetUploadsDir(), safeFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var catalog = new BrandCatalog
        {
            BrandId = brandId,
            OriginalFileName = file.FileName,
            CustomFileName = string.IsNullOrWhiteSpace(customFileName) ? file.FileName : customFileName,
            FilePath = $"/uploads/catalogs/{safeFileName}",
            UploadedAt = DateTime.UtcNow
        };

        db.BrandCatalogs.Add(catalog);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByBrand), new { brandId }, catalog);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Rename(int brandId, int id, [FromBody] RenameCatalogDto dto)
    {
        var catalog = await db.BrandCatalogs.FirstOrDefaultAsync(c => c.Id == id && c.BrandId == brandId);
        if (catalog is null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.NewName))
            return BadRequest("New name cannot be empty.");

        catalog.CustomFileName = dto.NewName;
        await db.SaveChangesAsync();

        return Ok(catalog);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int brandId, int id)
    {
        var catalog = await db.BrandCatalogs.FirstOrDefaultAsync(c => c.Id == id && c.BrandId == brandId);
        if (catalog is null) return NotFound();

        // Delete physical file
        var fullPath = Path.Combine(env.WebRootPath, catalog.FilePath.TrimStart('/'));
        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }

        db.BrandCatalogs.Remove(catalog);
        await db.SaveChangesAsync();

        return NoContent();
    }
}

public class RenameCatalogDto
{
    public string NewName { get; set; } = string.Empty;
}
