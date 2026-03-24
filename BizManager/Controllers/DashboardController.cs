using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;

namespace BizManager.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetStats()
    {
        var totalSales = await db.Sales.SumAsync(s => (decimal?)s.TotalPrice) ?? 0;
        var pendingOrders = await db.PurchaseOrders.CountAsync(o => o.Status == "preparing");
        var shippedOrders = await db.PurchaseOrders.CountAsync(o => o.Status == "shipped");
        var deliveredOrders = await db.PurchaseOrders.CountAsync(o => o.Status == "delivered");
        var missingDealerInvoices = await db.PurchaseOrders
            .Where(o => o.DealerInvoice == null || !o.DealerInvoice.Issued)
            .CountAsync();
        var missingCustomerInvoices = await db.Sales
            .Where(s => s.CustomerInvoice == null || !s.CustomerInvoice.Issued)
            .CountAsync();
        var lowStock = await db.DealerProducts
            .Include(dp => dp.Product)
            .Include(dp => dp.Dealer)
            .Where(dp => dp.StockQuantity < 5)
            .Select(dp => new
            {
                dp.Id,
                ProductName = dp.Product!.ProductName,
                DealerName = dp.Dealer!.Name,
                dp.StockQuantity
            })
            .ToListAsync();

        var recentOrders = await db.PurchaseOrders
            .Include(o => o.Dealer)
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
            .Select(o => new { o.Id, o.OrderNumber, DealerName = o.Dealer!.Name, o.Status, o.OrderDate })
            .ToListAsync();

        var missingImages = await db.Products
            .Include(p => p.Collection)
            .Include(p => p.Catalog).ThenInclude(c => c!.Brand)
            .Where(p => string.IsNullOrEmpty(p.ImageUrl))
            .Select(p => new
            {
                p.ProductCode,
                p.ProductName,
                CollectionName = p.Collection != null ? p.Collection.CollectionName : "-",
                CatalogName = p.Catalog != null ? p.Catalog.CatalogName : "-",
                BrandName = p.Catalog != null && p.Catalog.Brand != null ? p.Catalog.Brand.Name : "-"
            })
            .ToListAsync();

        return Ok(new
        {
            TotalSales = totalSales,
            PendingOrders = pendingOrders,
            ShippedOrders = shippedOrders,
            DeliveredOrders = deliveredOrders,
            MissingDealerInvoices = missingDealerInvoices,
            MissingCustomerInvoices = missingCustomerInvoices,
            LowStock = lowStock,
            RecentOrders = recentOrders,
            MissingImages = missingImages
        });
    }
}
