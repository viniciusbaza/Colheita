using FarmAndFriends.Api.Contracts.Plots;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("plots")]
[Authorize]
public class PlotController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ExperienceService _experienceService;

    public PlotController(AppDbContext context, ExperienceService experienceService)
    {
        _context = context;
        _experienceService = experienceService;
    }

    [HttpPost("{plotId}/plant")]
    public async Task<IActionResult> Plant(
        Guid plotId,
        [FromBody] PlantSeedRequest request)
    {
        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        // 1 Buscar o plot com a farm e o user
        var plot = await _context.Plots
            .Include(p => p.Farm)
            .ThenInclude(f => f.User)
            .FirstOrDefaultAsync(p => p.Id == plotId);

        if (plot == null)
            return NotFound("Plot não encontrado");

        if (plot.Farm.User.Id != userId)
            return Forbid();

        if (!plot.Unlocked)
            return BadRequest("Plot está bloqueado");

        if (plot.SeedId != null)
            return BadRequest("Plot já está plantado");
        
        // 2 Buscar a seed
        var seed = await _context.Seeds.FirstOrDefaultAsync(s => s.Id == request.SeedId);
        
        if (seed == null)
            return BadRequest("Seed inválida");

        // 3️ Buscar inventário do usuário
        var inventory = await _context.Inventories
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (inventory == null)
            return BadRequest("Inventário não encontrado");

        // 4️ Verificar se possui a seed
        var seedItem = inventory.Items.FirstOrDefault(i =>
            i.ItemType == ItemType.Seed &&
            i.ItemId == seed.Id
        );

        if (seedItem == null || seedItem.Quantity <= 0)
            return BadRequest("Você não possui essa seed");

        if (seedItem.Quantity < 1)
            return BadRequest("Você não possui essa seed");

        // 5️ Consumir a seed
        seedItem.Quantity -= 1;

        if (seedItem.Quantity == 0)
            _context.InventoryItems.Remove(seedItem);

        // 6️ Plantar
        var now = DateTime.UtcNow;

        plot.SeedId = seed.Id;
        plot.PlantedAt = now;
        plot.ReadyAt = now.Add(seed.GrowTime);

        await _experienceService.AddXpAsync(userId, 5);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            plot.Id,
            seed = seed.Name,
            plot.PlantedAt,
            plot.ReadyAt
        });
    }

    [HttpPost("{plotId}/harvest")]
    public async Task<IActionResult> Harvest(Guid plotId)
    {
        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        var plot = await _context.Plots
            .Include(p => p.Farm)
            .ThenInclude(f => f.User)
            .FirstOrDefaultAsync(p => p.Id == plotId);

        if (plot == null)
            return NotFound("Plantação não encontrada");

        if (plot.Farm.User.Id != userId)
            return Forbid();

        if (plot.SeedId == null)
            return BadRequest("Nenhuma plantação para colher");

        if (plot.ReadyAt == null || plot.ReadyAt > DateTime.UtcNow)
            return BadRequest("A plantação ainda não está pronta");

        // Buscar seed
        var seed = await _context.Seeds
            .FirstOrDefaultAsync(s => s.Id == plot.SeedId);

        if (seed == null)
            return BadRequest("Seed inválida");

        // Buscar inventário
        var inventory = await _context.Inventories
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (inventory == null)
            return BadRequest("Inventário não encontrado");

        // Consulta o roubo do plot
        var stolenAmount = await _context.TheftLogs
            .Where(t => t.PlotId == plot.Id)
            .SumAsync(t => t.Quantity);

        // Calcular yield final
        var baseYield = seed.CropAmount;
        var finalYield = Math.Max(1, baseYield - stolenAmount);

        // Adicionar crop ao inventário
        var cropItem = inventory.Items.FirstOrDefault(i =>
            i.ItemType == ItemType.Crop &&
            i.ItemId == seed.CropId
        );

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

        // Limpa o plot
        plot.SeedId = null;
        plot.PlantedAt = null;
        plot.ReadyAt = null;

        await _experienceService.AddXpAsync(userId, 25);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            plot.Id,
            crop = seed.CropId,
            amount = finalYield,
            inventoryTotal = cropItem.Quantity
        });
    }
}
