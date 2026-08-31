using FarmAndFriends.Api.Contracts.Seeds;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            .AsNoTracking()
            .OrderBy(s => s.MinLevel)
            .ThenBy(s => s.Id)
            .Select(seed => new SeedCatalogResponse(
                seed.Id,
                seed.Name,
                seed.CropName,
                seed.Icon,
                seed.BuyPrice,
                seed.SellPrice,
                seed.GrowTime,
                seed.RegrowTime,
                seed.TheftChancePercent,
                seed.MinLevel,
                seed.CropId,
                seed.CropAmount,
                seed.HarvestCycles))
            .ToListAsync();

        return Ok(seeds);
    }
}
