namespace FarmAndFriends.Api.Dtos.Shop;

public record BuySeedRequest(
    string SeedId,
    int Quantity
);
