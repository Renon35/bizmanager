using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/dealers")]
public class DealersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.Dealers.Include(d => d.Brand).OrderBy(d => d.Name).ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var dealer = await db.Dealers.Include(d => d.Brand).FirstOrDefaultAsync(d => d.Id == id);
        return dealer is null ? NotFound() : Ok(dealer);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Dealer dealer)
    {
        db.Dealers.Add(dealer);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = dealer.Id }, dealer);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Dealer updated)
    {
        var dealer = await db.Dealers.FindAsync(id);
        if (dealer is null) return NotFound();
        dealer.BrandId = updated.BrandId;
        dealer.Name = updated.Name;
        dealer.ContactPerson = updated.ContactPerson;
        dealer.Phone = updated.Phone;
        dealer.Email = updated.Email;
        dealer.Address = updated.Address;
        dealer.Notes = updated.Notes;
        await db.SaveChangesAsync();
        return Ok(dealer);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var dealer = await db.Dealers.FindAsync(id);
        if (dealer is null) return NotFound();
        db.Dealers.Remove(dealer);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
