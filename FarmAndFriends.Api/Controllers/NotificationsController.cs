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
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context)
    {
        _context = context;
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

        return Ok(notifications.Select(n => new NotificationResponse(
            n.Id,
            n.Type,
            n.ActorUserId,
            n.ActorUser?.Username,
            n.Message,
            n.FriendshipId,
            n.TheftLogId,
            n.CareOpportunityId,
            n.PestPlotId,
            n.CreatedAt,
            n.ReadAt)));
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
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var readAt = DateTime.UtcNow;

        await _context.Notifications
            .Where(n => n.RecipientUserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(n => n.ReadAt, readAt));

        return NoContent();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
