using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Rules;

namespace FarmAndFriends.Api.Domain.Services;

public class ExperienceService
{
    public void AddXp(User lockedUser, int xp)
    {
        ArgumentNullException.ThrowIfNull(lockedUser);

        if (xp < 0)
            throw new ArgumentOutOfRangeException(nameof(xp));

        lockedUser.CurrentXp = checked(lockedUser.CurrentXp + xp);

        while (lockedUser.CurrentXp >=
               LevelProgression.XpToNextLevel(lockedUser.Level))
        {
            lockedUser.CurrentXp -=
                LevelProgression.XpToNextLevel(lockedUser.Level);
            lockedUser.Level++;
        }
    }
}
