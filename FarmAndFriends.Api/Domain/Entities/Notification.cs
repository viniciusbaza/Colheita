using FarmAndFriends.Api.Domain.Enums;

namespace FarmAndFriends.Api.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecipientUserId { get; set; }
    public User RecipientUser { get; set; } = null!;

    public Guid? ActorUserId { get; set; }
    public User? ActorUser { get; set; }

    public NotificationType Type { get; set; }
    public string? Message { get; set; }

    public Guid? FriendshipId { get; set; }
    public Friendship? Friendship { get; set; }

    public Guid? TheftLogId { get; set; }
    public TheftLog? TheftLog { get; set; }

    public Guid? CareOpportunityId { get; set; }
    public Guid? PestPlotId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
