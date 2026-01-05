namespace FarmAndFriends.Api.Dtos.Shop;

public record BuySeedResponse(
    string SeedId,
    int Quantity,
    int CoinsLeft
);
