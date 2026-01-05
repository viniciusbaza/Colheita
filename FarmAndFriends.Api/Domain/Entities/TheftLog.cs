using FarmAndFriends.Api.Domain.Entities;

public class TheftLog
{
    public Guid Id { get; set; }

    public Guid FarmId { get; set; }
    public Farm Farm { get; set; } = null!;
    
    public Guid PlotId { get; set; }
    public Plot Plot { get; set; } = null!;

    public Guid ThiefUserId { get; set; }
    public User ThiefUser { get; set; } = null!;

    public string SeedId { get; set; } = null!;
    public Seed Seed { get; set; } = null!;
    
    public int Quantity { get; set; }

    public bool GotBonus { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
