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
    private const int MaxQuantityPerTransaction = 10_000;

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
        if (request.Quantity is <= 0 or > MaxQuantityPerTransaction)
            return BadRequest(
                $"A quantidade deve estar entre 1 e {MaxQuantityPerTransaction}.");

        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        await using var transaction =
            await _context.Database.BeginTransactionAsync();
        var user = await _context.LockAsync(userId);

        if (user == null)
            return Unauthorized();

        // 1️ Buscar seed
        var seed = await _context.Seeds
            .FirstOrDefaultAsync(s => s.Id == request.SeedId);

        if (seed == null)
            return BadRequest("Seed inválida");

        if (seed.BuyPrice <= 0)
            return Problem(
                "O preço de compra da semente está inválido.",
                statusCode: StatusCodes.Status500InternalServerError);

        // 2️ Buscar inventário
        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (inventory == null)
            return BadRequest("Inventário não encontrado");

        var totalCost = (long)seed.BuyPrice * request.Quantity;

        // 3️ Verificar coins
        if (inventory.Coins < totalCost)
            return BadRequest("Coins insuficientes");

        if (user.Level < seed.MinLevel)
            return BadRequest("Level insuficiente para comprar esta seed");

        // 4️ Calcular a nova quantidade sem risco de overflow
        var seedItem = await _context.InventoryItems.FirstOrDefaultAsync(i =>
            i.InventoryId == inventory.Id &&
            i.ItemType == ItemType.Seed &&
            i.ItemId == seed.Id
        );

        var currentQuantity = seedItem?.Quantity ?? 0;
        var updatedQuantity = (long)currentQuantity + request.Quantity;

        if (updatedQuantity > int.MaxValue)
            return BadRequest("O limite de estoque desta semente foi atingido.");

        // 5️ Debitar coins e adicionar a seed ao inventário
        inventory.Coins -= (int)totalCost;

        if (seedItem == null)
        {
            seedItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventory.Id,
                ItemType = ItemType.Seed,
                ItemId = seed.Id,
                Quantity = (int)updatedQuantity
            };

            _context.InventoryItems.Add(seedItem);
        }
        else
        {
            seedItem.Quantity = (int)updatedQuantity;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

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
        if (request.Quantity is <= 0 or > MaxQuantityPerTransaction)
            return BadRequest(
                $"A quantidade deve estar entre 1 e {MaxQuantityPerTransaction}.");

        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        await using var transaction =
            await _context.Database.BeginTransactionAsync();
        var user = await _context.LockAsync(userId);

        if (user == null)
            return Unauthorized();

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

        if (seed.SellPrice <= 0)
            return Problem(
                "O preço de venda da colheita está inválido.",
                statusCode: StatusCodes.Status500InternalServerError);

        // 4️ Calcular ganho e saldo sem risco de overflow
        var totalCoins = (long)seed.SellPrice * request.Quantity;
        var updatedCoins = (long)inventory.Coins + totalCoins;

        if (totalCoins > int.MaxValue || updatedCoins > int.MaxValue)
            return BadRequest("O limite de moedas do inventário foi atingido.");

        // 5️ Atualizar inventário
        cropItem.Quantity -= request.Quantity;

        if (cropItem.Quantity == 0)
            _context.InventoryItems.Remove(cropItem);

        inventory.Coins = (int)updatedCoins;

        _experienceService.AddXp(user, 5);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            crop = request.CropId,
            sold = request.Quantity,
            earned = (int)totalCoins,
            coins = inventory.Coins
        });
}

}
