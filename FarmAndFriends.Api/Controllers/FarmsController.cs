using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FarmAndFriends.Api.Infrastructure.Data;
using FarmAndFriends.Api.Contracts.Farms;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Mappers;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("farms")]
[Authorize]
public class FarmsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FarmYieldService _farmYieldService;
    private readonly FriendshipService _friendshipService;

    public FarmsController(
        AppDbContext context,
        FarmYieldService farmYieldService,
        FriendshipService friendshipService)
    {
        _context = context;
        _farmYieldService = farmYieldService;
        _friendshipService = friendshipService;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyFarm()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
            return Unauthorized();

        var farm = await _context.Farms
            .Include(f => f.User)
            .Include(f => f.Plots)
            .FirstOrDefaultAsync(f => f.UserId == Guid.Parse(userId));

        if (farm == null)
            return NotFound("Fazenda não encontrada");

        var now = DateTime.UtcNow;

        await _farmYieldService.PopulateRemainingYieldAsync(farm, now);

        var baseResponse = FarmMapper.ToFarmResponse(farm, now);

        return Ok(new
        {
            id = baseResponse.Id,
            name = farm.Name,
            ownerUserId = farm.UserId,
            ownerUsername = farm.User.Username,
            plots = baseResponse.Plots.Select(p => new
            {
                p.Id,
                p.X,
                p.Y,
                p.Unlocked,
                p.SeedId,
                plantedAt = farm.Plots.First(pl => pl.Id == p.Id).PlantedAt,
                p.IsReady,
                p.ReadyAt,
                p.RemainingYield
            })
        });
    }

    [HttpGet("{farmId}")]
    public async Task<IActionResult> GetPublicFarm(Guid farmId)
    {
        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        var farm = await _context.Farms
            .Include(f => f.Plots)
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == farmId);

        if (farm == null)
            return NotFound("Farm não encontrada");

        // ❌ Impede acessar a própria fazenda por aqui
        if (farm.UserId == userId)
            return BadRequest("Use /farms/my para acessar sua própria fazenda");

        // ❌ Impede acessar fazendas de usuários que não são amigos
        var areFriends = await _friendshipService.AreFriendsAsync(userId, farm.UserId);
        if (!areFriends)
            return Forbid();

        var now = DateTime.UtcNow;

        // Populamos remainingYield
        // ⚠️ O roubo depende disso
        await _farmYieldService.PopulateRemainingYieldAsync(farm, now);

        var baseResponse = FarmMapper.ToFarmResponse(farm, now);

        return Ok(new
        {
            baseResponse.Id,
            farm.Name,
            ownerUserId = farm.UserId,
            ownerUsername = farm.User.Username,
            plots = baseResponse.Plots.Select(p => new
            {
                p.Id,
                p.X,
                p.Y,
                p.Unlocked,
                p.SeedId,
                p.IsReady,
                p.ReadyAt,
                p.RemainingYield
            })
        });
    }

    [HttpGet("{farmId}/theft-log")]
    public async Task<IActionResult> GetTheftLog(Guid farmId)
    {
        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        // 1️⃣ Verifica se a farm é do usuário
        var farm = await _context.Farms
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == farmId);

        if (farm == null)
            return NotFound("Farm não encontrada");

        if (farm.User.Id != userId)
            return Forbid();

        // 2️⃣ Buscar logs
        var logs = await _context.TheftLogs
            .Include(t => t.Seed)
            .Include(t => t.ThiefUser)
            .Where(t => t.FarmId == farmId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TheftLogResponse(
                t.ThiefUser.Username,
                t.Seed.Name,
                t.Quantity,
                t.GotBonus,
                t.CreatedAt
            ))
            .ToListAsync();


        return Ok(logs);
    }

    [HttpGet("{farmId}/notifications")]
    public async Task<IActionResult> GetNotifications(Guid farmId)
    {
        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        var farm = await _context.Farms
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == farmId);

        if (farm == null)
            return NotFound();

        if (farm.User.Id != userId)
            return Forbid();

        var notifications = await _context.TheftLogs
            .Include(t => t.ThiefUser)
            .Include(t => t.Seed)
            .Where(t => t.FarmId == farmId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .Select(t => new TheftNotificationResponse(
                t.GotBonus
                    ? $"{t.ThiefUser.Username} te roubou {t.Quantity} {t.Seed.Name}(s) e ganhou bônus, sortudo! ⭐"
                    : $"{t.ThiefUser.Username} roubou {t.Quantity} {t.Seed.Name}(s)",
                t.CreatedAt
            ))
            .ToListAsync();

        return Ok(notifications);
    }
}
