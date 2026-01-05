namespace FarmAndFriends.Api.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public ICollection<Farm> Farms { get; set; } = new List<Farm>();
    public int Level { get; set; } = 1;
    public int CurrentXp { get; set; } = 0;
}
