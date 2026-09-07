namespace FarmAndFriends.Api.Contracts.Notifications;

public sealed record NotificationFeedResponse(
    IReadOnlyList<NotificationResponse> Items,
    string? NextCursor,
    DateTime WindowStartUtc,
    int UnreadCount);
