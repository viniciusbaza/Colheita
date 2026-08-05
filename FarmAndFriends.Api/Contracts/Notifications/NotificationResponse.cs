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
    Guid? PestPlotId,
    DateTime CreatedAt,
    DateTime? ReadAt
);
