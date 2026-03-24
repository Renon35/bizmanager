using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace BizManager.Controllers;

[ApiController]
[Route("api/catalog-analysis")]
public class CatalogAnalysisController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly IWebHostEnvironment _env = env;

    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue, ValueLengthLimit = int.MaxValue)]
    public async Task<IActionResult> UploadQueue(List<IFormFile> files)
    {
        if (files == null || files.Count == 0) return BadRequest("Görsel veya PDF yüklenmedi.");

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "analysis");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var uploadedFiles = new List<string>();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".pdf") continue;

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            uploadedFiles.Add(fileName);
        }

        return Ok(new { success = true, files = uploadedFiles });
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest req)
    {
        if (string.IsNullOrEmpty(req.FileName)) return BadRequest("Dosya adı eksik.");
        
        var brand = await _db.Brands.FindAsync(req.BrandId);
        if (brand == null) return BadRequest("Lütfen önce geçerli bir marka seçiniz.");

        var filePath = Path.Combine(_env.WebRootPath, "uploads", "analysis", req.FileName);
        if (!System.IO.File.Exists(filePath)) return NotFound("PDF dosyası bulunamadı.");

        var results = new List<AnalysisItem>();
        string currentCollection = "Bilinmeyen Koleksiyon";
        int currentQty = 0;

        using (PdfDocument document = PdfDocument.Open(filePath))
        {
            foreach (Page page in document.GetPages())
            {
                var words = page.GetWords().OrderBy(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();
                var pageProducts = new List<AnalysisItem>();
                
                foreach (var word in words)
                {
                    var text = word.Text.Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    // QTY detection heuristics
                    if (text.StartsWith("QTY:") || text.StartsWith("Adet:"))
                    {
                        var qtyPart = text.Split(':').LastOrDefault()?.Trim();
                        if (int.TryParse(qtyPart, out int parsedQty)) currentQty = parsedQty;
                    }

                    // Ignore strict textual noise
                    string upperText = text.ToUpperInvariant();
                    string[] ignoreWords = { "JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE", "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER", 
                                             "PRODUCTS", "BOWLS", "DISHES", "CATALOG", "INDEX", "CONTENTS", "QTY", "ADET" };
                    if (ignoreWords.Contains(upperText) || Regex.IsMatch(text, @"^(19|20)\d{2}$") || Regex.IsMatch(text, @"^\d+$"))
                    {
                        continue;
                    }

                    // 1) Collection Detection Rule: Detect collection BEFORE processing product codes
                    bool isPotentialCollection = text.Length > 3 && char.IsUpper(text[0]) && !Regex.IsMatch(text, @"[0-9]");
                    // 2) Product Code Detection Rule
                    // Added digit check so purely alphabetic words aren't mistakenly treated as product codes
                    bool isCode = Regex.IsMatch(text, @"^[A-Z0-9]{3,12}$") && Regex.IsMatch(text, @"[0-9]");
                    
                    if (isPotentialCollection)
                    {
                         currentCollection = text;
                    }
                    else if (isCode)
                    {
                        string moldCode = "";
                        string size = "";
                        
                        if (brand.CodeStructure == "dual_code")
                        {
                            var match = Regex.Match(text, @"^([0-9]*[A-Z]+)(\d+)$");
                            if (match.Success)
                            {
                                moldCode = match.Groups[1].Value;
                                size = match.Groups[2].Value;
                            }
                        }

                        string generatedName = string.IsNullOrEmpty(size) 
                            ? $"Ürün {text}" 
                            : $"{currentCollection} {size} cm".Trim();

                        var item = new AnalysisItem
                        {
                            ProductCode = text,
                            MoldCode = moldCode,
                            CollectionName = currentCollection,
                            ProductName = generatedName,
                            UnitsPerCase = currentQty > 0 ? currentQty : null,
                            PageNumber = page.Number,
                            WordX = word.BoundingBox.Left,
                            WordY = word.BoundingBox.Bottom
                        };
                        pageProducts.Add(item);
                        results.Add(item);
                    }
                }

                // Scoped Image Extraction Logic
                // If there's 1 image on the page, check if it is reasonably close to the products
                var images = page.GetImages().ToList();
                if (images.Any() && pageProducts.Any())
                {
                    try
                    {
                        var firstImage = images.First();
                        if (firstImage.TryGetPng(out byte[] pngData))
                        {
                            var outFolder = Path.Combine(_env.WebRootPath, "uploads", "products");
                            if (!Directory.Exists(outFolder)) Directory.CreateDirectory(outFolder);

                            // Base the name on the first mold or product code on the page to symbolize the cluster
                            string baseRef = pageProducts.First().MoldCode != "" ? pageProducts.First().MoldCode : pageProducts.First().ProductCode;
                            var outName = $"{baseRef}_{Guid.NewGuid().ToString().Substring(0,8)}.png";
                            var outPath = Path.Combine(outFolder, outName);
                            
                            System.IO.File.WriteAllBytes(outPath, pngData);
                            string webPath = $"/uploads/products/{outName}";

                            // Apply shared image to all page variants ONLY IF they are within a reasonable distance
                            // Use Euclidean distance between Image bounds and Word bounds.
                            // A standard A4 page is approx 595x842 points. Let's use 400 points as threshold (~14cm)
                            double maxDistanceThreshold = 400.0;
                            
                            // Image coordinates in PDF are usually bottom-left origin
                            double imgX = firstImage.Bounds.Left + (firstImage.Bounds.Width / 2);
                            double imgY = firstImage.Bounds.Bottom + (firstImage.Bounds.Height / 2);

                            foreach(var p in pageProducts) 
                            {
                                double dx = p.WordX - imgX;
                                double dy = p.WordY - imgY;
                                double distance = Math.Sqrt((dx*dx) + (dy*dy));

                                if (distance <= maxDistanceThreshold)
                                {
                                    p.ExtractedImagePath = webPath;
                                }
                            }
                        }
                    }
                    catch { /* skip broken images */ }
                }
            }
        }

        return Ok(new { success = true, items = results });
    }

    [HttpPost("commit")]
    public async Task<IActionResult> Commit([FromBody] CommitAnalysisRequest req)
    {
        var catalog = await _db.Catalogs.FindAsync(req.CatalogId);
        if (catalog == null) return NotFound("Katalog bulunamadı.");

        // Similar to the unified Excel commit process, map the reviewed payloads to Database entities.
        int createdCount = 0;
        int mappedCollections = 0;

        var existingCollections = await _db.Collections
            .Where(c => c.CatalogId == catalog.Id)
            .ToDictionaryAsync(c => c.CollectionName, c => c.Id);

        foreach (var item in req.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductCode)) continue;

            int? collId = null;
            if (!string.IsNullOrWhiteSpace(item.CollectionName))
            {
                if (!existingCollections.TryGetValue(item.CollectionName, out int existingId))
                {
                    var newColl = new Collection { CatalogId = catalog.Id, CollectionName = item.CollectionName };
                    _db.Collections.Add(newColl);
                    await _db.SaveChangesAsync();
                    existingCollections[item.CollectionName] = newColl.Id;
                    collId = newColl.Id;
                    mappedCollections++;
                }
                else
                {
                    collId = existingId;
                }
            }

            // Check if product exists to avoid overlap
            var existingProduct = await _db.Products.FirstOrDefaultAsync(p => p.ProductCode == item.ProductCode && p.CatalogId == catalog.Id && p.CollectionId == collId);
            if (existingProduct == null)
            {
                var p = new Product
                {
                    ProductCode = item.ProductCode,
                    MoldCode = item.MoldCode,
                    ProductName = item.ProductName ?? item.ProductCode,
                    UnitsPerCase = item.UnitsPerCase,
                    CatalogId = catalog.Id,
                    CollectionId = collId,
                    ImageUrl = item.ExtractedImagePath
                };
                _db.Products.Add(p);
                createdCount++;
            }
            else
            {
                // Update collection if missing
                if (existingProduct.CollectionId == null && collId.HasValue) existingProduct.CollectionId = collId;
                // Update image if we found a new one
                if (string.IsNullOrEmpty(existingProduct.ImageUrl) && !string.IsNullOrEmpty(item.ExtractedImagePath)) existingProduct.ImageUrl = item.ExtractedImagePath;
                // Update Mold / QTY if missing
                if (string.IsNullOrEmpty(existingProduct.MoldCode) && !string.IsNullOrEmpty(item.MoldCode)) existingProduct.MoldCode = item.MoldCode;
                if (!existingProduct.UnitsPerCase.HasValue && item.UnitsPerCase.HasValue) existingProduct.UnitsPerCase = item.UnitsPerCase;
            }
        }
        await _db.SaveChangesAsync();

        return Ok(new { success = true, created = createdCount, newCollections = mappedCollections });
    }
}

public class AnalyzeRequest
{
    public string FileName { get; set; } = string.Empty;
    public int BrandId { get; set; }
}

public class CommitAnalysisRequest
{
    public int CatalogId { get; set; }
    public List<AnalysisItem> Items { get; set; } = new();
}

public class AnalysisItem
{
    public string ProductCode { get; set; } = string.Empty;
    public string MoldCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public int? UnitsPerCase { get; set; }
    public int PageNumber { get; set; }
    public string? ExtractedImagePath { get; set; }
    public double WordX { get; set; }
    public double WordY { get; set; }
}
