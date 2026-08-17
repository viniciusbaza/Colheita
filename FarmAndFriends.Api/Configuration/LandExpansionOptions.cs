namespace FarmAndFriends.Api.Configuration;

public sealed class LandExpansionOptions
{
    public const string SectionName = "LandExpansion";

    public bool Enabled { get; set; } = true;
}
