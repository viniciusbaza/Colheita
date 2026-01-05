namespace FarmAndFriends.Api.Contracts.Shop;

public record SellCropRequest(
    string CropId,
    int Quantity
);
