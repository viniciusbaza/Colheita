namespace FarmAndFriends.Api.Contracts.Shop;

public sealed record ShopItemResponse(
    string Id,
    string Name,
    string Icon,
    string Description,
    int BuyPrice,
    int MinLevel,
    int ProtectionDurationHours);

public sealed record BuyItemRequest(string ItemId, int Quantity);

public sealed record BuyItemResponse(
    string ItemId,
    int Quantity,
    int InventoryQuantity,
    int CoinsLeft);
