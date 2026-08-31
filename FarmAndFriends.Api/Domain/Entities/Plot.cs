using FarmAndFriends.Api.Domain.Enums;

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
    public int? CurrentHarvestCycle { get; set; }
    public DateTime? CurrentHarvestCycleStartedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public bool IsEmpty => SeedId == null;
    public bool IsReady => ReadyAt <= DateTime.UtcNow;

    public int? RemainingYield { get; set; }

    public Guid? CareOpportunityId { get; set; }

    public Guid? PestOccurrenceId { get; set; }
    public PestType? PestType { get; set; }
    public PestStatus PestStatus { get; set; } = PestStatus.None;
    public DateTime? PestScheduledAt { get; set; }
    public DateTime? PestAppearsAt { get; set; }
    public DateTime? PestAppearedAt { get; set; }
    public DateTime? PestConsumesAt { get; set; }
    public DateTime? PestResolvedAt { get; set; }
    public int PestConsumedAmount { get; set; }
    public DateTime? ProtectedUntil { get; set; }
}
