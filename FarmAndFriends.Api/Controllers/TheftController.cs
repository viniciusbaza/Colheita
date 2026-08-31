using System.Security.Claims;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Dtos.Theft;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Authorize]
[Route("farms/{farmId}/plots/{plotId}/steal")]
public class TheftController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TheftService _theftService;
    private readonly ExperienceService _experienceService;
    private readonly FriendshipService _friendshipService;
    private readonly PestService _pestService;

    public TheftController(
        AppDbContext context,
        TheftService theftService,
        ExperienceService experienceService,
        FriendshipService friendshipService,
        PestService pestService)
    {
        _context = context;
        _theftService = theftService;
        _experienceService = experienceService;
        _friendshipService = friendshipService;
        _pestService = pestService;
    }

    [HttpPost]
    public async Task<IActionResult> Steal(Guid farmId, Guid plotId)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var thiefUserId))
        {
            return Unauthorized();
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();
        var thief = await _context.LockAsync(thiefUserId);

        if (thief == null)
            return Unauthorized();

        var farm = await _context.LockFarmAsync(farmId);
        if (farm == null)
            return NotFound("Fazenda não encontrada.");

        var plots = await _context.LockFarmPlotsAsync(farmId);
        var plot = plots.SingleOrDefault(candidate => candidate.Id == plotId);

        if (plot == null || !plot.Unlocked)
            return NotFound("Plot não encontrado.");

        if (farm.UserId == thiefUserId)
            return BadRequest("Você não pode roubar a própria fazenda.");

        if (!await _friendshipService.AreFriendsAsync(
                thiefUserId,
                farm.UserId))
        {
            return Forbid();
        }

        var now = _pestService.UtcNow;
        await _pestService.ProcessLockedFarmAsync(farm, plots, now);

        if (plot.SeedId == null)
            return BadRequest("Nada plantado.");

        var seed = await _context.Seeds.FindAsync(plot.SeedId);
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

        if (plot.ReadyAt == null || plot.ReadyAt > now)
            return BadRequest("Plantação não está pronta.");

        if (plot.RemainingYield is not int remainingYield
            || remainingYield < 1)
        {
            return YieldUnavailable();
        }

        var today = now.Date;
        var alreadyStolenToday = await _context.TheftLogs
            .Where(log =>
                log.FarmId == farmId
                && log.ThiefUserId == thiefUserId
                && log.CreatedAt >= today)
            .SumAsync(log => log.Quantity);

        TheftResult result;
        try
        {
            result = _theftService.Steal(
                remainingYield,
                thief.Level,
                alreadyStolenToday);
        }
        catch (InvalidOperationException exception)
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return BadRequest(exception.Message);
        }

        var inventory = await _context.Inventories
            .Include(candidate => candidate.Items)
            .FirstAsync(candidate => candidate.UserId == thiefUserId);
        var cropItem = inventory.Items.FirstOrDefault(item =>
            item.ItemType == ItemType.Crop
            && item.ItemId == seed.CropId);

        var inventoryTotal = (long)(cropItem?.Quantity ?? 0)
            + result.StolenAmount;
        if (inventoryTotal is < 0 or > int.MaxValue)
            return InventoryCapacityExceeded();

        if (cropItem == null)
        {
            cropItem = new InventoryItem
            {
                InventoryId = inventory.Id,
                ItemType = ItemType.Crop,
                ItemId = seed.CropId,
                Quantity = 0
            };
            inventory.Items.Add(cropItem);
        }

        var ownerWillReceive = remainingYield - result.StolenAmount;
        plot.RemainingYield = ownerWillReceive;
        var pestCancelled = PestRules.CancelActiveByTheft(plot, now);
        cropItem.Quantity = (int)inventoryTotal;

        var theftLog = new TheftLog
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            PlotId = plot.Id,
            ThiefUserId = thiefUserId,
            SeedId = seed.Id,
            Quantity = result.StolenAmount,
            GotBonus = result.GotBonus,
            CreatedAt = now
        };
        _context.TheftLogs.Add(theftLog);

        var theftMessage = result.GotBonus
            ? $"{thief.Username} roubou {result.StolenAmount} unidades de {seed.CropName.ToLowerInvariant()} da sua fazenda."
            : $"{thief.Username} roubou {result.StolenAmount} {seed.CropName.ToLowerInvariant()} da sua fazenda.";
        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = farm.UserId,
            ActorUserId = thiefUserId,
            Type = NotificationType.TheftOccurred,
            Message = theftMessage,
            TheftLogId = theftLog.Id,
            CreatedAt = now
        });

        if (result.XpGained > 0)
            _experienceService.AddXp(thief, result.XpGained);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new TheftResponse(
            plot.Id,
            result.StolenAmount,
            ownerWillReceive,
            result.XpGained,
            pestCancelled));
    }

    private ObjectResult YieldUnavailable() =>
        Problem(
            detail:
                "O rendimento persistido deste lote está ausente ou inválido. Nenhum roubo foi concedido.",
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Rendimento do lote indisponível");

    private ObjectResult InventoryCapacityExceeded()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Capacidade do inventário excedida",
            Detail =
                "A colheita roubada excederia a capacidade do inventário. Nenhum roubo foi concedido."
        };
        problem.Extensions["code"] =
            FarmAndFriends.Api.Contracts.Plots.PlotErrorCodes
                .InventoryCapacityExceeded;
        return StatusCode(StatusCodes.Status409Conflict, problem);
    }

    private ObjectResult CropCycleStateInvalid()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Estado produtivo do lote inválido",
            Detail =
                "O ciclo produtivo persistido está inconsistente. Nenhum roubo foi concedido."
        };
        problem.Extensions["code"] =
            FarmAndFriends.Api.Contracts.Plots.PlotErrorCodes
                .CropCycleStateInvalid;
        return StatusCode(StatusCodes.Status500InternalServerError, problem);
    }
}
