namespace FarmAndFriends.Api.Contracts.Farms;

public record TheftNotificationResponse(
    string Message,
    DateTime CreatedAt
);
