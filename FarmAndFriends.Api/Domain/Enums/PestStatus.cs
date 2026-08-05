namespace FarmAndFriends.Api.Domain.Enums;

public enum PestStatus
{
    None = 0,
    Scheduled = 1,
    Active = 2,
    Removed = 3,
    Consumed = 4,
    CancelledByTheft = 5,
    CancelledByHarvest = 6,
    CancelledByProtection = 7
}
