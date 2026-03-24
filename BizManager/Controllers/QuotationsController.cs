using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;
using BizManager.Services;

namespace BizManager.Controllers;

[ApiController]
[Route("api/quotations")]
public class QuotationsController(AppDbContext db, PdfService pdfService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.Quotations
            .Include(q => q.SalesRep)
            .Include(q => q.Customer)
            .Include(q => q.Dealer)
            .Include(q => q.Items)
            .OrderByDescending(q => q.Date)
            .ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var q = await db.Quotations
            .Include(q => q.SalesRep)
            .Include(q => q.Customer)
            .Include(q => q.Dealer)
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id);
        return q is null ? NotFound() : Ok(q);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] QuotationRequest req)
    {
        var customer = await db.Customers.FindAsync(req.CustomerId);
        var salesRep = await db.SalesReps.FindAsync(req.SalesRepId);
        if (customer == null || salesRep == null) return BadRequest("Geçersiz müşteri veya satış temsilcisi.");

        // Generating Quotation Code: [CUSTOMER_ABBREVIATION]-[SALESPERSON_INITIALS]-[DATE]-[SEQUENCE]
        string custName = customer.CompanyName.Replace(" ", "");
        string custAbbr = new string(custName.Take(4).ToArray()).ToUpperInvariant();
        if (custAbbr.Length < 4) custAbbr = custAbbr.PadRight(4, 'X');
        
        string repInitials = $"{salesRep.FirstName?.FirstOrDefault()}{salesRep.LastName?.FirstOrDefault()}".ToUpperInvariant();
        string dateStr = req.Date.ToString("ddMMyyyy");

        var todayStart = req.Date.Date;
        var todayEnd = todayStart.AddDays(1);
        int todayCount = await db.Quotations.CountAsync(q => q.Date >= todayStart && q.Date < todayEnd);
        string seqNum = (todayCount + 1).ToString("D3");

        string generatedCode = $"{custAbbr}-{repInitials}-{dateStr}-{seqNum}";

        var quotation = new Quotation
        {
            QuotationNumber = generatedCode,
            SalesRepId = req.SalesRepId,
            CustomerId = req.CustomerId,
            DealerId = req.DealerId,
            Date = req.Date,
            VatRate = req.VatRate ?? 20.0m
        };
        foreach (var item in req.Items)
        {
            item.TotalPrice = item.Quantity * item.UnitPrice;
            quotation.Items.Add(item);
        }
        
        quotation.Subtotal = quotation.Items.Sum(i => i.TotalPrice);
        quotation.VatAmount = quotation.Subtotal * (quotation.VatRate / 100);
        quotation.GrandTotal = quotation.Subtotal + quotation.VatAmount;
        quotation.TotalPrice = quotation.GrandTotal; // Keep in sync for older clients

        db.Quotations.Add(quotation);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = quotation.Id }, quotation);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] QuotationRequest req)
    {
        var quotation = await db.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == id);
        if (quotation is null) return NotFound();
        quotation.QuotationNumber = req.QuotationNumber;
        quotation.SalesRepId = req.SalesRepId;
        quotation.CustomerId = req.CustomerId;
        quotation.DealerId = req.DealerId;
        quotation.Date = req.Date;
        if (req.VatRate.HasValue) quotation.VatRate = req.VatRate.Value;

        db.QuotationItems.RemoveRange(quotation.Items);
        quotation.Items.Clear();
        foreach (var item in req.Items)
        {
            item.TotalPrice = item.Quantity * item.UnitPrice;
            quotation.Items.Add(item);
        }
        
        quotation.Subtotal = quotation.Items.Sum(i => i.TotalPrice);
        quotation.VatAmount = quotation.Subtotal * (quotation.VatRate / 100);
        quotation.GrandTotal = quotation.Subtotal + quotation.VatAmount;
        quotation.TotalPrice = quotation.GrandTotal; // keep synced
        
        await db.SaveChangesAsync();
        return Ok(quotation);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var q = await db.Quotations.FindAsync(id);
        if (q is null) return NotFound();
        db.Quotations.Remove(q);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> GetPdf(int id)
    {
        var q = await db.Quotations
            .Include(q => q.SalesRep)
            .Include(q => q.Customer)
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id);
        if (q is null) return NotFound();
        var bytes = pdfService.GenerateQuotationPdf(q);
        
        string safeCustName = string.Join("_", (q.Customer?.CompanyName ?? "Musteri").Split(Path.GetInvalidFileNameChars()));
        string filename = $"{safeCustName}_{q.Date:dd-MM-yyyy}.pdf";
        
        return File(bytes, "application/pdf", filename);
    }
}

public record QuotationRequest(
    string QuotationNumber,
    int SalesRepId,
    int CustomerId,
    int? DealerId,
    DateTime Date,
    decimal? VatRate,
    List<QuotationItem> Items
);
