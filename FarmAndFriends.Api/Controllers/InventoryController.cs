using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FarmAndFriends.Api.Infrastructure.Data;
using FarmAndFriends.Api.Dtos.Inventory;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public InventoryController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<InventoryResponseDto>> GetInventory()
    {
        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        var inventory = await _context.Inventories
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (inventory == null)
            return NotFound("Inventário não encontrado");

        var response = new InventoryResponseDto(
            inventory.Coins,
            inventory.PremiumCoins,
            inventory.Items.Select(i =>
                new InventoryItemDto(
                    i.ItemType.ToString(),
                    i.ItemId,
                    i.Quantity
                )
            ).ToList()
        );

        return Ok(response);
    }
}
