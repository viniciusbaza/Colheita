namespace FarmAndFriends.Api.Dtos.Users;

public class MeResponseDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;

    public int Level { get; set; }
    public int CurrentXp { get; set; }
    public int XpToNextLevel { get; set; }
}
