namespace FarmAndFriends.Api.Domain.Entities;

public class VisitorFarmCareCycle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VisitorUserId { get; set; }
    public User VisitorUser { get; set; } = null!;

    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;

    public Guid FarmId { get; set; }
    public Farm Farm { get; set; } = null!;

    public DateTime StartedAt { get; set; }
    public DateTime EndsAt { get; set; }

    public bool RewardGranted { get; set; }
    // Snapshotted per-plot values keep every completion in this cycle
    // consistent even if configuration changes while the cycle is active.
    public int CoinsReward { get; set; }
    public int XpReward { get; set; }

    public ICollection<CropCareCompletion> Completions { get; set; } =
        new List<CropCareCompletion>();
}
