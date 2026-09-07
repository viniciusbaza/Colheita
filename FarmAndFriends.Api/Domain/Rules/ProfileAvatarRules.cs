namespace FarmAndFriends.Api.Domain.Rules;

public static class ProfileAvatarRules
{
    public const string DefaultAvatarId = "avatar-1";

    public static bool IsAllowed(string? avatarId) => avatarId is
        "avatar-1" or "avatar-2" or "avatar-3" or "avatar-4" or "avatar-5";
}
