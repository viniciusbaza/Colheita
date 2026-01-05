namespace FarmAndFriends.Api.Domain.Rules;

public static class LevelProgression
{
    public static int XpToNextLevel(int level)
    {
        return level switch
        {
            1 => 100,
            2 => 250,
            3 => 500,
            _ => 1000 + (level * 250)
        };
    }
}
