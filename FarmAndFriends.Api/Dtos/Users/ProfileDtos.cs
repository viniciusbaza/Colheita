namespace FarmAndFriends.Api.Dtos.Users;

public sealed record UpdateProfileRequest(string? AvatarId, string? FarmName);

public sealed record ProfileResponse(
    string AvatarId,
    Guid FarmId,
    string FarmName);
