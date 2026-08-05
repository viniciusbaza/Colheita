using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Plots;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("plots")]
[Authorize]
public class PlotController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ExperienceService _experienceService;
    private readonly PestService _pestService;

    public PlotController(
        AppDbContext context,
        ExperienceService experienceService,
        PestService pestService)
    {
        _context = context;
        _experienceService = experienceService;
        _pestService = pestService;
    }

    [HttpPost("{plotId}/plant")]
    public async Task<IActionResult> Plant(
        Guid plotId,
        [FromBody] PlantSeedRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        await using var transaction =
            await _context.Database.BeginTransactionAsync();
        var lockedUser = await _context.LockAsync(userId);

        if (lockedUser == null)
            return Unauthorized();

        var farmId = await FindFarmIdAsync(plotId);
        if (!farmId.HasValue)
            return NotFound("Plot não encontrado.");

        var farm = await _context.LockFarmAsync(farmId.Value);
        var plots = await _context.LockFarmPlotsAsync(farmId.Value);
        var plot = plots.Single(candidate => candidate.Id == plotId);

        if (farm!.UserId != userId)
            return Forbid();

        if (!plot.Unlocked)
            return BadRequest("Plot está bloqueado.");

        if (plot.SeedId != null)
            return BadRequest("Plot já está plantado.");

        var seed = await _context.Seeds
            .FirstOrDefaultAsync(candidate => candidate.Id == request.SeedId);

        if (seed == null)
            return BadRequest("Semente inválida.");

        var inventory = await _context.Inventories
            .Include(candidate => candidate.Items)
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId);

        if (inventory == null)
            return BadRequest("Inventário não encontrado.");

        var seedItem = inventory.Items.FirstOrDefault(item =>
            item.ItemType == ItemType.Seed
            && item.ItemId == seed.Id);

        if (seedItem == null || seedItem.Quantity <= 0)
            return BadRequest("Você não possui essa semente.");

        seedItem.Quantity--;
        if (seedItem.Quantity == 0)
            _context.InventoryItems.Remove(seedItem);

        var now = _pestService.UtcNow;
        plot.SeedId = seed.Id;
        plot.PlantedAt = now;
        plot.ReadyAt = now.Add(seed.GrowTime);
        plot.RemainingYield = seed.CropAmount;
        PestRules.ResetCycle(plot);
        CropCareRules.StartCurrentCropCycle(plot);

        const int xpGained = 10;
        _experienceService.AddXp(lockedUser, xpGained);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            plot.Id,
            seed = seed.Name,
            plot.PlantedAt,
            plot.ReadyAt,
            xpGained
        });
    }

    [HttpPost("{plotId}/harvest")]
    public async Task<IActionResult> Harvest(Guid plotId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        await using var transaction =
            await _context.Database.BeginTransactionAsync();
        var lockedUser = await _context.LockAsync(userId);

        if (lockedUser == null)
            return Unauthorized();

        var farmId = await FindFarmIdAsync(plotId);
        if (!farmId.HasValue)
            return NotFound("Plantação não encontrada.");

        var farm = await _context.LockFarmAsync(farmId.Value);
        var plots = await _context.LockFarmPlotsAsync(farmId.Value);
        var plot = plots.Single(candidate => candidate.Id == plotId);

        if (farm!.UserId != userId)
            return Forbid();

        var now = _pestService.UtcNow;
        await _pestService.ProcessLockedFarmAsync(farm, plots, now);

        if (plot.SeedId == null)
            return BadRequest("Nenhuma plantação para colher.");

        if (plot.ReadyAt == null || plot.ReadyAt > now)
            return BadRequest("A plantação ainda não está pronta.");

        if (plot.RemainingYield is not int finalYield
            || finalYield < 1)
        {
            return YieldUnavailable();
        }

        var seed = await _context.Seeds
            .FirstOrDefaultAsync(candidate => candidate.Id == plot.SeedId);

        if (seed == null)
            return BadRequest("Semente inválida.");

        var inventory = await _context.Inventories
            .Include(candidate => candidate.Items)
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId);

        if (inventory == null)
            return BadRequest("Inventário não encontrado.");

        var cropItem = inventory.Items.FirstOrDefault(item =>
            item.ItemType == ItemType.Crop
            && item.ItemId == seed.CropId);

        if (cropItem == null)
        {
            cropItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventory.Id,
                ItemType = ItemType.Crop,
                ItemId = seed.CropId,
                Quantity = finalYield
            };
            _context.InventoryItems.Add(cropItem);
        }
        else
        {
            cropItem.Quantity += finalYield;
        }

        PestRules.CancelByHarvest(plot, now);
        plot.SeedId = null;
        plot.PlantedAt = null;
        plot.ReadyAt = null;
        plot.RemainingYield = null;
        CropCareRules.ClearCurrentOpportunity(plot);

        var xpGained = 25 * seed.MinLevel;
        _experienceService.AddXp(lockedUser, xpGained);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            plot.Id,
            crop = seed.CropId,
            amount = finalYield,
            inventoryTotal = cropItem.Quantity,
            xpGained
        });
    }

    private async Task<Guid?> FindFarmIdAsync(Guid plotId) =>
        await _context.Plots
            .AsNoTracking()
            .Where(candidate => candidate.Id == plotId)
            .Select(candidate => (Guid?)candidate.FarmId)
            .SingleOrDefaultAsync();

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);

    private ObjectResult YieldUnavailable() =>
        Problem(
            detail:
                "O rendimento persistido deste lote está ausente ou inválido. Nenhuma colheita foi concedida.",
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Rendimento do lote indisponível");
}
