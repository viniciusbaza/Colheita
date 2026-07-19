using FarmAndFriends.Api.Domain.Enums;

namespace FarmAndFriends.Api.Domain.Entities;

public class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // The pair is stored in a canonical order so one row represents both directions.
    public Guid UserAId { get; set; }
    public User UserA { get; set; } = null!;

    public Guid UserBId { get; set; }
    public User UserB { get; set; } = null!;

    public Guid RequestedByUserId { get; set; }
    public User RequestedByUser { get; set; } = null!;

    public FriendshipStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}
