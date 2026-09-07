using FarmAndFriends.Api.Domain.Enums;

namespace FarmAndFriends.Api.Contracts.Friends;

public record CreateFriendRequest(Guid RecipientUserId);

public record UserSearchResponse(
    Guid Id,
    string Username
);

public record FriendResponse(
    Guid UserId,
    string Username,
    Guid FarmId,
    string FarmName,
    string AvatarId
);

public record FriendRequestResponse(
    Guid Id,
    Guid RequesterUserId,
    string RequesterUsername,
    Guid RecipientUserId,
    string RecipientUsername,
    FriendshipStatus Status,
    DateTime CreatedAt,
    DateTime? RespondedAt
);
