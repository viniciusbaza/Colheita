using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Plots;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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

        try
        {
            CropCycleRules.ValidateSeedConfiguration(seed);
        }
        catch (InvalidOperationException)
        {
            return CropCycleStateInvalid();
        }

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
        CropCycleRules.StartPlanting(plot, seed, now);
        PestRules.ResetCycle(plot);
        CropCareRules.StartCurrentCropCycle(plot);

        const int xpGained = 10;
        _experienceService.AddXp(lockedUser, xpGained);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new PlantResponse(
            plot.Id,
            seed.Name,
            plot.PlantedAt!.Value,
            plot.ReadyAt!.Value,
            plot.CurrentHarvestCycle!.Value,
            xpGained));
    }

    [HttpPost("{plotId}/harvest")]
    public async Task<IActionResult> Harvest(
        Guid plotId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        HarvestCropRequest? request = null)
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
        {
            if (request?.ExpectedHarvestCycle != null)
            {
                return HarvestCycleMismatch(
                    request.ExpectedHarvestCycle,
                    currentHarvestCycle: null);
            }

            return BadRequest("Nenhuma plantação para colher.");
        }

        var seed = await _context.Seeds
            .FirstOrDefaultAsync(candidate => candidate.Id == plot.SeedId);

        if (seed == null)
            return CropCycleStateInvalid();

        try
        {
            CropCycleRules.ValidateCurrentCycle(plot, seed);
        }
        catch (InvalidOperationException)
        {
            return CropCycleStateInvalid();
        }

        if (seed.HarvestCycles > 1
            && request?.ExpectedHarvestCycle
                != plot.CurrentHarvestCycle)
        {
            return HarvestCycleMismatch(
                request?.ExpectedHarvestCycle,
                plot.CurrentHarvestCycle);
        }

        if (plot.ReadyAt == null || plot.ReadyAt > now)
            return BadRequest("A plantação ainda não está pronta.");

        if (plot.RemainingYield is not int finalYield
            || finalYield < 1)
        {
            return YieldUnavailable();
        }

        var inventory = await _context.Inventories
            .Include(candidate => candidate.Items)
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId);

        if (inventory == null)
            return BadRequest("Inventário não encontrado.");

        var cropItem = inventory.Items.FirstOrDefault(item =>
            item.ItemType == ItemType.Crop
            && item.ItemId == seed.CropId);

        var inventoryTotal = (long)(cropItem?.Quantity ?? 0) + finalYield;
        if (inventoryTotal is < 0 or > int.MaxValue)
            return InventoryCapacityExceeded();

        if (cropItem == null)
        {
            cropItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventory.Id,
                ItemType = ItemType.Crop,
                ItemId = seed.CropId,
                Quantity = (int)inventoryTotal
            };
            _context.InventoryItems.Add(cropItem);
        }
        else
        {
            cropItem.Quantity = (int)inventoryTotal;
        }

        PestRules.CancelByHarvest(plot, now);
        var transition = CropCycleRules.CompleteHarvest(plot, seed, now);
        if (transition.HasNextCycle)
        {
            PestRules.ResetCycle(plot);
            CropCareRules.StartCurrentCropCycle(plot);
        }
        else
        {
            CropCareRules.ClearCurrentOpportunity(plot);
        }

        var xpGained = checked(25 * seed.MinLevel);
        _experienceService.AddXp(lockedUser, xpGained);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new HarvestResponse(
            plot.Id,
            seed.CropId,
            finalYield,
            cropItem.Quantity,
            xpGained,
            transition.CurrentHarvestCycle,
            transition.ReadyAt));
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

    private ObjectResult HarvestCycleMismatch(
        int? expectedHarvestCycle,
        int? currentHarvestCycle)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Ciclo de colheita desatualizado",
            Detail =
                "A plantação avançou ou foi encerrada. Atualize o lote antes de colher novamente."
        };
        problem.Extensions["code"] = PlotErrorCodes.HarvestCycleMismatch;
        problem.Extensions["expectedHarvestCycle"] = expectedHarvestCycle;
        problem.Extensions["currentHarvestCycle"] = currentHarvestCycle;
        return StatusCode(StatusCodes.Status409Conflict, problem);
    }

    private ObjectResult CropCycleStateInvalid()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Estado produtivo do lote inválido",
            Detail =
                "O ciclo produtivo persistido está inconsistente. Nenhuma recompensa foi concedida."
        };
        problem.Extensions["code"] = PlotErrorCodes.CropCycleStateInvalid;
        return StatusCode(StatusCodes.Status500InternalServerError, problem);
    }

    private ObjectResult InventoryCapacityExceeded()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Limite de estoque atingido",
            Detail =
                "Não há espaço numérico para adicionar esta colheita ao inventário. Nenhuma recompensa foi concedida."
        };
        problem.Extensions["code"] =
            PlotErrorCodes.InventoryCapacityExceeded;
        return StatusCode(StatusCodes.Status409Conflict, problem);
    }
}
