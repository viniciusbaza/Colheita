namespace FarmAndFriends.Api.Contracts.Seeds;

public sealed record SeedCatalogResponse(
    string Id,
    string Name,
    string CropName,
    string Icon,
    int BuyPrice,
    int SellPrice,
    TimeSpan GrowTime,
    TimeSpan? RegrowTime,
    int TheftChancePercent,
    int MinLevel,
    string CropId,
    int CropAmount,
    int HarvestCycles);
