using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FarmAndFriends.Api.Infrastructure.Data;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("seeds")]
public class SeedController : ControllerBase
{
    private readonly AppDbContext _context;

    public SeedController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var seeds = await _context.Seeds
            .OrderBy(s => s.MinLevel)
            .ToListAsync();

            var result = seeds.Select(s => new
            {
                s.Id,
                s.Name,
                s.BuyPrice,
                s.SellPrice,
                GrowTimeMinutes = s.GrowTime.TotalMinutes,
                s.TheftChancePercent,
                s.MinLevel
            });

        return Ok(seeds);
    }
}
