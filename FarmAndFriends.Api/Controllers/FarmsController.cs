using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Farms;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using FarmAndFriends.Api.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("farms")]
[Authorize]
public class FarmsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FriendshipService _friendshipService;
    private readonly CropCareService _cropCareService;
    private readonly PestService _pestService;
    private readonly LandExpansionService _landExpansionService;

    public FarmsController(
        AppDbContext context,
        FriendshipService friendshipService,
        CropCareService cropCareService,
        PestService pestService,
        LandExpansionService landExpansionService)
    {
        _context = context;
        _friendshipService = friendshipService;
        _cropCareService = cropCareService;
        _pestService = pestService;
        _landExpansionService = landExpansionService;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyFarm()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var farmId = await _context.Farms
            .AsNoTracking()
            .Where(farm => farm.UserId == userId)
            .Select(farm => (Guid?)farm.Id)
            .SingleOrDefaultAsync();

        if (!farmId.HasValue)
            return NotFound("Fazenda não encontrada.");

        await _pestService.ProcessFarmAsync(farmId.Value);
        var farm = await LoadFarmAsync(farmId.Value);
        var now = _pestService.UtcNow;
        var careStates = await _cropCareService.GetFarmCareStatesAsync(
            farm!.Id,
            userId,
            farm.UserId,
            farm.Plots,
            now);

        return Ok(FarmMapper.ToFarmResponse(
            farm,
            now,
            careStates,
            _pestService.GetNextPestCheckAt(farm.Plots, now),
            _landExpansionService.GetOffer(farm.Id, farm.Plots)));
    }

    [HttpGet("{farmId}")]
    public async Task<IActionResult> GetPublicFarm(Guid farmId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var farmIdentity = await _context.Farms
            .AsNoTracking()
            .Where(farm => farm.Id == farmId)
            .Select(farm => new { farm.Id, farm.UserId })
            .SingleOrDefaultAsync();

        if (farmIdentity == null)
            return NotFound("Fazenda não encontrada.");

        if (farmIdentity.UserId == userId)
            return BadRequest(
                "Use /farms/my para acessar sua própria fazenda.");

        if (!await _friendshipService.AreFriendsAsync(
                userId,
                farmIdentity.UserId))
        {
            return Forbid();
        }

        await _pestService.ProcessFarmAsync(farmId);
        var farm = await LoadFarmAsync(farmId);
        var now = _pestService.UtcNow;
        var careStates = await _cropCareService.GetFarmCareStatesAsync(
            farm!.Id,
            userId,
            farm.UserId,
            farm.Plots,
            now);

        return Ok(FarmMapper.ToFarmResponse(
            farm,
            now,
            careStates,
            _pestService.GetNextPestCheckAt(farm.Plots, now),
            landOffer: null,
            isOwnerView: false));
    }

    [HttpGet("{farmId}/theft-log")]
    public async Task<IActionResult> GetTheftLog(Guid farmId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var farm = await _context.Farms
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == farmId);

        if (farm == null)
            return NotFound("Fazenda não encontrada.");

        if (farm.UserId != userId)
            return Forbid();

        var logs = await _context.TheftLogs
            .AsNoTracking()
            .Include(log => log.Seed)
            .Include(log => log.ThiefUser)
            .Where(log => log.FarmId == farmId)
            .OrderByDescending(log => log.CreatedAt)
            .Select(log => new TheftLogResponse(
                log.ThiefUser.Username,
                log.Seed.Name,
                log.Quantity,
                log.GotBonus,
                log.CreatedAt))
            .ToListAsync();

        return Ok(logs);
    }

    [HttpGet("{farmId}/notifications")]
    public async Task<IActionResult> GetNotifications(Guid farmId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var farm = await _context.Farms
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == farmId);

        if (farm == null)
            return NotFound();

        if (farm.UserId != userId)
            return Forbid();

        var notifications = await _context.TheftLogs
            .AsNoTracking()
            .Include(log => log.ThiefUser)
            .Include(log => log.Seed)
            .Where(log => log.FarmId == farmId)
            .OrderByDescending(log => log.CreatedAt)
            .Take(10)
            .Select(log => new TheftNotificationResponse(
                log.GotBonus
                    ? $"{log.ThiefUser.Username} te roubou {log.Quantity} {log.Seed.Name}(s) e ganhou bônus, sortudo! ⭐"
                    : $"{log.ThiefUser.Username} roubou {log.Quantity} {log.Seed.Name}(s)",
                log.CreatedAt))
            .ToListAsync();

        return Ok(notifications);
    }

    private async Task<Domain.Entities.Farm?> LoadFarmAsync(Guid farmId) =>
        await _context.Farms
            .AsNoTracking()
            .Include(farm => farm.User)
            .Include(farm => farm.Plots)
            .SingleOrDefaultAsync(farm => farm.Id == farmId);

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
}
