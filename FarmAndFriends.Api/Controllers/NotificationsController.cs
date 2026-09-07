using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Notifications;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private const int FeedWindowDays = 7;
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public NotificationsController(
        AppDbContext context,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int take = 50)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var limit = Math.Clamp(take, 1, 100);
        var query = _context.Notifications
            .AsNoTracking()
            .Include(n => n.ActorUser)
            .Where(n => n.RecipientUserId == userId);

        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return Ok(notifications.Select(ToResponse));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var count = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == userId && n.ReadAt == null);

        return Ok(new { count });
    }

    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(
        [FromQuery] string? cursor = null,
        [FromQuery] int take = 50)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windowStartUtc = now.AddDays(-FeedWindowDays);
        var limit = Math.Clamp(take, 1, 100);

        NotificationFeedCursor? decodedCursor = null;
        if (cursor != null)
        {
            if (!NotificationFeedCursor.TryDecode(cursor, out decodedCursor)
                || decodedCursor is null)
            {
                return BadRequest("Cursor inv\u00e1lido.");
            }
        }

        var query = _context.Notifications
            .AsNoTracking()
            .Include(n => n.ActorUser)
            .Where(n =>
                n.RecipientUserId == userId
                && n.CreatedAt >= windowStartUtc
                && n.CreatedAt <= now);

        if (decodedCursor != null)
        {
            var cursorCreatedAt = decodedCursor.CreatedAt;
            var cursorId = decodedCursor.Id;
            query = query.Where(n => EF.Functions.LessThan(
                ValueTuple.Create(n.CreatedAt, n.Id),
                ValueTuple.Create(cursorCreatedAt, cursorId)));
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(limit + 1)
            .ToListAsync();
        var hasNextPage = notifications.Count > limit;
        if (hasNextPage)
            notifications.RemoveAt(limit);

        var unreadCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n =>
                n.RecipientUserId == userId
                && n.CreatedAt >= windowStartUtc
                && n.CreatedAt <= now
                && n.ReadAt == null);

        var items = notifications
            .Select(ToResponse)
            .ToArray();
        var nextCursor = hasNextPage
            ? NotificationFeedCursor.Encode(
                notifications[^1].CreatedAt,
                notifications[^1].Id)
            : null;

        return Ok(new NotificationFeedResponse(
            items,
            nextCursor,
            windowStartUtc,
            unreadCount));
    }

    [HttpPatch("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n =>
                n.Id == notificationId &&
                n.RecipientUserId == userId);

        if (notification == null)
            return NotFound("Notifica\u00e7\u00e3o n\u00e3o encontrada.");

        if (notification.ReadAt == null)
        {
            notification.ReadAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var readAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.Notifications
            .Where(n => n.RecipientUserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(n => n.ReadAt, readAt));

        return NoContent();
    }

    [HttpPatch("feed/read-all")]
    public async Task<IActionResult> MarkFeedAsRead()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var readAt = _timeProvider.GetUtcNow().UtcDateTime;
        var windowStartUtc = readAt.AddDays(-FeedWindowDays);

        await _context.Notifications
            .Where(n =>
                n.RecipientUserId == userId
                && n.CreatedAt >= windowStartUtc
                && n.CreatedAt <= readAt
                && n.ReadAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(n => n.ReadAt, readAt));

        return NoContent();
    }

    private static NotificationResponse ToResponse(
        Domain.Entities.Notification notification) =>
        new(
            notification.Id,
            notification.Type,
            notification.ActorUserId,
            notification.ActorUser?.Username,
            notification.Message,
            notification.FriendshipId,
            notification.TheftLogId,
            notification.CareOpportunityId,
            notification.PestPlotId,
            notification.CreatedAt,
            notification.ReadAt);

    private bool TryGetCurrentUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private sealed record NotificationFeedCursor(DateTime CreatedAt, Guid Id)
    {
        private const int PayloadLength = sizeof(long) + 16;

        public static string Encode(DateTime createdAt, Guid id)
        {
            Span<byte> payload = stackalloc byte[PayloadLength];
            BitConverter.TryWriteBytes(payload, createdAt.Ticks);
            id.TryWriteBytes(payload[sizeof(long)..]);

            return Convert.ToBase64String(payload)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public static bool TryDecode(
            string encoded,
            out NotificationFeedCursor? cursor)
        {
            cursor = null;

            if (encoded.Length != 32)
                return false;

            try
            {
                var base64 = encoded.Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(
                    base64.Length + ((4 - base64.Length % 4) % 4),
                    '=');
                var payload = Convert.FromBase64String(base64);
                if (payload.Length != PayloadLength)
                    return false;

                var ticks = BitConverter.ToInt64(payload, 0);
                var createdAt = new DateTime(ticks, DateTimeKind.Utc);
                var id = new Guid(payload.AsSpan(sizeof(long), 16));
                cursor = new NotificationFeedCursor(createdAt, id);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }
    }
}
