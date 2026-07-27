using FarmAndFriends.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Infrastructure.Data;

public static class PlayerEconomyLock
{
    public static async Task<User?> LockAsync(
        this AppDbContext context,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException(
                "The player economy lock requires an explicit transaction.");
        }

        // FOR NO KEY UPDATE serializes this player's mutable economy while
        // remaining compatible with the KEY SHARE locks used by user FKs.
        return await context.Users
            .FromSqlInterpolated(
                $"""
                SELECT * FROM "Users"
                WHERE "Id" = {userId}
                FOR NO KEY UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
