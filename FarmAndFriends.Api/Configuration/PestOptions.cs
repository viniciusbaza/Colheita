namespace FarmAndFriends.Api.Configuration;

public sealed class PestOptions
{
    public const string SectionName = "Pests";
    public const string NaturalRepellentItemId = "natural_repellent";

    public bool Enabled { get; set; } = false;
    public int SafetyPeriodMinutes { get; set; } = 1;
    public int ReactionWindowMinutes { get; set; } = 5;
    public int DamageAmount { get; set; } = 1;
    public int MaxActivePestsPerFarm { get; set; } = 9;
    public int MinimumInfestationIntervalMinutes { get; set; } = 1;
    public int ProtectionDurationHours { get; set; } = 4;
    public string NaturalRepellentName { get; set; } = "Repelente Natural";
    public string NaturalRepellentIcon { get; set; } = "🛡️";
    public string NaturalRepellentDescription { get; set; } =
        "Protege um lote contra pragas por algumas horas. Não impede roubos de outros jogadores.";
    public int NaturalRepellentBuyPrice { get; set; } = 30;
    public int NaturalRepellentMinLevel { get; set; } = 1;
    public int RemovalCoinsReward { get; set; } = 2;
    public int RemovalXpReward { get; set; } = 5;
    public int MaxRewardedRemovalsPerWindow { get; set; } = 15;
    public int RemovalRewardRollingWindowHours { get; set; } = 24;
}
