using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Notifications;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class NotificationFeedIntegrationTests
{
    private static readonly DateTime Now = new(
        2026,
        9,
        1,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Feed_IsolatedByRecipientAndLimitedToSevenDayWindow()
    {
        var options = CreateOptions();
        var recipientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var otherRecipientId = Guid.NewGuid();
        var actorUsername = $"feed_actor_{actorId:N}";
        var boundary = Now.AddDays(-7);
        var recentUnreadId = Guid.NewGuid();
        var boundaryUnreadId = Guid.NewGuid();
        var recentReadId = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        var futureId = Guid.NewGuid();
        var otherRecipientNotificationId = Guid.NewGuid();

        await using (var arrange = new AppDbContext(options))
        {
            arrange.Users.AddRange(
                CreateUser(recipientId, "feed_recipient"),
                CreateUser(actorId, "feed_actor"),
                CreateUser(otherRecipientId, "feed_other"));
            arrange.Notifications.AddRange(
                CreateNotification(
                    recentUnreadId,
                    recipientId,
                    actorId,
                    Now.AddHours(-1)),
                CreateNotification(
                    boundaryUnreadId,
                    recipientId,
                    actorId,
                    boundary),
                CreateNotification(
                    recentReadId,
                    recipientId,
                    actorId,
                    Now.AddHours(-2),
                    Now.AddHours(-1)),
                CreateNotification(
                    oldId,
                    recipientId,
                    actorId,
                    boundary.AddSeconds(-1)),
                CreateNotification(
                    futureId,
                    recipientId,
                    actorId,
                    Now.AddSeconds(1)),
                CreateNotification(
                    otherRecipientNotificationId,
                    otherRecipientId,
                    actorId,
                    Now.AddMinutes(-30)));
            await arrange.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var response = await GetFeed(
            CreateController(context, recipientId),
            take: 100);

        Assert.Equal(boundary, response.WindowStartUtc);
        Assert.Equal(2, response.UnreadCount);
        Assert.Null(response.NextCursor);
        Assert.Equal(
            [recentUnreadId, recentReadId, boundaryUnreadId],
            response.Items.Select(item => item.Id).ToArray());
        Assert.All(
            response.Items,
            item => Assert.Equal(actorUsername, item.ActorUsername));
        Assert.DoesNotContain(response.Items, item => item.Id == oldId);
        Assert.DoesNotContain(response.Items, item => item.Id == futureId);
        Assert.DoesNotContain(
            response.Items,
            item => item.Id == otherRecipientNotificationId);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Feed_CursorPaginatesEqualTimestampsWithoutDuplicates()
    {
        var options = CreateOptions();
        var recipientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var createdAt = Now.AddHours(-1);
        var notificationIds = Enumerable.Range(0, 5)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        await using (var arrange = new AppDbContext(options))
        {
            arrange.Users.AddRange(
                CreateUser(recipientId, "cursor_recipient"),
                CreateUser(actorId, "cursor_actor"));
            arrange.Notifications.AddRange(notificationIds.Select(id =>
                CreateNotification(
                    id,
                    recipientId,
                    actorId,
                    createdAt)));
            await arrange.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var controller = CreateController(context, recipientId);
        var firstPage = await GetFeed(controller, take: 2);
        var repeatedFirstPage = await GetFeed(controller, take: 2);
        var secondPage = await GetFeed(
            controller,
            firstPage.NextCursor,
            take: 2);
        var thirdPage = await GetFeed(
            controller,
            secondPage.NextCursor,
            take: 2);

        Assert.NotNull(firstPage.NextCursor);
        Assert.NotNull(secondPage.NextCursor);
        Assert.Null(thirdPage.NextCursor);
        Assert.Equal(
            firstPage.Items.Select(item => item.Id),
            repeatedFirstPage.Items.Select(item => item.Id));

        var allPages = firstPage.Items
            .Concat(secondPage.Items)
            .Concat(thirdPage.Items)
            .Select(item => item.Id)
            .ToArray();
        Assert.Equal(5, allPages.Length);
        Assert.Equal(5, allPages.Distinct().Count());
        Assert.Equal(
            notificationIds.OrderBy(id => id),
            allPages.OrderBy(id => id));
        Assert.Equal(5, firstPage.UnreadCount);
        Assert.Equal(5, secondPage.UnreadCount);
        Assert.Equal(5, thirdPage.UnreadCount);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task FeedReadAll_OnlyMarksUnreadItemsInsideCurrentWindow()
    {
        var options = CreateOptions();
        var recipientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var otherRecipientId = Guid.NewGuid();
        var existingReadAt = Now.AddHours(-3);
        var recentUnreadId = Guid.NewGuid();
        var boundaryUnreadId = Guid.NewGuid();
        var alreadyReadId = Guid.NewGuid();
        var oldUnreadId = Guid.NewGuid();
        var futureUnreadId = Guid.NewGuid();
        var otherRecipientUnreadId = Guid.NewGuid();

        await using (var arrange = new AppDbContext(options))
        {
            arrange.Users.AddRange(
                CreateUser(recipientId, "read_all_recipient"),
                CreateUser(actorId, "read_all_actor"),
                CreateUser(otherRecipientId, "read_all_other"));
            arrange.Notifications.AddRange(
                CreateNotification(
                    recentUnreadId,
                    recipientId,
                    actorId,
                    Now.AddHours(-1)),
                CreateNotification(
                    boundaryUnreadId,
                    recipientId,
                    actorId,
                    Now.AddDays(-7)),
                CreateNotification(
                    alreadyReadId,
                    recipientId,
                    actorId,
                    Now.AddHours(-2),
                    existingReadAt),
                CreateNotification(
                    oldUnreadId,
                    recipientId,
                    actorId,
                    Now.AddDays(-7).AddSeconds(-1)),
                CreateNotification(
                    futureUnreadId,
                    recipientId,
                    actorId,
                    Now.AddSeconds(1)),
                CreateNotification(
                    otherRecipientUnreadId,
                    otherRecipientId,
                    actorId,
                    Now.AddHours(-1)));
            await arrange.SaveChangesAsync();
        }

        await using (var actionContext = new AppDbContext(options))
        {
            var action = await CreateController(actionContext, recipientId)
                .MarkFeedAsRead();
            Assert.IsType<NoContentResult>(action);
        }

        await using (var assertion = new AppDbContext(options))
        {
            var readStates = await assertion.Notifications
                .AsNoTracking()
                .Where(notification =>
                    notification.RecipientUserId == recipientId
                    || notification.Id == otherRecipientUnreadId)
                .ToDictionaryAsync(
                    notification => notification.Id,
                    notification => notification.ReadAt);

            Assert.Equal(Now, readStates[recentUnreadId]);
            Assert.Equal(Now, readStates[boundaryUnreadId]);
            Assert.Equal(existingReadAt, readStates[alreadyReadId]);
            Assert.Null(readStates[oldUnreadId]);
            Assert.Null(readStates[futureUnreadId]);
            Assert.Null(readStates[otherRecipientUnreadId]);
        }

        await using var feedContext = new AppDbContext(options);
        var feed = await GetFeed(
            CreateController(feedContext, recipientId),
            take: 100);
        Assert.Equal(0, feed.UnreadCount);
        Assert.Equal(3, feed.Items.Count);
        Assert.All(feed.Items, item => Assert.NotNull(item.ReadAt));
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task IndividualRead_PreservesItemInFeedAndCannotReadAnotherRecipientItem()
    {
        var options = CreateOptions();
        var recipientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var otherRecipientId = Guid.NewGuid();
        var ownedNotificationId = Guid.NewGuid();
        var otherNotificationId = Guid.NewGuid();

        await using (var arrange = new AppDbContext(options))
        {
            arrange.Users.AddRange(
                CreateUser(recipientId, "individual_recipient"),
                CreateUser(actorId, "individual_actor"),
                CreateUser(otherRecipientId, "individual_other"));
            arrange.Notifications.AddRange(
                CreateNotification(
                    ownedNotificationId,
                    recipientId,
                    actorId,
                    Now.AddHours(-1)),
                CreateNotification(
                    otherNotificationId,
                    otherRecipientId,
                    actorId,
                    Now.AddHours(-1)));
            await arrange.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var controller = CreateController(context, recipientId);
        Assert.IsType<NoContentResult>(
            await controller.MarkAsRead(ownedNotificationId));
        Assert.IsType<NotFoundObjectResult>(
            await controller.MarkAsRead(otherNotificationId));

        var feed = await GetFeed(controller, take: 100);
        var item = Assert.Single(feed.Items);
        Assert.Equal(ownedNotificationId, item.Id);
        Assert.Equal(Now, item.ReadAt);
        Assert.Equal(0, feed.UnreadCount);

        await using var assertion = new AppDbContext(options);
        Assert.Null((await assertion.Notifications
            .AsNoTracking()
            .SingleAsync(notification => notification.Id == otherNotificationId))
            .ReadAt);
    }

    private static async Task<NotificationFeedResponse> GetFeed(
        NotificationsController controller,
        string? cursor = null,
        int take = 50)
    {
        var result = await controller.GetFeed(cursor, take);
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<NotificationFeedResponse>(ok.Value);
    }

    private static NotificationsController CreateController(
        AppDbContext context,
        Guid userId)
    {
        return new NotificationsController(
            context,
            new FixedTimeProvider(Now))
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
                                    userId.ToString())
                            ],
                            authenticationType: "integration-test"))
                }
            }
        };
    }

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable(
                "CROP_CARE_TEST_CONNECTION")!)
            .Options;

    private static User CreateUser(Guid id, string prefix)
    {
        var username = $"{prefix}_{id:N}";
        return new User
        {
            Id = id,
            Username = username,
            NormalizedUsername = username.ToUpperInvariant(),
            PasswordHash = "integration-test"
        };
    }

    private static Notification CreateNotification(
        Guid id,
        Guid recipientId,
        Guid actorId,
        DateTime createdAt,
        DateTime? readAt = null) =>
        new()
        {
            Id = id,
            RecipientUserId = recipientId,
            ActorUserId = actorId,
            Type = NotificationType.CropCaredFor,
            Message = "Acontecimento de teste.",
            CreatedAt = createdAt,
            ReadAt = readAt
        };

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(now, TimeSpan.Zero);
    }
}
