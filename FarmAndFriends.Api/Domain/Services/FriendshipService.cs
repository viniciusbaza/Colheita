using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Domain.Services;

public class FriendshipService
{
    private readonly AppDbContext _context;

    public FriendshipService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> AreFriendsAsync(Guid firstUserId, Guid secondUserId)
    {
        if (firstUserId == secondUserId)
            return false;

        var (userAId, userBId) = GetCanonicalPair(firstUserId, secondUserId);

        return await _context.Friendships
            .AsNoTracking()
            .AnyAsync(friendship =>
                friendship.UserAId == userAId &&
                friendship.UserBId == userBId &&
                friendship.Status == FriendshipStatus.Accepted);
    }

    public static (Guid UserAId, Guid UserBId) GetCanonicalPair(
        Guid firstUserId,
        Guid secondUserId)
    {
        if (firstUserId == secondUserId)
            throw new ArgumentException("A amizade precisa envolver dois usu\u00e1rios diferentes.");

        return firstUserId.CompareTo(secondUserId) < 0
            ? (firstUserId, secondUserId)
            : (secondUserId, firstUserId);
    }
}
