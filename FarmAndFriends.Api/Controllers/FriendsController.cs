using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Friends;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly AppDbContext _context;

    public FriendsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetFriends()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var friendUserIds = _context.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted && f.UserAId == userId)
            .Select(f => f.UserBId)
            .Concat(
                _context.Friendships
                    .AsNoTracking()
                    .Where(f => f.Status == FriendshipStatus.Accepted && f.UserBId == userId)
                    .Select(f => f.UserAId));

        var friends = await (
            from user in _context.Users.AsNoTracking()
            join farm in _context.Farms.AsNoTracking() on user.Id equals farm.UserId
            where friendUserIds.Contains(user.Id)
            orderby user.Username
            select new FriendResponse(
                user.Id,
                user.Username,
                farm.Id,
                farm.Name)
        ).ToListAsync();

        return Ok(friends);
    }

    [HttpGet("requests/incoming")]
    public async Task<IActionResult> GetIncomingRequests()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var requests = await _context.Friendships
            .AsNoTracking()
            .Include(f => f.UserA)
            .Include(f => f.UserB)
            .Where(f =>
                f.Status == FriendshipStatus.Pending &&
                f.RequestedByUserId != userId &&
                (f.UserAId == userId || f.UserBId == userId))
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return Ok(requests.Select(ToResponse));
    }

    [HttpGet("requests/outgoing")]
    public async Task<IActionResult> GetOutgoingRequests()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var requests = await _context.Friendships
            .AsNoTracking()
            .Include(f => f.UserA)
            .Include(f => f.UserB)
            .Where(f =>
                f.Status == FriendshipStatus.Pending &&
                f.RequestedByUserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return Ok(requests.Select(ToResponse));
    }

    [HttpPost("requests")]
    public async Task<IActionResult> SendRequest([FromBody] CreateFriendRequest request)
    {
        if (!TryGetCurrentUserId(out var requesterUserId))
            return Unauthorized();

        if (request.RecipientUserId == Guid.Empty)
            return BadRequest("Destinat\u00e1rio inv\u00e1lido.");

        if (request.RecipientUserId == requesterUserId)
            return BadRequest("Voc\u00ea n\u00e3o pode adicionar a si mesmo.");

        var recipientExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.RecipientUserId);

        if (!recipientExists)
            return NotFound("Usu\u00e1rio n\u00e3o encontrado.");

        var (userAId, userBId) = FriendshipService.GetCanonicalPair(
            requesterUserId,
            request.RecipientUserId);

        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f => f.UserAId == userAId && f.UserBId == userBId);

        if (friendship != null)
        {
            if (friendship.Status == FriendshipStatus.Accepted)
                return Conflict("Voc\u00eas j\u00e1 s\u00e3o amigos.");

            if (friendship.Status == FriendshipStatus.Pending)
            {
                return friendship.RequestedByUserId == requesterUserId
                    ? Conflict("O convite de amizade j\u00e1 foi enviado.")
                    : Conflict("Voc\u00ea possui um convite pendente desse usu\u00e1rio.");
            }

            friendship.RequestedByUserId = requesterUserId;
            friendship.Status = FriendshipStatus.Pending;
            friendship.CreatedAt = DateTime.UtcNow;
            friendship.RespondedAt = null;
        }
        else
        {
            friendship = new Friendship
            {
                Id = Guid.NewGuid(),
                UserAId = userAId,
                UserBId = userBId,
                RequestedByUserId = requesterUserId,
                Status = FriendshipStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Friendships.Add(friendship);
        }

        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = request.RecipientUserId,
            ActorUserId = requesterUserId,
            FriendshipId = friendship.Id,
            Type = NotificationType.FriendRequestReceived,
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("J\u00e1 existe uma rela\u00e7\u00e3o de amizade entre estes usu\u00e1rios.");
        }

        var createdFriendship = await GetFriendshipWithUsersAsync(friendship.Id);
        return StatusCode(StatusCodes.Status201Created, ToResponse(createdFriendship!));
    }

    [HttpPost("requests/{friendshipId:guid}/accept")]
    public async Task<IActionResult> AcceptRequest(Guid friendshipId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var friendship = await GetFriendshipWithUsersAsync(friendshipId);
        if (friendship == null)
            return NotFound("Convite n\u00e3o encontrado.");

        if (friendship.Status != FriendshipStatus.Pending)
            return Conflict("Este convite n\u00e3o est\u00e1 mais pendente.");

        if (!IsRecipient(friendship, userId))
            return Forbid();

        var now = DateTime.UtcNow;
        friendship.Status = FriendshipStatus.Accepted;
        friendship.RespondedAt = now;

        await MarkIncomingRequestNotificationsAsReadAsync(friendship.Id, userId, now);

        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = friendship.RequestedByUserId,
            ActorUserId = userId,
            FriendshipId = friendship.Id,
            Type = NotificationType.FriendRequestAccepted,
            CreatedAt = now
        });

        await _context.SaveChangesAsync();
        return Ok(ToResponse(friendship));
    }

    [HttpPost("requests/{friendshipId:guid}/decline")]
    public async Task<IActionResult> DeclineRequest(Guid friendshipId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var friendship = await GetFriendshipWithUsersAsync(friendshipId);
        if (friendship == null)
            return NotFound("Convite n\u00e3o encontrado.");

        if (friendship.Status != FriendshipStatus.Pending)
            return Conflict("Este convite n\u00e3o est\u00e1 mais pendente.");

        if (!IsRecipient(friendship, userId))
            return Forbid();

        var now = DateTime.UtcNow;
        friendship.Status = FriendshipStatus.Declined;
        friendship.RespondedAt = now;

        await MarkIncomingRequestNotificationsAsReadAsync(friendship.Id, userId, now);

        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = friendship.RequestedByUserId,
            ActorUserId = userId,
            FriendshipId = friendship.Id,
            Type = NotificationType.FriendRequestDeclined,
            CreatedAt = now
        });

        await _context.SaveChangesAsync();
        return Ok(ToResponse(friendship));
    }

    [HttpDelete("requests/{friendshipId:guid}")]
    public async Task<IActionResult> CancelRequest(Guid friendshipId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f => f.Id == friendshipId);

        if (friendship == null)
            return NotFound("Convite n\u00e3o encontrado.");

        if (friendship.Status != FriendshipStatus.Pending)
            return Conflict("Somente convites pendentes podem ser cancelados.");

        if (friendship.RequestedByUserId != userId)
            return Forbid();

        var now = DateTime.UtcNow;
        friendship.Status = FriendshipStatus.Cancelled;
        friendship.RespondedAt = now;

        await MarkIncomingRequestNotificationsAsReadAsync(
            friendship.Id,
            GetRecipientUserId(friendship),
            now);

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{friendUserId:guid}")]
    public async Task<IActionResult> RemoveFriend(Guid friendUserId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        if (friendUserId == userId)
            return BadRequest("Voc\u00ea n\u00e3o pode remover a si mesmo.");

        var (userAId, userBId) = FriendshipService.GetCanonicalPair(userId, friendUserId);
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f =>
                f.UserAId == userAId &&
                f.UserBId == userBId &&
                f.Status == FriendshipStatus.Accepted);

        if (friendship == null)
            return NotFound("Amizade n\u00e3o encontrada.");

        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<Friendship?> GetFriendshipWithUsersAsync(Guid friendshipId)
    {
        return await _context.Friendships
            .Include(f => f.UserA)
            .Include(f => f.UserB)
            .FirstOrDefaultAsync(f => f.Id == friendshipId);
    }

    private async Task MarkIncomingRequestNotificationsAsReadAsync(
        Guid friendshipId,
        Guid recipientUserId,
        DateTime readAt)
    {
        var notifications = await _context.Notifications
            .Where(n =>
                n.FriendshipId == friendshipId &&
                n.RecipientUserId == recipientUserId &&
                n.Type == NotificationType.FriendRequestReceived &&
                n.ReadAt == null)
            .ToListAsync();

        foreach (var notification in notifications)
            notification.ReadAt = readAt;
    }

    private static FriendRequestResponse ToResponse(Friendship friendship)
    {
        var requester = friendship.RequestedByUserId == friendship.UserAId
            ? friendship.UserA
            : friendship.UserB;
        var recipient = friendship.RequestedByUserId == friendship.UserAId
            ? friendship.UserB
            : friendship.UserA;

        return new FriendRequestResponse(
            friendship.Id,
            requester.Id,
            requester.Username,
            recipient.Id,
            recipient.Username,
            friendship.Status,
            friendship.CreatedAt,
            friendship.RespondedAt);
    }

    private static bool IsRecipient(Friendship friendship, Guid userId)
    {
        return friendship.RequestedByUserId != userId &&
               (friendship.UserAId == userId || friendship.UserBId == userId);
    }

    private static Guid GetRecipientUserId(Friendship friendship)
    {
        return friendship.RequestedByUserId == friendship.UserAId
            ? friendship.UserBId
            : friendship.UserAId;
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
