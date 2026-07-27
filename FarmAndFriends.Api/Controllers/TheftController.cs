using System.Security.Claims;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
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

    public TheftController(
        AppDbContext context,
        TheftService theftService,
        ExperienceService experienceService,
        FriendshipService friendshipService)
    {
        _context = context;
        _theftService = theftService;
        _experienceService = experienceService;
        _friendshipService = friendshipService;
    }

    [HttpPost]
    public async Task<IActionResult> Steal(Guid farmId, Guid plotId)
    {
        var thiefUserId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        await using var transaction =
            await _context.Database.BeginTransactionAsync();
        var thief = await _context.LockAsync(thiefUserId);

        if (thief == null)
            return Unauthorized();

        // 1️⃣ Carregar plot + seed + farm
        var plot = await _context.Plots
            .Include(p => p.Farm)
            .FirstOrDefaultAsync(p => p.Id == plotId && p.FarmId == farmId);

        if (plot == null)
            return NotFound("Plot não encontrado");

        if (plot.Farm.UserId == thiefUserId)
            return BadRequest("Voc\u00ea n\u00e3o pode roubar a pr\u00f3pria fazenda.");

        var areFriends = await _friendshipService.AreFriendsAsync(
            thiefUserId,
            plot.Farm.UserId);

        if (!areFriends)
            return Forbid();

        if (plot.SeedId == null)
            return BadRequest("Nada plantado");
        
        if (!plot.IsReady)
            return BadRequest("Plantação não está pronta");

        var seed = await _context.Seeds.FindAsync(plot.SeedId);
        if (seed == null)
            return BadRequest("Seed inválida");

        var stolenFromThisPlant = await _context.TheftLogs
            .Where(t =>
                t.PlotId == plot.Id &&
                t.CreatedAt >= plot.PlantedAt
            )
            .SumAsync(t => t.Quantity);

        // 3️⃣ Quantidade restante de yield
        var remainingYield = Math.Max(
            1,
            seed.CropAmount - stolenFromThisPlant
        );

        // 4️⃣ Quanto já roubou hoje nessa farm
        var today = DateTime.UtcNow.Date;

        var alreadyStolenToday = await _context.TheftLogs
            .Where(t =>
                t.FarmId == farmId &&
                t.ThiefUserId == thiefUserId &&
                t.CreatedAt >= today)
            .SumAsync(t => t.Quantity);

        // 5️⃣ Regra do jogo
        TheftResult result;
        try
        {
            result = _theftService.Steal(
                remainingYield,
                thief.Level,
                alreadyStolenToday
            );
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        // Atualizar yield do plot
        plot.RemainingYield = remainingYield - result.StolenAmount;

        // 6️⃣ Inventário do ladrão
        var inventory = await _context.Inventories
            .Include(i => i.Items)
            .FirstAsync(i => i.UserId == thiefUserId);

        var cropItem = inventory.Items.FirstOrDefault(i =>
            i.ItemType == ItemType.Crop &&
            i.ItemId == seed.CropId
        );

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

        cropItem.Quantity += result.StolenAmount;

        // 7️⃣ Log do roubo
        var now = DateTime.UtcNow;
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

        // 8️⃣ Notificação persistente para o dono da fazenda
        var theftMessage = result.GotBonus
            ? $"{thief.Username} roubou {result.StolenAmount} unidades de {seed.Name.ToLower()} da sua fazenda."
            : $"{thief.Username} roubou {result.StolenAmount} {seed.Name.ToLower()} da sua fazenda.";

        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = plot.Farm.UserId,
            ActorUserId = thiefUserId,
            Type = NotificationType.TheftOccurred,
            Message = theftMessage,
            TheftLogId = theftLog.Id,
            CreatedAt = now
        });

        // 9️⃣ Ganho de XP por roubo
        if (result.XpGained > 0)
        {
            _experienceService.AddXp(thief, result.XpGained);
        }

        // ⚠️ IMPORTANTE: NÃO limpamos o plot
        // O dono ainda vai colher o restante

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            plot.Id,
            stolen = result.StolenAmount,
            ownerWillReceive = result.OwnerAmount,
            xpGained = result.XpGained
        });
    }
}
