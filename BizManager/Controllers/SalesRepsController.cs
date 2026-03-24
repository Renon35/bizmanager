using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizManager.Data;
using BizManager.Models;

namespace BizManager.Controllers;

[ApiController]
[Route("api/sales-reps")]
public class SalesRepsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.SalesReps.OrderBy(s => s.LastName).ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var rep = await db.SalesReps.FindAsync(id);
        return rep is null ? NotFound() : Ok(rep);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] string firstName, [FromForm] string lastName,
        [FromForm] string? phone, [FromForm] string? email, IFormFile? logo)
    {
        var rep = new SalesRep { FirstName = firstName, LastName = lastName, Phone = phone, Email = email };
        if (logo != null)
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(logo.FileName)}";
            var path = Path.Combine("wwwroot", "uploads", fileName);
            await using var stream = System.IO.File.Create(path);
            await logo.CopyToAsync(stream);
            rep.LogoPath = $"/uploads/{fileName}";
        }
        db.SalesReps.Add(rep);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = rep.Id }, rep);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromForm] string firstName, [FromForm] string lastName,
        [FromForm] string? phone, [FromForm] string? email, IFormFile? logo)
    {
        var rep = await db.SalesReps.FindAsync(id);
        if (rep is null) return NotFound();
        rep.FirstName = firstName; rep.LastName = lastName;
        rep.Phone = phone; rep.Email = email;
        if (logo != null)
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(logo.FileName)}";
            var path = Path.Combine("wwwroot", "uploads", fileName);
            await using var stream = System.IO.File.Create(path);
            await logo.CopyToAsync(stream);
            rep.LogoPath = $"/uploads/{fileName}";
        }
        await db.SaveChangesAsync();
        return Ok(rep);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rep = await db.SalesReps.FindAsync(id);
        if (rep is null) return NotFound();
        db.SalesReps.Remove(rep);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
