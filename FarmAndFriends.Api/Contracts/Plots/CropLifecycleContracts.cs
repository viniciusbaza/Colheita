namespace FarmAndFriends.Api.Contracts.Plots;

public sealed record PlantResponse(
    Guid Id,
    string Seed,
    DateTime PlantedAt,
    DateTime ReadyAt,
    int CurrentHarvestCycle,
    int XpGained);

public sealed record HarvestCropRequest(int? ExpectedHarvestCycle);

public sealed record HarvestResponse(
    Guid Id,
    string Crop,
    int Amount,
    int InventoryTotal,
    int XpGained,
    int? CurrentHarvestCycle,
    DateTime? ReadyAt);

public static class PlotErrorCodes
{
    public const string HarvestCycleMismatch = "HARVEST_CYCLE_MISMATCH";
    public const string CropCycleStateInvalid = "CROP_CYCLE_STATE_INVALID";
    public const string InventoryCapacityExceeded =
        "INVENTORY_CAPACITY_EXCEEDED";
}
