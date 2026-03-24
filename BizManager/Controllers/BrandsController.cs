using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;
using BizManager.Services;

namespace BizManager.Controllers;

[ApiController]
[Route("api/brands")]
public class BrandsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.Brands.OrderBy(b => b.Name).ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var brand = await db.Brands.FindAsync(id);
        return brand is null ? NotFound() : Ok(brand);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] string name, [FromForm] string? description, [FromForm] string? codeStructure, [FromForm] string? websiteDomain, IFormFile? logo,
        [FromServices] IWebHostEnvironment env, [FromServices] SupabaseStorageService storage)
    {
        var brand = new Brand { Name = name, Description = description, CodeStructure = codeStructure ?? "single_code", WebsiteDomain = websiteDomain };
        if (logo != null)
            brand.LogoPath = await SaveLogoAsync(logo, null, env, storage);
        db.Brands.Add(brand);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = brand.Id }, brand);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromForm] string name, [FromForm] string? description, [FromForm] string? codeStructure, [FromForm] string? websiteDomain, IFormFile? logo,
        [FromServices] IWebHostEnvironment env, [FromServices] SupabaseStorageService storage)
    {
        var brand = await db.Brands.FindAsync(id);
        if (brand is null) return NotFound();
        brand.Name = name;
        brand.Description = description;
        brand.CodeStructure = codeStructure ?? "single_code";
        brand.WebsiteDomain = websiteDomain;
        if (logo != null)
            brand.LogoPath = await SaveLogoAsync(logo, brand.LogoPath, env, storage);
        await db.SaveChangesAsync();
        return Ok(brand);
    }

    private static async Task<string> SaveLogoAsync(IFormFile logo, string? oldUrl,
        IWebHostEnvironment env, SupabaseStorageService storage)
    {
        var ext = Path.GetExtension(logo.FileName);
        var fileName = $"{Guid.NewGuid()}{ext}";
        using var ms = new MemoryStream();
        await logo.CopyToAsync(ms);
        ms.Position = 0;

        if (storage.IsConfigured)
        {
            var oldPath = SupabaseStorageService.ExtractObjectPath(oldUrl, "product-images");
            if (oldPath != null) await storage.DeleteByUrlAsync("product-images", oldPath);
            return await storage.UploadAsync("product-images", $"logos/{fileName}", ms,
                SupabaseStorageService.GetContentType(ext));
        }
        else
        {
            var dir = Path.Combine(env.WebRootPath, "uploads");
            Directory.CreateDirectory(dir);
            if (!string.IsNullOrEmpty(oldUrl) && oldUrl.StartsWith("/uploads"))
            {
                var oldLocal = Path.Combine(env.WebRootPath, oldUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(oldLocal)) System.IO.File.Delete(oldLocal);
            }
            await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, fileName), ms.ToArray());
            return $"/uploads/{fileName}";
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var brand = await db.Brands.FindAsync(id);
        if (brand is null) return NotFound();
        db.Brands.Remove(brand);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
