using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FarmAndFriends.Api.Infrastructure.Data;
using FarmAndFriends.Api.Dtos.Users;
using FarmAndFriends.Api.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using FarmAndFriends.Api.Contracts.Friends;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return Unauthorized();
            
        var xpToNextLevel = LevelProgression.XpToNextLevel(user.Level);

        var dto = new MeResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Level = user.Level,
            CurrentXp = user.CurrentXp,
            XpToNextLevel = xpToNextLevel
        };

        return Ok(dto);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] int take = 20)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Informe ao menos parte do nome de usu\u00e1rio.");

        var normalizedQuery = q.Trim().ToUpperInvariant();
        var limit = Math.Clamp(take, 1, 20);

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id != userId && u.NormalizedUsername.StartsWith(normalizedQuery))
            .OrderBy(u => u.Username)
            .Take(limit)
            .Select(u => new UserSearchResponse(u.Id, u.Username))
            .ToListAsync();

        return Ok(users);
    }
}
