using FarmAndFriends.Api.Domain.Enums;

namespace FarmAndFriends.Api.Contracts.Notifications;

public record NotificationResponse(
    Guid Id,
    NotificationType Type,
    Guid? ActorUserId,
    string? ActorUsername,
    string? Message,
    Guid? FriendshipId,
    Guid? TheftLogId,
    Guid? CareOpportunityId,
    DateTime CreatedAt,
    DateTime? ReadAt
);
