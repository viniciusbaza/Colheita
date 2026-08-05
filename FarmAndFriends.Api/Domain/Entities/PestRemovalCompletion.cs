namespace FarmAndFriends.Api.Domain.Entities;

public sealed class PestRemovalCompletion
{
    public Guid Id { get; set; }
    public Guid PestOccurrenceId { get; set; }
    public Guid IdempotencyKey { get; set; }

    public Guid ActorUserId { get; set; }
    public User ActorUser { get; set; } = null!;

    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;

    public Guid FarmId { get; set; }
    public Guid PlotId { get; set; }
    public DateTime RemovedAt { get; set; }
    public int? RemainingYieldAfter { get; set; }
    public int CoinsGained { get; set; }
    public int XpGained { get; set; }
    public int CoinsAfter { get; set; }
}
