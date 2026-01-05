namespace FarmAndFriends.Api.Dtos.Inventory;

public record InventoryResponseDto(
    int Coins,
    int PremiumCoins,
    List<InventoryItemDto> Items
);
