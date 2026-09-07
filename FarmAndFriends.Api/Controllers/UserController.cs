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
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || userId == Guid.Empty)
            return Unauthorized();
        var profile = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                User = user,
                Farm = user.Farms
                    .Select(farm => new { farm.Id, farm.Name })
                    .SingleOrDefault()
            })
            .SingleOrDefaultAsync();

        if (profile == null)
            return Unauthorized();

        if (profile.Farm == null)
            return NotFound("Fazenda não encontrada.");

        var user = profile.User;
            
        var xpToNextLevel = LevelProgression.XpToNextLevel(user.Level);

        var dto = new MeResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            AvatarId = user.AvatarId,
            FarmId = profile.Farm.Id,
            FarmName = profile.Farm.Name,
            Level = user.Level,
            CurrentXp = user.CurrentXp,
            XpToNextLevel = xpToNextLevel
        };

        return Ok(dto);
    }

    [HttpPatch("me/avatar")]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || userId == Guid.Empty)
            return Unauthorized();

        if (!ProfileAvatarRules.IsAllowed(request.AvatarId))
            return BadRequest("Escolha um avatar disponível no jogo.");

        // Update only this field, preserving concurrent XP and account changes.
        var updated = await _context.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.AvatarId, request.AvatarId!));

        return updated == 0
            ? Unauthorized()
            : Ok(new AvatarResponse(request.AvatarId!));
    }

    [HttpPatch("me/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest? request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || userId == Guid.Empty)
            return Unauthorized();

        if (request == null || !ProfileAvatarRules.IsAllowed(request.AvatarId))
            return BadRequest("Escolha um avatar disponível no jogo.");

        var farmName = FarmNameRules.Normalize(request.FarmName);
        if (farmName == null)
            return BadRequest("Nome da fazenda é obrigatório.");

        if (farmName.Length > FarmNameRules.MaxLength)
            return BadRequest($"Nome da fazenda deve ter no máximo {FarmNameRules.MaxLength} caracteres.");

        var user = await _context.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId);
        if (user == null)
            return Unauthorized();

        var farm = await _context.Farms.SingleOrDefaultAsync(candidate => candidate.UserId == userId);
        if (farm == null)
            return NotFound("Fazenda não encontrada.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        user.AvatarId = request.AvatarId!;
        farm.Name = farmName;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new ProfileResponse(user.AvatarId, farm.Id, farm.Name));
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
