namespace FarmAndFriends.Api.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string NormalizedUsername { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public ICollection<Farm> Farms { get; set; } = new List<Farm>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public int Level { get; set; } = 1;
    public int CurrentXp { get; set; } = 0;
}
