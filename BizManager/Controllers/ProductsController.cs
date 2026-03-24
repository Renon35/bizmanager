using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;
using BizManager.Models.DTOs;
using BizManager.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace BizManager.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? brandId, [FromQuery] int? catalogId, [FromQuery] int? collectionId, [FromQuery] string? q)
    {
        var query = db.Products
            .Include(p => p.Collection)
            .Include(p => p.Catalog).ThenInclude(c => c.Brand)
            .Include(p => p.DealerProducts).ThenInclude(dp => dp.Dealer)
            .AsQueryable();

        if (brandId.HasValue)
            query = query.Where(p => p.Catalog != null && p.Catalog.BrandId == brandId.Value);
        
        if (catalogId.HasValue)
            query = query.Where(p => p.CatalogId == catalogId.Value);
            
        if (collectionId.HasValue)
            query = query.Where(p => p.CollectionId == collectionId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var lowerQ = q.ToLower();
            query = query.Where(p => 
                p.ProductName.ToLower().Contains(lowerQ) ||
                (p.ProductCode != null && p.ProductCode.ToLower().Contains(lowerQ)) ||
                (p.MoldCode != null && p.MoldCode.ToLower().Contains(lowerQ)) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(lowerQ))
            );
        }

        var products = await query.OrderBy(p => p.ProductName).ToListAsync();
        return Ok(products);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var product = await db.Products
            .Include(p => p.Collection)
            .Include(p => p.Catalog)
            .ThenInclude(c => c.Brand)
            .Include(p => p.DealerProducts)
            .ThenInclude(dp => dp.Dealer)
            .FirstOrDefaultAsync(p => p.Id == id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Product product)
    {
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Product updated)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        
        product.CatalogId = updated.CatalogId;
        product.CollectionId = updated.CollectionId;
        product.ProductName = updated.ProductName;
        product.ProductCode = updated.ProductCode;
        product.MoldCode = updated.MoldCode;
        product.Barcode = updated.Barcode;
        product.PackageType = updated.PackageType;
        product.UnitsPerCase = updated.UnitsPerCase;
        product.UnitsPerPack = updated.UnitsPerPack;
        
        product.PurchasePrice = updated.PurchasePrice;
        product.SalePrice = updated.SalePrice;
        product.ListPrice = updated.ListPrice;
        
        await db.SaveChangesAsync();
        return Ok(product);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("export-pdf")]
    public async Task<IActionResult> ExportPdf([FromServices] BizManager.Services.PdfService pdfService, 
        [FromQuery] int? brandId, [FromQuery] int? catalogId, [FromQuery] int? collectionId, [FromQuery] string? q, [FromQuery] string? selectedIds)
    {
        var query = db.Products
            .Include(p => p.Collection)
            .Include(p => p.Catalog).ThenInclude(c => c.Brand)
            .Include(p => p.DealerProducts).ThenInclude(dp => dp.Dealer)
            .AsQueryable();

        // If specific IDs are selected, ignore other filters
        if (!string.IsNullOrWhiteSpace(selectedIds))
        {
            var idList = selectedIds.Split(',').Select(id => int.TryParse(id, out var parsed) ? parsed : 0).Where(id => id > 0).ToList();
            query = query.Where(p => idList.Contains(p.Id));
        }
        else
        {
            if (brandId.HasValue)
                query = query.Where(p => p.Catalog != null && p.Catalog.BrandId == brandId.Value);
            
            if (catalogId.HasValue)
                query = query.Where(p => p.CatalogId == catalogId.Value);
                
            if (collectionId.HasValue)
                query = query.Where(p => p.CollectionId == collectionId.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var lowerQ = q.ToLower();
                query = query.Where(p => 
                    p.ProductName.ToLower().Contains(lowerQ) ||
                    (p.ProductCode != null && p.ProductCode.ToLower().Contains(lowerQ)) ||
                    (p.MoldCode != null && p.MoldCode.ToLower().Contains(lowerQ)) ||
                    (p.Barcode != null && p.Barcode.ToLower().Contains(lowerQ))
                );
            }
        }

        var products = await query.OrderBy(p => p.ProductName).ToListAsync();

        // Determine names for the header
        string brandName = "Tümü";
        if (brandId.HasValue)
        {
            var b = await db.Brands.FindAsync(brandId.Value);
            if (b != null) brandName = b.Name;
        }

        string? catName = null;
        if (catalogId.HasValue)
        {
            var c = await db.Catalogs.FindAsync(catalogId.Value);
            if (c != null) catName = c.CatalogName;
        }

        var pdfBytes = pdfService.GenerateProductListPdf(products, brandName, catName);
        
        string safeBrandName = brandName.Replace(" ", "-").ToLowerInvariant();
        string fileName = $"{safeBrandName}-üürün-listesi-{DateTime.Now:yyyy-MM-dd}.pdf";

        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpGet("by-code")]
    public async Task<IActionResult> GetByCode([FromQuery] string? code, [FromQuery] string? moldCode, [FromQuery] string? barcode)
    {
        var query = db.Products
            .Include(p => p.Collection)
            .Include(p => p.Catalog).ThenInclude(c => c.Brand)
            .Include(p => p.DealerProducts)
            .AsQueryable();

        if (!string.IsNullOrEmpty(barcode))
            query = query.Where(p => p.Barcode == barcode);
        else if (!string.IsNullOrEmpty(moldCode) && !string.IsNullOrEmpty(code))
            query = query.Where(p => p.MoldCode == moldCode && p.ProductCode == code);
        else if (!string.IsNullOrEmpty(code))
            query = query.Where(p => p.ProductCode == code);
        else
            return BadRequest("Must provide product identification.");

        var product = await query.FirstOrDefaultAsync();
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost("bulk-images")]
    public async Task<IActionResult> UploadBulkImages(List<IFormFile> images,
        [FromServices] IWebHostEnvironment env,
        [FromServices] SupabaseStorageService storage)
    {
        if (images == null || images.Count == 0) return BadRequest("Görsel yüklenmedi.");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        int matchedCount = 0;
        int skippedCount = 0;

        foreach (var image in images)
        {
            var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                skippedCount++;
                continue;
            }

            string baseName = Path.GetFileNameWithoutExtension(image.FileName).Trim();
            var product = await db.Products.FirstOrDefaultAsync(p =>
                (p.ProductCode != null && p.ProductCode.ToLower() == baseName.ToLower()) ||
                (p.Barcode != null && p.Barcode.ToLower() == baseName.ToLower()));

            if (product == null) { skippedCount++; continue; }

            var newFileName = $"{product.Id}_{Guid.NewGuid()}{ext}";

            using var ms = new MemoryStream();
            using (var stream = image.OpenReadStream())
            using (var img = await SixLabors.ImageSharp.Image.LoadAsync(stream))
            {
                if (img.Width > 300 || img.Height > 300)
                    img.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(300, 300) }));
                await img.SaveAsync(ms, SixLabors.ImageSharp.Formats.Jpeg.JpegFormat.Instance);
            }
            ms.Position = 0;

            if (storage.IsConfigured)
            {
                // Delete old image from Supabase
                var oldPath = SupabaseStorageService.ExtractObjectPath(product.ImageUrl, "product-images");
                if (oldPath != null) await storage.DeleteByUrlAsync("product-images", oldPath);

                product.ImageUrl = await storage.UploadAsync("product-images", $"products/{newFileName}", ms, "image/jpeg");
            }
            else
            {
                // Local fallback (dev only)
                var dir = Path.Combine(env.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(dir);
                var filePath = Path.Combine(dir, newFileName);
                await System.IO.File.WriteAllBytesAsync(filePath, ms.ToArray());
                product.ImageUrl = $"/uploads/products/{newFileName}";
            }
            matchedCount++;
        }

        await db.SaveChangesAsync();
        return Ok(new { matched = matchedCount, skipped = skippedCount });
    }

    [HttpPost("{id}/image")]
    public async Task<IActionResult> UploadImage(int id, IFormFile image,
        [FromServices] IWebHostEnvironment env,
        [FromServices] SupabaseStorageService storage)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        if (image == null || image.Length == 0) return BadRequest("No image provided.");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext)) return BadRequest("Invalid file extension.");

        var fileName = $"{id}_{Guid.NewGuid()}.jpg";

        using var ms = new MemoryStream();
        using (var stream = image.OpenReadStream())
        using (var img = await SixLabors.ImageSharp.Image.LoadAsync(stream))
        {
            if (img.Width > 300 || img.Height > 300)
                img.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(300, 300) }));
            await img.SaveAsync(ms, SixLabors.ImageSharp.Formats.Jpeg.JpegFormat.Instance);
        }
        ms.Position = 0;

        if (storage.IsConfigured)
        {
            var oldPath = SupabaseStorageService.ExtractObjectPath(product.ImageUrl, "product-images");
            if (oldPath != null) await storage.DeleteByUrlAsync("product-images", oldPath);
            product.ImageUrl = await storage.UploadAsync("product-images", $"products/{fileName}", ms, "image/jpeg");
        }
        else
        {
            var dir = Path.Combine(env.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, fileName);
            await System.IO.File.WriteAllBytesAsync(filePath, ms.ToArray());
            // Clean up old local file
            if (!string.IsNullOrEmpty(product.ImageUrl) && product.ImageUrl.StartsWith("/uploads"))
            {
                var oldPath = Path.Combine(env.WebRootPath, product.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }
            product.ImageUrl = $"/uploads/products/{fileName}";
        }

        await db.SaveChangesAsync();
        return Ok(product);
    }

    [HttpDelete("{id}/image")]
    public async Task<IActionResult> DeleteImage(int id,
        [FromServices] IWebHostEnvironment env,
        [FromServices] SupabaseStorageService storage)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();

        if (!string.IsNullOrEmpty(product.ImageUrl))
        {
            if (storage.IsConfigured)
            {
                var objPath = SupabaseStorageService.ExtractObjectPath(product.ImageUrl, "product-images");
                if (objPath != null) await storage.DeleteByUrlAsync("product-images", objPath);
            }
            else
            {
                var localPath = Path.Combine(env.WebRootPath, product.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(localPath)) System.IO.File.Delete(localPath);
            }
            product.ImageUrl = null;
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request,
        [FromServices] IWebHostEnvironment env,
        [FromServices] SupabaseStorageService storage)
    {
        if (request.ProductIds == null || !request.ProductIds.Any()) return BadRequest("Hiçbir ürün seçilmedi.");

        var products = await db.Products.Where(p => request.ProductIds.Contains(p.Id)).ToListAsync();

        foreach (var product in products)
        {
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                if (storage.IsConfigured)
                {
                    var objPath = SupabaseStorageService.ExtractObjectPath(product.ImageUrl, "product-images");
                    if (objPath != null) await storage.DeleteByUrlAsync("product-images", objPath);
                }
                else
                {
                    var localPath = Path.Combine(env.WebRootPath, product.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(localPath)) System.IO.File.Delete(localPath);
                }
            }
        }

        db.Products.RemoveRange(products);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{products.Count} ürün silindi." });
    }

    [HttpPost("bulk/update-price")]
    public async Task<IActionResult> BulkUpdatePrice([FromBody] BulkUpdatePriceRequest request)
    {
        if (request.ProductIds == null || !request.ProductIds.Any()) return BadRequest("Hiçbir ürün seçilmedi.");

        var products = await db.Products.Where(p => request.ProductIds.Contains(p.Id)).ToListAsync();
        
        decimal multiplier = 1 + (request.Percentage / 100m);

        foreach (var product in products)
        {
            product.PurchasePrice = Math.Round(product.PurchasePrice * multiplier, 2);
            product.SalePrice = Math.Round(product.SalePrice * multiplier, 2);
            product.ListPrice = Math.Round(product.ListPrice * multiplier, 2);
        }

        await db.SaveChangesAsync();

        return Ok(new { message = $"{products.Count} ürünün fiyatı güncellendi." });
    }

    [HttpPost("bulk/update-collection")]
    public async Task<IActionResult> BulkUpdateCollection([FromBody] BulkUpdateCollectionRequest request)
    {
        if (request.ProductIds == null || !request.ProductIds.Any()) return BadRequest("Hiçbir ürün seçilmedi.");

        if (request.NewCollectionId.HasValue)
        {
            var collectionExists = await db.Collections.AnyAsync(c => c.Id == request.NewCollectionId.Value);
            if (!collectionExists) return BadRequest("Geçersiz koleksiyon.");
        }

        var products = await db.Products.Where(p => request.ProductIds.Contains(p.Id)).ToListAsync();
        
        foreach (var product in products)
        {
            product.CollectionId = request.NewCollectionId;
        }

        await db.SaveChangesAsync();

        return Ok(new { message = $"{products.Count} ürün yeni koleksiyona taşındı." });
    }

    [HttpPost("bulk/upload-image")]
    public async Task<IActionResult> BulkUploadImage([FromForm] string productIdsCsv, IFormFile image,
        [FromServices] IWebHostEnvironment env,
        [FromServices] SupabaseStorageService storage)
    {
        if (string.IsNullOrWhiteSpace(productIdsCsv)) return BadRequest("Hiçbir ürün seçilmedi.");
        if (image == null || image.Length == 0) return BadRequest("Görsel yüklenmedi.");

        var idList = productIdsCsv.Split(',').Select(id => int.TryParse(id, out var parsed) ? parsed : 0).Where(id => id > 0).ToList();
        if (!idList.Any()) return BadRequest("Geçerli ürün seçimi yok.");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext)) return BadRequest("Geçersiz dosya uzantısı.");

        // Read and resize the source image once into a byte array
        using var masterMs = new MemoryStream();
        using (var stream = image.OpenReadStream())
        using (var img = await SixLabors.ImageSharp.Image.LoadAsync(stream))
        {
            if (img.Width > 300 || img.Height > 300)
                img.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(300, 300) }));
            await img.SaveAsync(masterMs, SixLabors.ImageSharp.Formats.Jpeg.JpegFormat.Instance);
        }
        var imageBytes = masterMs.ToArray();

        var products = await db.Products.Where(p => idList.Contains(p.Id)).ToListAsync();
        int updateCount = 0;

        foreach (var product in products)
        {
            var fileName = $"{product.Id}_{Guid.NewGuid()}.jpg";

            if (storage.IsConfigured)
            {
                var oldPath = SupabaseStorageService.ExtractObjectPath(product.ImageUrl, "product-images");
                if (oldPath != null) await storage.DeleteByUrlAsync("product-images", oldPath);
                using var ms = new MemoryStream(imageBytes);
                product.ImageUrl = await storage.UploadAsync("product-images", $"products/{fileName}", ms, "image/jpeg");
            }
            else
            {
                var dir = Path.Combine(env.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(dir);
                if (!string.IsNullOrEmpty(product.ImageUrl) && product.ImageUrl.StartsWith("/uploads"))
                {
                    var oldLocal = Path.Combine(env.WebRootPath, product.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldLocal)) System.IO.File.Delete(oldLocal);
                }
                await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, fileName), imageBytes);
                product.ImageUrl = $"/uploads/products/{fileName}";
            }
            updateCount++;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = $"{updateCount} ürüne görsel atandı." });
    }
}
