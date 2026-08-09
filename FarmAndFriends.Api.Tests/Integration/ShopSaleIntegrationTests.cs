using System.Security.Claims;
using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Shop;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class ShopSaleIntegrationTests
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task FractionalSales_UpdateCoinsAndStockWithoutChangingXpOrLevel()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var userId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();

        await using (var arrangeContext = new AppDbContext(dbOptions))
        {
            var uniqueName = $"shop_sale_{userId:N}";
            arrangeContext.Users.Add(new User
            {
                Id = userId,
                Username = uniqueName,
                NormalizedUsername = uniqueName.ToUpperInvariant(),
                PasswordHash = "integration-test",
                Level = 4,
                CurrentXp = 37
            });
            arrangeContext.Inventories.Add(new Inventory
            {
                Id = inventoryId,
                UserId = userId,
                Coins = 100,
                PremiumCoins = 0
            });
            arrangeContext.InventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                ItemType = ItemType.Crop,
                ItemId = "carrot_crop",
                Quantity = 5
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using (var firstSaleContext = new AppDbContext(dbOptions))
        {
            var result = await CreateController(firstSaleContext, userId)
                .SellCrop(new SellCropRequest("carrot_crop", 2));

            Assert.IsType<OkObjectResult>(result);
        }

        await AssertSaleState(
            dbOptions,
            userId,
            inventoryId,
            expectedCoins: 140,
            expectedCropQuantity: 3);

        await using (var secondSaleContext = new AppDbContext(dbOptions))
        {
            var result = await CreateController(secondSaleContext, userId)
                .SellCrop(new SellCropRequest("carrot_crop", 1));

            Assert.IsType<OkObjectResult>(result);
        }

        await AssertSaleState(
            dbOptions,
            userId,
            inventoryId,
            expectedCoins: 160,
            expectedCropQuantity: 2);
    }

    private static async Task AssertSaleState(
        DbContextOptions<AppDbContext> dbOptions,
        Guid userId,
        Guid inventoryId,
        int expectedCoins,
        int expectedCropQuantity)
    {
        await using var assertContext = new AppDbContext(dbOptions);
        var user = await assertContext.Users
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == userId);
        var inventory = await assertContext.Inventories
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == inventoryId);
        var cropItem = await assertContext.InventoryItems
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.InventoryId == inventoryId
                && candidate.ItemType == ItemType.Crop
                && candidate.ItemId == "carrot_crop");

        Assert.Equal(4, user.Level);
        Assert.Equal(37, user.CurrentXp);
        Assert.Equal(expectedCoins, inventory.Coins);
        Assert.Equal(expectedCropQuantity, cropItem.Quantity);
    }

    private static ShopController CreateController(
        AppDbContext context,
        Guid userId)
    {
        return new ShopController(
            context,
            Options.Create(new PestOptions()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[]
                            {
                                new Claim(
                                    ClaimTypes.NameIdentifier,
                                    userId.ToString())
                            },
                            authenticationType: "integration-test"))
                }
            }
        };
    }
}
