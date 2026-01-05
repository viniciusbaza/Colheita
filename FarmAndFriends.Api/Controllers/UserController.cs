using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FarmAndFriends.Api.Infrastructure.Data;
using FarmAndFriends.Api.Dtos.Users;
using FarmAndFriends.Api.Domain.Rules;
using Microsoft.EntityFrameworkCore;

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
}
