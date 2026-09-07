namespace FarmAndFriends.Api.Domain.Rules;

public static class FarmNameRules
{
    public const int MaxLength = 30;

    public static string? Normalize(string? farmName)
    {
        var normalized = farmName?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
