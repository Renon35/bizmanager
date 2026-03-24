using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    // --- Dealer Invoices ---
    [HttpGet("dealer")]
    public async Task<IActionResult> GetDealerInvoices() =>
        Ok(await db.DealerInvoices
            .Include(i => i.Order).ThenInclude(o => o!.Dealer)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync());

    [HttpPost("dealer")]
    public async Task<IActionResult> CreateDealerInvoice([FromForm] int orderId,
        [FromForm] bool issued, [FromForm] string? invoiceNumber,
        [FromForm] DateTime? invoiceDate, IFormFile? file)
    {
        var inv = new DealerInvoice { OrderId = orderId, Issued = issued, InvoiceNumber = invoiceNumber, InvoiceDate = invoiceDate };
        if (file != null) inv.FilePath = await SaveFile(file);
        db.DealerInvoices.Add(inv);
        await db.SaveChangesAsync();
        return Ok(inv);
    }

    [HttpPut("dealer/{id}")]
    public async Task<IActionResult> UpdateDealerInvoice(int id, [FromForm] bool issued,
        [FromForm] string? invoiceNumber, [FromForm] DateTime? invoiceDate, IFormFile? file)
    {
        var inv = await db.DealerInvoices.FindAsync(id);
        if (inv is null) return NotFound();
        inv.Issued = issued; inv.InvoiceNumber = invoiceNumber; inv.InvoiceDate = invoiceDate;
        if (file != null) inv.FilePath = await SaveFile(file);
        await db.SaveChangesAsync();
        return Ok(inv);
    }

    [HttpDelete("dealer/{id}")]
    public async Task<IActionResult> DeleteDealerInvoice(int id)
    {
        var inv = await db.DealerInvoices.FindAsync(id);
        if (inv is null) return NotFound();
        db.DealerInvoices.Remove(inv);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Customer Invoices ---
    [HttpGet("customer")]
    public async Task<IActionResult> GetCustomerInvoices() =>
        Ok(await db.CustomerInvoices
            .Include(i => i.Sale).ThenInclude(s => s!.Customer)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync());

    [HttpPost("customer")]
    public async Task<IActionResult> CreateCustomerInvoice([FromForm] int saleId,
        [FromForm] bool issued, [FromForm] string? invoiceNumber,
        [FromForm] DateTime? invoiceDate, IFormFile? file)
    {
        var inv = new CustomerInvoice { SaleId = saleId, Issued = issued, InvoiceNumber = invoiceNumber, InvoiceDate = invoiceDate };
        if (file != null) inv.FilePath = await SaveFile(file);
        db.CustomerInvoices.Add(inv);
        await db.SaveChangesAsync();
        return Ok(inv);
    }

    [HttpPut("customer/{id}")]
    public async Task<IActionResult> UpdateCustomerInvoice(int id, [FromForm] bool issued,
        [FromForm] string? invoiceNumber, [FromForm] DateTime? invoiceDate, IFormFile? file)
    {
        var inv = await db.CustomerInvoices.FindAsync(id);
        if (inv is null) return NotFound();
        inv.Issued = issued; inv.InvoiceNumber = invoiceNumber; inv.InvoiceDate = invoiceDate;
        if (file != null) inv.FilePath = await SaveFile(file);
        await db.SaveChangesAsync();
        return Ok(inv);
    }

    [HttpDelete("customer/{id}")]
    public async Task<IActionResult> DeleteCustomerInvoice(int id)
    {
        var inv = await db.CustomerInvoices.FindAsync(id);
        if (inv is null) return NotFound();
        db.CustomerInvoices.Remove(inv);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> SaveFile(IFormFile file)
    {
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var uploadsPath = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);
        var fullPath = Path.Combine(uploadsPath, fileName);
        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);
        return $"/uploads/{fileName}";
    }
}
