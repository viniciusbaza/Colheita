namespace FarmAndFriends.Api.Domain.Entities;

public class Plot
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }
    public Farm Farm { get; set; } = null!;

    public int X { get; set; }
    public int Y { get; set; }

    public bool Unlocked { get; set; }

    public string? SeedId { get; set; }

    public DateTime? PlantedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public bool IsEmpty => SeedId == null;
    public bool IsReady => ReadyAt <= DateTime.UtcNow;

    public int? RemainingYield { get; set; }

    public Guid? CareOpportunityId { get; set; }
}
