namespace FarmAndFriends.Api.Configuration;

public sealed class CropCareOptions
{
    public const string SectionName = "CropCare";

    // Eligibility is per visitor + plot crop cycle. The opportunity itself
    // remains active for the whole growing stage.
    public int VisitorPlotCareCooldownHours { get; init; } = 5;
    // Reward cycles are scoped by visitor + farm and pay every unique
    // eligible plot cared for during the cycle.
    public int VisitorFarmRewardCycleHours { get; init; } = 5;
    public int VisitorFarmRewardRollingWindowHours { get; init; } = 24;
    public int MaxRewardedCyclesPerVisitorFarmWindow { get; init; } = 5;
    public int OwnerNotificationDeduplicationWindowHours { get; init; } = 24;
    public int CoinsReward { get; init; } = 2;
    public int XpReward { get; init; } = 5;
}
