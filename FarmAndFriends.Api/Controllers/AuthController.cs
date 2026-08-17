using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FarmAndFriends.Api.Infrastructure.Data;
using FarmAndFriends.Api.Infrastructure.Auth;
using FarmAndFriends.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;

    public AuthController(AppDbContext context, TokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var normalizedUsername = NormalizeUsername(request.Username);
        if (normalizedUsername == null)
            return Unauthorized("Usu\u00e1rio ou senha inv\u00e1lidos");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.NormalizedUsername == normalizedUsername);

        if (user == null)
            return Unauthorized("Usuário ou senha inválidos");

        var passwordHasher = new PasswordHasher<User>();

        var result = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password
        );

        if (result == PasswordVerificationResult.Failed)
            return Unauthorized("Usuário ou senha inválidos");

        // 🔐 Access token
        var accessToken = _tokenService.GenerateToken(user);
        // 🔁 Refresh token
        var refreshToken = _tokenService.GenerateRefreshToken();;

        _context.RefreshTokens.Add(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                Revoked = false
            }
        );

        await _context.SaveChangesAsync();

        // 🍪 Cookie HttpOnly
        Response.Cookies.Append(
            "refresh_token",
            refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = HttpContext.Request.IsHttps, // true em produção HTTPS
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(7)
            }
        );

        return Ok(new
        {
            access_token = accessToken,
           // refresh_token = refreshToken
        });
    }

    public record RegisterRequest(string Username, string Password, string FarmName);

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var username = request.Username?.Trim();
        var normalizedUsername = NormalizeUsername(username);

        if (normalizedUsername == null)
            return BadRequest("Nome de usu\u00e1rio \u00e9 obrigat\u00f3rio");

        if (username!.Length > 30)
            return BadRequest("Nome de usu\u00e1rio muito longo");

        var exists = await _context.Users
            .AnyAsync(u => u.NormalizedUsername == normalizedUsername);

        if (exists)
            return BadRequest("Usuário já existe");

        var passwordHasher = new PasswordHasher<User>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            NormalizedUsername = normalizedUsername
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        if (string.IsNullOrWhiteSpace(request.FarmName))
            return BadRequest("Nome da fazenda é obrigatório");
        
        if (request.FarmName.Length > 30)
            return BadRequest("Nome da fazenda muito longo");

        // 🏡 Criar Farm inicial
        var farm = new Farm
        {
            Id = Guid.NewGuid(),
            Name = request.FarmName,
            UserId = user.Id
        };

        foreach (var plot in FarmLayoutRules.CreateInitialPlots(farm.Id))
            farm.Plots.Add(plot);

        // 🧰 Inventário inicial
        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Coins = 100,
            PremiumCoins = 10
        };

        var starterItems = new List<InventoryItem>
        {
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventory.Id,
                ItemType = ItemType.Seed,
                ItemId = "corn",
                Quantity = 10
            },
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventory.Id,
                ItemType = ItemType.Seed,
                ItemId = "carrot",
                Quantity = 5
            },
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventory.Id,
                ItemType = ItemType.Seed,
                ItemId = "tomato",
                Quantity = 10
            }
        };

        _context.Users.Add(user);
        _context.Farms.Add(farm);
        _context.Inventories.Add(inventory);
        _context.InventoryItems.AddRange(starterItems);

        await _context.SaveChangesAsync();

        return Created("", new
        {
            UserId = user.Id,
            Username = user.Username,
            FarmId = farm.Id
        });
    }

    private static string? NormalizeUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        return username.Trim().ToUpperInvariant();
    }

    //public record RefreshRequest(string RefreshToken);
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue("refresh_token", out var refreshToken))
            return Unauthorized("Refresh token não fornecido");

        var storedToken = await _context.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r =>
                r.Token == refreshToken &&
                !r.Revoked &&
                r.ExpiresAt > DateTime.UtcNow
            );

        if (storedToken == null)
            return Unauthorized("Refresh token inválido");

        // 🔒 Revoga o token antigo
        storedToken.Revoked = true;

        // 🔁 Gera novos tokens
        var newAccessToken = _tokenService.GenerateToken(storedToken.User);
        var newRefreshTokenValue = _tokenService.GenerateRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = storedToken.UserId,
            Token = newRefreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        await _context.SaveChangesAsync();

        // 🍪 Atualiza cookie
        Response.Cookies.Append(
            "refresh_token",
            newRefreshTokenValue,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = HttpContext.Request.IsHttps, // true em produção HTTPS
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(7)
            }
        );

        return Ok(new
        {
            access_token = newAccessToken
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue("refresh_token", out var token))
        {
            var stored = await _context.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == token);

            if (stored != null)
            {
                stored.Revoked = true;
                await _context.SaveChangesAsync();
            }
        }

        Response.Cookies.Delete("refresh_token");

        return Ok();
    }
}
