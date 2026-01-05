namespace FarmAndFriends.Api.Domain.Entities;

public class Farm
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<Plot> Plots { get; set; } = new List<Plot>();
}
