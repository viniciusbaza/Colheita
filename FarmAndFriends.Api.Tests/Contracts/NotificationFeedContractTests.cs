using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FarmAndFriends.Api.Contracts.Notifications;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FarmAndFriends.Api.Tests.Contracts;

public sealed class NotificationFeedContractTests
{
    [Fact]
    public async Task Feed_RejectsMissingAuthenticatedUserClaim()
    {
        await using var context = CreateContext();
        var controller = new NotificationsController(
            context,
            TimeProvider.System)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        Assert.IsType<UnauthorizedResult>(await controller.GetFeed());
        Assert.NotNull(typeof(NotificationsController)
            .GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Feed_RejectsMalformedOpaqueCursor()
    {
        await using var context = CreateContext();
        var controller = CreateAuthenticatedController(context);

        var result = await controller.GetFeed("not-a-feed-cursor");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Cursor inv\u00e1lido.", badRequest.Value);
    }

    [Fact]
    public void FeedResponse_UsesExpectedCamelCaseShape()
    {
        var response = new NotificationFeedResponse(
            [
                new NotificationResponse(
                    Guid.NewGuid(),
                    NotificationType.CropCaredFor,
                    Guid.NewGuid(),
                    "Amigo",
                    "Ajudou na sua planta\u00e7\u00e3o.",
                    null,
                    null,
                    Guid.NewGuid(),
                    null,
                    DateTime.UtcNow,
                    null)
            ],
            "opaque-cursor",
            DateTime.UtcNow.AddDays(-7),
            1);

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("items", out var items));
        Assert.True(root.TryGetProperty("nextCursor", out _));
        Assert.True(root.TryGetProperty("windowStartUtc", out _));
        Assert.True(root.TryGetProperty("unreadCount", out _));
        Assert.True(items[0].TryGetProperty("actorUsername", out _));
        Assert.False(root.TryGetProperty("Items", out _));
    }

    private static NotificationsController CreateAuthenticatedController(
        AppDbContext context)
    {
        return new NotificationsController(context, TimeProvider.System)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            [
                                new Claim(
                                    ClaimTypes.NameIdentifier,
                                    Guid.NewGuid().ToString())
                            ],
                            authenticationType: "contract-test"))
                }
            }
        };
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);
}
