namespace FarmAndFriends.Api.Dtos.Theft;

public class TheftResult
{
    public int StolenAmount { get; set; }
    public int OwnerAmount { get; set; }
    public int XpGained { get; set; }
    public bool GotBonus { get; set; }
}