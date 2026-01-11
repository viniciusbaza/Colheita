using FarmAndFriends.Api.Domain.Entities;

namespace FarmAndFriends.Api.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }

    public bool Revoked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
