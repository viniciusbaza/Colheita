namespace FarmAndFriends.Api.Contracts.Farms;

public record TheftLogResponse(
    string ThiefUsername,
    string CropName,
    int Quantity,
    bool GotBonus,
    DateTime CreatedAt
);
