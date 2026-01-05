namespace FarmAndFriends.Api.Dtos.Inventory;

public record InventoryItemDto(
    string ItemType,
    string ItemId,
    int Quantity
);
