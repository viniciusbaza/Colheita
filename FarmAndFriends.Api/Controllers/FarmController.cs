using FarmAndFriends.Api.Contracts.Farms;
using FarmAndFriends.Api.Infrastructure.Data;
using FarmAndFriends.Api.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Authorize]
[Route("farm")]
public class FarmController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FarmYieldService _farmYieldService;

    public FarmController(AppDbContext context, FarmYieldService farmYieldService)
    {
        _context = context;
        _farmYieldService = farmYieldService;
    }

    [HttpGet]
    public async Task<ActionResult<FarmResponse>> GetMyFarm()
    {
        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        var farm = await _context.Farms
            .Include(f => f.Plots)
            .FirstOrDefaultAsync(f => f.UserId == userId);

        if (farm == null)
            return NotFound();

        var now = DateTime.UtcNow;

        await _farmYieldService.PopulateRemainingYieldAsync(farm, now);
        return Ok(FarmMapper.ToFarmResponse(farm, now));
    }
}
