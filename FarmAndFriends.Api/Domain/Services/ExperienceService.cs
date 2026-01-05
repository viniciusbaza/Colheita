using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Domain.Services;

public class ExperienceService
{
    private readonly AppDbContext _context;

    public ExperienceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddXpAsync(Guid userId, int xp)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new Exception("Usuário não encontrado");

        user.CurrentXp += xp;

        while (user.CurrentXp >= LevelProgression.XpToNextLevel(user.Level))
        {
            user.CurrentXp -= LevelProgression.XpToNextLevel(user.Level);
            user.Level++;
        }

        await _context.SaveChangesAsync();
    }   
}
