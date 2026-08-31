namespace FarmAndFriends.Api.Contracts.Pests;

public static class PestErrorCodes
{
    public const string FeatureDisabled = "PEST_FEATURE_DISABLED";
    public const string FarmNotFound = "PEST_FARM_NOT_FOUND";
    public const string PlotNotFound = "PEST_PLOT_NOT_FOUND";
    public const string Forbidden = "PEST_FORBIDDEN";
    public const string NotActive = "PEST_NOT_ACTIVE";
    public const string AlreadyProtected = "PEST_ALREADY_PROTECTED";
    public const string InventoryNotFound = "PEST_INVENTORY_NOT_FOUND";
    public const string ItemUnavailable = "PEST_ITEM_UNAVAILABLE";
    public const string IdempotencyKeyRequired =
        "PEST_IDEMPOTENCY_KEY_REQUIRED";
    public const string IdempotencyKeyReused =
        "PEST_IDEMPOTENCY_KEY_REUSED";
    public const string OccurrenceIdRequired =
        "PEST_OCCURRENCE_ID_REQUIRED";
    public const string OccurrenceMismatch =
        "PEST_OCCURRENCE_MISMATCH";
    public const string CropCycleStateInvalid =
        "PEST_CROP_CYCLE_STATE_INVALID";
}
