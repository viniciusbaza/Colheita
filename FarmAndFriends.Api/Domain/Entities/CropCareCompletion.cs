namespace FarmAndFriends.Api.Domain.Entities;

public class CropCareCompletion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VisitorFarmCareCycleId { get; set; }
    public VisitorFarmCareCycle VisitorFarmCareCycle { get; set; } = null!;

    public Guid VisitorUserId { get; set; }
    public User VisitorUser { get; set; } = null!;

    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;

    public Guid FarmId { get; set; }
    public Guid PlotId { get; set; }
    public Guid CareOpportunityId { get; set; }
    public Guid IdempotencyKey { get; set; }

    public DateTime CaredAt { get; set; }
    public string CaredByUsername { get; set; } = null!;
    public int CoinsGained { get; set; }
    public int XpGained { get; set; }
    public int CoinsAfter { get; set; }
}
