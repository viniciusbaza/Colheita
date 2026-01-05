using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FarmAndFriends.Api.Infrastructure.Data;
using FarmAndFriends.Api.Infrastructure.Auth;
using FarmAndFriends.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;

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

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

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

        var token = _tokenService.GenerateToken(user);

        return Ok(new
        {
            access_token = token
        });
    }

    public record RegisterRequest(string Username, string Password);

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var exists = await _context.Users
            .AnyAsync(u => u.Username == request.Username);

        if (exists)
            return BadRequest("Usuário já existe");

        var passwordHasher = new PasswordHasher<User>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        // 🏡 Criar Farm inicial
        var farm = new Farm
        {
            Id = Guid.NewGuid(),
            Name = "Minha Fazenda",
            UserId = user.Id
        };

        // 🌱 Grid 3x3
        const int width = 3;
        const int height = 3;
        const int unlockedPlots = 6;

        int counter = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                farm.Plots.Add(new Plot
                {
                    Id = Guid.NewGuid(),
                    X = x,
                    Y = y,
                    Unlocked = counter < unlockedPlots,
                    SeedId = null,
                    PlantedAt = null,
                    ReadyAt = null
                });

                counter++;
            }
        }

        // 🧰 Inventário inicia
        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Coins = 100,
            PremiumCoins = 10
        };

        _context.Users.Add(user);
        _context.Farms.Add(farm);
        _context.Inventories.Add(inventory);

        await _context.SaveChangesAsync();

        return Created("", new
        {
            UserId = user.Id,
            Username = user.Username,
            FarmId = farm.Id
        });
    }

}

public record LoginRequest(string Username, string Password);
