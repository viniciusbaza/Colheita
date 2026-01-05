using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FarmAndFriends.Api.Infrastructure.Data;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Dtos.Shop;
using FarmAndFriends.Api.Contracts.Shop;
using FarmAndFriends.Api.Domain.Services;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("shop")]
[Authorize]
public class ShopController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ExperienceService _experienceService;

    public ShopController(AppDbContext context, ExperienceService experienceService)
    {
        _context = context;
        _experienceService = experienceService;
    }

    [HttpPost("buy-seed")]
    public async Task<IActionResult> BuySeed([FromBody] BuySeedRequest request)
    {
        if (request.Quantity <= 0)
            return BadRequest("Quantidade inválida");

        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );
        // 0️ Buscar usuário
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return Unauthorized();

        // 1️ Buscar seed
        var seed = await _context.Seeds
            .FirstOrDefaultAsync(s => s.Id == request.SeedId);

        if (seed == null)
            return BadRequest("Seed inválida");

        // 2️ Buscar inventário
        var inventory = await _context.Inventories
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (inventory == null)
            return BadRequest("Inventário não encontrado");

        var totalCost = seed.BuyPrice * request.Quantity;

        // 3️ Verificar coins
        if (inventory.Coins < totalCost)
            return BadRequest("Coins insuficientes");

        if (user.Level < seed.MinLevel)
            return BadRequest("Level insuficiente para comprar esta seed");

        // 4️ Debitar coins
        inventory.Coins -= totalCost;

        // 5️ Adicionar seed ao inventário
        var seedItem = await _context.InventoryItems.FirstOrDefaultAsync(i =>
            i.InventoryId == inventory.Id &&
            i.ItemType == ItemType.Seed &&
            i.ItemId == seed.Id
        );

        if (seedItem == null)
        {
            seedItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventory.Id,
                ItemType = ItemType.Seed,
                ItemId = seed.Id,
                Quantity = request.Quantity
            };

            _context.InventoryItems.Add(seedItem);
        }
        else
        {
            seedItem.Quantity += request.Quantity;
        }

        await _context.SaveChangesAsync();

        return Ok(new BuySeedResponse(
            seed.Id,
            request.Quantity,
            inventory.Coins
        ));
    }

    [Authorize]
    [HttpPost("sell-crop")]
    public async Task<IActionResult> SellCrop([FromBody] SellCropRequest request)
    {
        if (request.Quantity <= 0)
            return BadRequest("Quantidade inválida");

        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        // 1️ Inventário
        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (inventory == null)
            return BadRequest("Inventário não encontrado");

        // 2️ Item do inventário (Crop)
        var cropItem = await _context.InventoryItems.FirstOrDefaultAsync(i =>
            i.InventoryId == inventory.Id &&
            i.ItemType == ItemType.Crop &&
            i.ItemId == request.CropId
        );

        if (cropItem == null || cropItem.Quantity < request.Quantity)
            return BadRequest("Quantidade insuficiente");

        // 3️ Buscar Seed dona do crop
        var seed = await _context.Seeds
            .FirstOrDefaultAsync(s => s.CropId == request.CropId);

        if (seed == null)
            return BadRequest("Crop inválido");

        // 4️ Calcular ganho
        var totalCoins = seed.SellPrice * request.Quantity;

        // 5️ Atualizar inventário
        cropItem.Quantity -= request.Quantity;

        if (cropItem.Quantity == 0)
            _context.InventoryItems.Remove(cropItem);

        inventory.Coins += totalCoins;

        await _experienceService.AddXpAsync(userId, 5);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            crop = request.CropId,
            sold = request.Quantity,
            earned = totalCoins,
            coins = inventory.Coins
        });
}

}