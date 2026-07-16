using FarmAndFriends.Api.Domain.Enums;

namespace FarmAndFriends.Api.Domain.Entities;

public class InventoryItem
{
    public Guid Id { get; set; }

    public Guid InventoryId { get; set; }
    public Inventory Inventory { get; set; } = null!;

    public ItemType ItemType { get; set; }

    public string ItemId { get; set; } = null!; 

    public int Quantity { get; set; }
}
