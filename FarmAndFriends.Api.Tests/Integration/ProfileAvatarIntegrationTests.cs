using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Friends;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Dtos.Users;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class ProfileAvatarIntegrationTests
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Avatar_MigratesExistingAccountsAndUpdatesOnlyAuthenticatedProfile()
    {
        var configured = Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var databaseName = $"avatar_test_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(configured) { Database = databaseName }.ConnectionString;
        await using var admin = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" }.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var ownerId = Guid.NewGuid();
            var friendId = Guid.NewGuid();
            var strangerId = Guid.NewGuid();
            var ownerFarmId = Guid.NewGuid();
            await using (var arrange = CreateContext(connection))
            {
                await arrange.GetService<IMigrator>().MigrateAsync("20260828041558_RepairTomatoMultiHarvestConfiguration");
                await arrange.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Users" ("Id", "Username", "NormalizedUsername", "PasswordHash", "Level", "CurrentXp")
                    VALUES ({ownerId}, 'avatar_owner', 'AVATAR_OWNER', 'test', 3, 42);
                    """);
                await arrange.Database.MigrateAsync();
                var existingUser = await arrange.Users.SingleAsync(user => user.Id == ownerId);
                Assert.Equal("avatar-1", existingUser.AvatarId);
                Assert.Equal(42, existingUser.CurrentXp);
                arrange.Users.AddRange(
                    new User { Id = friendId, Username = "Friend", NormalizedUsername = "FRIEND", PasswordHash = "test", AvatarId = "avatar-3" },
                    new User { Id = strangerId, Username = "Stranger", NormalizedUsername = "STRANGER", PasswordHash = "test" });
                arrange.Inventories.Add(new Inventory { Id = Guid.NewGuid(), UserId = ownerId, Coins = 123, PremiumCoins = 9 });
                arrange.Farms.AddRange(
                    new Farm { Id = ownerFarmId, UserId = ownerId, Name = "Owner farm" },
                    new Farm { Id = Guid.NewGuid(), UserId = friendId, Name = "Friend farm" });
                var pair = FriendshipService.GetCanonicalPair(ownerId, friendId);
                arrange.Friendships.Add(new Friendship
                {
                    Id = Guid.NewGuid(), UserAId = pair.UserAId, UserBId = pair.UserBId,
                    RequestedByUserId = ownerId, Status = FriendshipStatus.Accepted, CreatedAt = DateTime.UtcNow
                });
                await arrange.SaveChangesAsync();
            }

            await using var context = CreateContext(connection);
            // Keep a stale tracked user to prove a profile write cannot replace newer XP.
            await context.Users.SingleAsync(user => user.Id == ownerId);
            await using (var concurrent = CreateContext(connection))
                await concurrent.Users.Where(user => user.Id == ownerId)
                    .ExecuteUpdateAsync(update => update.SetProperty(user => user.CurrentXp, 55));
            var controller = new UsersController(context) { ControllerContext = Identity(ownerId) };
            foreach (var choice in new[] { "avatar-1", "avatar-2", "avatar-3", "avatar-4", "avatar-5", "avatar-5" })
            {
                var response = Assert.IsType<AvatarResponse>(Assert.IsType<OkObjectResult>(
                    await controller.UpdateAvatar(new(choice))).Value);
                Assert.Equal(choice, response.AvatarId);
            }
            Assert.IsType<BadRequestObjectResult>(await controller.UpdateAvatar(new("avatar-6")));
            Assert.IsType<BadRequestObjectResult>(
                await controller.UpdateProfile(new("avatar-4", "   ")));
            Assert.IsType<BadRequestObjectResult>(
                await controller.UpdateProfile(new("avatar-6", "New farm")));
            var profile = Assert.IsType<ProfileResponse>(Assert.IsType<OkObjectResult>(
                await controller.UpdateProfile(new("avatar-4", "  Sunny Acres  "))).Value);
            Assert.Equal("avatar-4", profile.AvatarId);
            Assert.Equal(ownerFarmId, profile.FarmId);
            Assert.Equal("Sunny Acres", profile.FarmName);

            // The legacy endpoint remains compatible and does not alter the farm name.
            var legacy = Assert.IsType<AvatarResponse>(Assert.IsType<OkObjectResult>(
                await controller.UpdateAvatar(new("avatar-5"))).Value);
            Assert.Equal("avatar-5", legacy.AvatarId);

            var me = Assert.IsType<MeResponseDto>(Assert.IsType<OkObjectResult>(await controller.Me()).Value);
            Assert.Equal("avatar-5", me.AvatarId);
            Assert.Equal(ownerFarmId, me.FarmId);
            Assert.Equal("Sunny Acres", me.FarmName);
            Assert.Equal(55, me.CurrentXp);
            Assert.Equal(3, me.Level);
            var unknown = new UsersController(context) { ControllerContext = Identity(Guid.NewGuid()) };
            Assert.IsType<UnauthorizedResult>(await unknown.UpdateAvatar(new("avatar-2")));
            Assert.IsType<UnauthorizedResult>(
                await unknown.UpdateProfile(new("avatar-2", "Unknown farm")));
            Assert.IsType<UnauthorizedResult>(await unknown.Me());

            var missingFarm = new UsersController(context) { ControllerContext = Identity(strangerId) };
            Assert.IsType<NotFoundObjectResult>(
                await missingFarm.UpdateProfile(new("avatar-2", "Missing farm")));
            Assert.IsType<NotFoundObjectResult>(await missingFarm.Me());

            var friends = new FriendsController(context) { ControllerContext = Identity(friendId) };
            var list = Assert.IsType<List<FriendResponse>>(Assert.IsType<OkObjectResult>(await friends.GetFriends()).Value);
            Assert.Equal(ownerId, Assert.Single(list).UserId);
            Assert.Equal("avatar-5", list[0].AvatarId);

            await using var verify = CreateContext(connection);
            Assert.Equal("avatar-3", (await verify.Users.FindAsync(friendId))!.AvatarId);
            Assert.Equal("avatar-1", (await verify.Users.FindAsync(strangerId))!.AvatarId);
            var wallet = await verify.Inventories.SingleAsync(item => item.UserId == ownerId);
            Assert.Equal(123, wallet.Coins);
            Assert.Equal(9, wallet.PremiumCoins);
            Assert.Empty(await verify.PremiumCurrencyTransactions.ToListAsync());
            Assert.Empty(await verify.Notifications.ToListAsync());

            // A database failure on the farm write must also roll back the avatar write.
            await verify.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Farms"
                ADD CONSTRAINT "CK_ProfileAtomicityTest"
                CHECK ("Name" <> 'Database failure');
                """);
            await using (var atomicContext = CreateContext(connection))
            {
                var atomicController = new UsersController(atomicContext)
                {
                    ControllerContext = Identity(ownerId)
                };
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    atomicController.UpdateProfile(new("avatar-2", "Database failure")));
            }

            await using var atomicVerify = CreateContext(connection);
            Assert.Equal("avatar-5", (await atomicVerify.Users.FindAsync(ownerId))!.AvatarId);
            Assert.Equal("Sunny Acres", (await atomicVerify.Farms.FindAsync(ownerFarmId))!.Name);
        }
        finally
        {
            // Only this test-created database is removed; the application database is never used.
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE);", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static AppDbContext CreateContext(string connection) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    private static ControllerContext Identity(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
        }
    };
}
