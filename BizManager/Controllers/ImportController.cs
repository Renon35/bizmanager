using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;
using ExcelDataReader;
using System.Data;
using System.Text;
using BizManager.Services;

namespace BizManager.Controllers;

public class PreviewItem
{
    public string ProductCode { get; set; } = string.Empty;
    public string MoldCode { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsHeader { get; set; }
}

public class CommitPayload
{
    public int BrandId { get; set; }
    public int CatalogId { get; set; }
    public int DealerId { get; set; }
    public string PriceType { get; set; } = "purchase_price";
    public List<PreviewItem> Items { get; set; } = new();
}

[ApiController]
[Route("api/import")]
public class ImportController(AppDbContext db, ProductImageScraperService scraperService) : ControllerBase
{
    // ─────────────────────────────────────────────────────────────────────────
    // UNIFIED IMPORT
    // Expected Columns: Product Code | Product Name | Case Price | Pack Price | Unit Price | Stock
    // Logic: 
    //   1. Preview the file data, detect collections, clean prices.
    //   2. Commit parsed data logically matching user specifications.
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("test-scraper")]
    public async Task<IActionResult> TestScraper([FromQuery] string domain, [FromQuery] string code)
    {
        var url = await scraperService.ScrapeImageAsync(domain, code);
        return Ok(new { url });
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewUnified([FromQuery] int brandId, [FromQuery] int dealerId, [FromQuery] string priceType, IFormFile file)
    {
        var dealer = await db.Dealers.FindAsync(dealerId);
        if (dealer is null) return NotFound(new { error = $"Bayi bulunamadı (id={dealerId})." });
        
        var brand = await db.Brands.FindAsync(brandId);
        if (brand is null) return NotFound(new { error = $"Marka bulunamadı (id={brandId})." });
        
        string codeStructure = brand.CodeStructure ?? "single_code";

        var (table, err) = OpenExcel(file);
        if (table is null) return BadRequest(new { error = err });

        var hdr = BuildHeaderMap(table);
        int colCode      = ColIdx(hdr, "Product Code", "Ürün Kodu", "Urun Kodu", "product_code", "kod", "code", "kodu");
        int colMold      = ColIdx(hdr, "Mold Code",    "Kalıp Kodu", "Kalip Kodu", "mold_code", "kalıp", "kalip");
        int colBarcode   = ColIdx(hdr, "Barcode",      "Barkod",    "barcode", "ean");
        int colName      = ColIdx(hdr, "Product Name", "Ürün Adı", "Urun Adi", "product_name", "mamul adı", "item name", "ürün ismi", "name");
        
        int colPrice    = ColIdx(hdr, "Price", "Fiyat", "Fiyati", "Birim Fiyatı", "Birim Fiyati", "Unit Price", "Alış Fiyatı", "Satış Fiyatı", "Liste Fiyatı", "price");
        int colStock    = ColIdx(hdr, "Stock", "Stok", "Miktar", "Adet", "stock");
        int colCollection = ColIdx(hdr, "Collection Name", "Koleksiyon Adı", "Koleksiyon Adi", "Koleksiyon", "collection", "collection_name", "Collection");

        // Validate required headers based on codeStructure
        if (codeStructure == "single_code" && colCode < 0) return BadRequest(new { error = "Excel dosyasında 'Ürün Kodu' (veya benzeri) sütunu bulunamadı." });
        if (codeStructure == "dual_code" && (colCode < 0 || colMold < 0)) return BadRequest(new { error = "Excel dosyasında 'Ürün Kodu' ve 'Kalıp Kodu' sütunları bulunamadı." });
        if (codeStructure == "barcode" && colBarcode < 0) return BadRequest(new { error = "Excel dosyasında 'Barkod' sütunu bulunamadı." });

        var results = new List<PreviewItem>();
        string currentCollection = string.Empty;

        foreach (DataRow row in table.Rows)
        {
            string code    = colCode >= 0 ? GetStr(row, colCode) : string.Empty;
            string mold    = colMold >= 0 ? GetStr(row, colMold) : string.Empty;
            string barcode = colBarcode >= 0 ? GetStr(row, colBarcode) : string.Empty;
            string name    = colName >= 0 ? GetStr(row, colName) : string.Empty;

            // Header Detection Logic:
            // If the row lacks any valid product codes but has a single text string (assumed to be name or the first cell),
            // and the rest is empty, treat it as a Collection Header.
            bool hasCode = !string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(mold) || !string.IsNullOrWhiteSpace(barcode);
            decimal priceValue = colPrice >= 0 ? GetDec(row, colPrice) : 0;
            int stockQuantity  = colStock >= 0 ? (int)GetDec(row, colStock) : 0;
            
            // Explicit collection column support
            if (colCollection >= 0)
            {
                string explicitCollection = GetStr(row, colCollection);
                if (!string.IsNullOrWhiteSpace(explicitCollection))
                    currentCollection = explicitCollection;
            }

            if (!hasCode && !string.IsNullOrWhiteSpace(name))
            {
                if (priceValue == 0 && colCollection < 0)
                {
                    currentCollection = name;
                    results.Add(new PreviewItem { 
                        CollectionName = name, 
                        IsHeader = true 
                    });
                    continue; // Header row safely mapped. 
                }
            }

            if (codeStructure == "single_code" && string.IsNullOrWhiteSpace(code)) continue;
            if (codeStructure == "dual_code" && (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(mold))) continue;
            if (codeStructure == "barcode" && string.IsNullOrWhiteSpace(barcode)) continue;

            string status = "Ready";
            
            if (codeStructure == "single_code" && string.IsNullOrWhiteSpace(code)) status = "Error: Missing Product Code";
            else if (codeStructure == "dual_code" && (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(mold))) status = "Error: Missing Code/Mold";
            else if (codeStructure == "barcode" && string.IsNullOrWhiteSpace(barcode)) status = "Error: Missing Barcode";
            else if (priceValue <= 0) status = "Warning: Price is zero";
            else if (string.IsNullOrWhiteSpace(name)) status = "Warning: Missing Name";

            var previewRecord = new ProductImportPreview {
                ProductCode = code,
                MoldCode = mold,
                Barcode = barcode,
                ProductName = name,
                Price = priceValue,
                Stock = stockQuantity,
                Collection = currentCollection,
                IsHeader = false,
                Status = status,
                BrandId = brandId,
                CatalogId = null,
                DealerId = dealerId,
                PriceType = priceType
            };
            db.ProductImportPreviews.Add(previewRecord);
            results.Add(new PreviewItem {
                ProductCode = code,
                MoldCode = mold,
                Barcode = barcode,
                ProductName = name,
                Price = priceValue,
                Stock = stockQuantity,
                CollectionName = currentCollection,
                IsHeader = false
            });
        }

        // Clean up previous previews for this brand and dealer to avoid conflicts
        await db.ProductImportPreviews
            .Where(p => p.BrandId == brandId && p.DealerId == dealerId)
            .ExecuteDeleteAsync();

        await db.SaveChangesAsync();

        // Re-fetch with IDs to return to frontend
        var savedItems = await db.ProductImportPreviews
            .Where(p => p.BrandId == brandId && p.DealerId == dealerId)
            .OrderBy(p => p.Id)
            .ToListAsync();

        return Ok(new { items = savedItems });
    }

    [HttpPut("preview/{id}")]
    public async Task<IActionResult> UpdatePreview(int id, [FromBody] ProductImportPreview model)
    {
        var preview = await db.ProductImportPreviews.FindAsync(id);
        if (preview == null) return NotFound();

        preview.ProductCode = model.ProductCode;
        preview.MoldCode = model.MoldCode;
        preview.Barcode = model.Barcode;
        preview.ProductName = model.ProductName;
        preview.Collection = model.Collection;
        preview.Price = model.Price;
        preview.Stock = model.Stock;
        preview.Status = "Ready"; // Reset status on edit, could re-validate here

        await db.SaveChangesAsync();
        return Ok(preview);
    }

    [HttpDelete("preview/{id}")]
    public async Task<IActionResult> DeletePreview(int id)
    {
        var preview = await db.ProductImportPreviews.FindAsync(id);
        if (preview != null)
        {
            db.ProductImportPreviews.Remove(preview);
            await db.SaveChangesAsync();
        }
        return Ok();
    }

    [HttpPost("commit")]
    public async Task<IActionResult> CommitUnified([FromBody] CommitPayload payload)
    {
        var dealer = await db.Dealers.FindAsync(payload.DealerId);
        if (dealer is null) return NotFound(new { error = $"Bayi bulunamadı (id={payload.DealerId})." });
        
        var brand = await db.Brands.FindAsync(payload.BrandId);
        if (brand is null) return NotFound(new { error = $"Marka bulunamadı (id={payload.BrandId})." });

        var previewItems = await db.ProductImportPreviews
            .Where(p => p.BrandId == payload.BrandId && p.DealerId == payload.DealerId)
            .OrderBy(p => p.Id)
            .ToListAsync();

        if (!previewItems.Any()) return BadRequest(new { error = "Aktarılacak veri bulunamadı. Lütfen önce önizleme oluşturun." });
        
        // Block commit if there are any remaining errors
        if (previewItems.Any(p => p.Status.StartsWith("Error:")))
        {
            return BadRequest(new { error = "Hatalı satırlar mevcut. Lütfen sorunlu satırları düzeltin veya silin." });
        }

        string codeStructure = brand.CodeStructure ?? "single_code";
        string? brandDomain = brand.WebsiteDomain;
        
        int productsCreated = 0, productsUpdated = 0, dealerPricesUpdated = 0, rowsSkipped = 0;
        
        // Find existing Catalogs for this brand
        var brandCatalogsResult = await db.Catalogs.Where(c => c.BrandId == brand.Id).ToListAsync();
        Catalog? defaultCatalog = brandCatalogsResult.FirstOrDefault();
        var catalogIds = brandCatalogsResult.Select(c => (int?)c.Id).ToList();

        // Optimize querying collections beforehand
        var knownCollectionsDb = await db.Collections
            .Where(c => catalogIds.Contains(c.CatalogId))
            .ToListAsync();
        var knownCollections = knownCollectionsDb
            .GroupBy(c => c.CollectionName)
            .ToDictionary(g => g.Key, g => g.First());
            
        // Batch dictionary lookups - safe against duplicates
        var codeProductsDb = await db.Products.Where(p => catalogIds.Contains(p.CatalogId) && p.ProductCode != null).ToListAsync();
        var codeProducts = codeProductsDb.GroupBy(p => p.ProductCode!).ToDictionary(g => g.Key, g => g.First());
        
        var barcodeProductsDb = await db.Products.Where(p => catalogIds.Contains(p.CatalogId) && p.Barcode != null).ToListAsync();
        var barcodeProducts = barcodeProductsDb.GroupBy(p => p.Barcode!).ToDictionary(g => g.Key, g => g.First());
        
        var moldProductsDb = await db.Products.Where(p => catalogIds.Contains(p.CatalogId) && p.MoldCode != null && p.ProductCode != null).ToListAsync();
        var moldProducts = moldProductsDb.GroupBy(p => p.MoldCode + "_" + p.ProductCode).ToDictionary(g => g.Key, g => g.First());

        var existingDPs = await db.DealerProducts.Where(dp => dp.DealerId == dealer.Id).ToDictionaryAsync(dp => dp.ProductId, dp => dp);

        foreach(var item in previewItems)
        {
            if (item.IsHeader) continue;

            Collection? targetCollection = null;
            if (!string.IsNullOrWhiteSpace(item.Collection))
            {
                if (!knownCollections.TryGetValue(item.Collection, out targetCollection))
                {
                    if (defaultCatalog is null)
                    {
                        defaultCatalog = new Catalog { BrandId = brand.Id, CatalogName = $"{brand.Name} Genel Katalog" };
                        db.Catalogs.Add(defaultCatalog);
                        await db.SaveChangesAsync();
                        brandCatalogsResult.Add(defaultCatalog);
                        catalogIds.Add((int?)defaultCatalog.Id);
                    }
                    
                    var newColl = new Collection { CatalogId = defaultCatalog.Id, CollectionName = item.Collection };
                    db.Collections.Add(newColl);
                    await db.SaveChangesAsync(); // Needs sequential save to get ID.
                    knownCollections[item.Collection] = newColl;
                    targetCollection = newColl;
                }
            }

            Product? product = null;
            if (codeStructure == "barcode")
            {
                if (!string.IsNullOrWhiteSpace(item.Barcode))
                    barcodeProducts.TryGetValue(item.Barcode, out product);
            }
            else if (codeStructure == "dual_code")
            {
                if (!string.IsNullOrWhiteSpace(item.MoldCode) && !string.IsNullOrWhiteSpace(item.ProductCode))
                    moldProducts.TryGetValue(item.MoldCode + "_" + item.ProductCode, out product);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(item.ProductCode))
                    codeProducts.TryGetValue(item.ProductCode, out product);
            }

            if (product is null)
            {
                string name = item.ProductName ?? string.Empty;
                string? inheritedImageUrl = null;
                Collection? inheritedCollection = targetCollection;

                // Mold Code Cloning Engine
                if (codeStructure == "dual_code" && !string.IsNullOrWhiteSpace(item.MoldCode))
                {
                    // Find a root product spanning the same MoldCode AND Collection
                    var baseQuery = db.Products.Where(p => p.MoldCode == item.MoldCode && catalogIds.Contains(p.CatalogId));
                    if (targetCollection != null)
                    {
                        baseQuery = baseQuery.Where(p => p.CollectionId == targetCollection.Id);
                    }
                    
                    var baseProduct = await baseQuery.FirstOrDefaultAsync();

                    if (baseProduct != null)
                    {
                        if (string.IsNullOrWhiteSpace(name)) name = baseProduct.ProductName;
                        if (inheritedCollection == null && baseProduct.CollectionId.HasValue) 
                        {
                            inheritedCollection = await db.Collections.FindAsync(baseProduct.CollectionId);
                        }
                        inheritedImageUrl = baseProduct.ImageUrl;
                    }
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    if (codeStructure == "barcode") name = item.Barcode ?? string.Empty;
                    else if (codeStructure == "dual_code") name = $"{item.MoldCode} - {item.ProductCode}";
                    else name = item.ProductCode ?? string.Empty;
                }

                if (inheritedCollection == null && defaultCatalog == null)
                {
                    defaultCatalog = new Catalog { BrandId = brand.Id, CatalogName = $"{brand.Name} Genel Katalog" };
                    db.Catalogs.Add(defaultCatalog);
                    await db.SaveChangesAsync();
                    brandCatalogsResult.Add(defaultCatalog);
                    catalogIds.Add((int?)defaultCatalog.Id);
                }

                product = new Product
                {
                    ProductCode = codeStructure != "barcode" ? item.ProductCode : null,
                    MoldCode    = codeStructure == "dual_code" ? item.MoldCode : null,
                    Barcode     = codeStructure == "barcode" ? item.Barcode : null,
                    ProductName = name,
                    CatalogId   = inheritedCollection?.CatalogId ?? defaultCatalog!.Id,
                    CollectionId = inheritedCollection?.Id,
                    ImageUrl     = inheritedImageUrl,
                    PurchasePrice = payload.PriceType == "purchase_price" ? item.Price : 0,
                    SalePrice = payload.PriceType == "sale_price" ? item.Price : 0,
                    ListPrice = payload.PriceType == "list_price" ? item.Price : 0
                };
                
                db.Products.Add(product);
                await db.SaveChangesAsync(); // Must save product instantly to grab ID for DealerProducts mapping. 
                
                // --- Image Scraping ---
                if (string.IsNullOrEmpty(product.ImageUrl) && !string.IsNullOrWhiteSpace(brandDomain) && !string.IsNullOrWhiteSpace(product.ProductCode))
                {
                    string? newImageUrl = await scraperService.ScrapeImageAsync(brandDomain, product.ProductCode);
                    if (!string.IsNullOrEmpty(newImageUrl))
                    {
                        product.ImageUrl = newImageUrl;
                        await db.SaveChangesAsync();
                    }
                    else
                    {
                        product.HasMissingImage = true;
                        // await db.SaveChangesAsync(); // Optional since it will be saved later anyway, but good practice
                    }
                }
                
                // Add to lookup maps
                if (product.ProductCode != null) codeProducts[product.ProductCode] = product;
                if (product.Barcode != null) barcodeProducts[product.Barcode] = product;
                if (product.MoldCode != null && product.ProductCode != null) moldProducts[product.MoldCode + "_" + product.ProductCode] = product;

                productsCreated++;
            }
            else
            {
                bool changed = false;
                if (payload.PriceType == "purchase_price" && item.Price > 0 && product.PurchasePrice != item.Price) { product.PurchasePrice = item.Price; changed = true; }
                if (payload.PriceType == "sale_price" && item.Price > 0 && product.SalePrice != item.Price) { product.SalePrice = item.Price; changed = true; }
                if (payload.PriceType == "list_price" && item.Price > 0 && product.ListPrice != item.Price) { product.ListPrice = item.Price; changed = true; }
                if (targetCollection != null && product.CollectionId != targetCollection.Id) 
                { 
                    product.CollectionId = targetCollection.Id; 
                    product.CatalogId = targetCollection.CatalogId;
                    changed = true; 
                }
                if (changed) productsUpdated++;
            }

            // Dealer Products Mapping Layer
            if (!existingDPs.TryGetValue(product.Id, out DealerProduct? dp))
            {
                dp = new DealerProduct {
                    DealerId = dealer.Id,
                    ProductId = product.Id,
                    StockQuantity = item.Stock,
                    UnitPrice = item.Price,
                    LastUpdated = DateTime.UtcNow
                };
                db.DealerProducts.Add(dp);
                existingDPs[product.Id] = dp; // Cache locally
                dealerPricesUpdated++;
            }
            else 
            {
                if (dp.UnitPrice != item.Price || dp.StockQuantity != item.Stock)
                {
                    dp.UnitPrice = item.Price;
                    dp.StockQuantity = item.Stock;
                    dp.LastUpdated = DateTime.UtcNow;
                    dealerPricesUpdated++;
                }
            }
        }

        db.ProductImportPreviews.RemoveRange(previewItems);
        await db.SaveChangesAsync();

        return Ok(new { productsCreated, productsUpdated, dealerPricesUpdated, rowsSkipped });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Shared Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (DataTable? table, string? error) OpenExcel(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return (null, "Dosya yüklenemedi.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".xls")
            return (null, "Lütfen geçerli bir Excel dosyası (.xlsx veya .xls) yükleyin.");

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var stream = file.OpenReadStream();
        var reader = ext == ".xls"
            ? ExcelReaderFactory.CreateBinaryReader(stream)
            : ExcelReaderFactory.CreateOpenXmlReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        reader.Dispose();
        stream.Dispose();

        if (dataSet.Tables.Count == 0)
            return (null, "Excel dosyasında hiçbir sayfa bulunamadı.");

        return (dataSet.Tables[0], null);
    }

    private static Dictionary<string, int> BuildHeaderMap(DataTable table)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < table.Columns.Count; c++)
            map[table.Columns[c].ColumnName.Trim()] = c;
        return map;
    }

    private static int ColIdx(Dictionary<string, int> map, params string[] candidates)
    {
        foreach (var name in candidates)
            if (map.TryGetValue(name, out int idx)) return idx;
        return -1;
    }

    private static string GetStr(DataRow row, int col)
    {
        if (col < 0 || col >= row.Table.Columns.Count) return string.Empty;
        var val = row[col];
        return val is DBNull or null ? string.Empty : val.ToString()!.Trim();
    }

    private static int? GetInt(DataRow row, int col)
    {
        var s = GetStr(row, col);
        return int.TryParse(s, out var v) ? v : null;
    }

    private static decimal GetDec(DataRow row, int col)
    {
        var s = GetStr(row, col).ToUpperInvariant();
        s = s.Replace("₺", "").Replace("TL", "").Trim();
        s = s.Replace(',', '.');

        // If there's multiple dots (e.g. 1.250.00), only keep the last dot for decimals.
        int firstDot = s.IndexOf('.');
        int lastDot = s.LastIndexOf('.');
        if (firstDot != -1 && firstDot != lastDot)
        {
            s = s.Substring(0, lastDot).Replace(".", "") + "." + s.Substring(lastDot + 1);
        }

        return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }
}
