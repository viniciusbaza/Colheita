namespace FarmAndFriends.Api.Dtos.Users;

public sealed record UpdateAvatarRequest(string? AvatarId);

public sealed record AvatarResponse(string AvatarId);
