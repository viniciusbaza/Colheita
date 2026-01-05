namespace FarmAndFriends.Api.Domain.Entities;

public class Inventory
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int Coins { get; set; }
    public int PremiumCoins { get; set; }

    public ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
}
