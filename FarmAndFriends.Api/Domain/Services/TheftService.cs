using FarmAndFriends.Api.Dtos.Theft;

namespace FarmAndFriends.Api.Domain.Services;

public class TheftService
{
    private const int BaseDailyLimit = 5;
    private const int XpPerItem = 5;
    private const double MaxBonusChance = 0.20;
    private const double MinBonusChance = 0.05;
    private readonly Random _random = new();

    public TheftResult Steal(
        int remainingYield,
        int playerLevel,
        int alreadyStolenToday)
    {
        if (remainingYield <= 1)
            throw new InvalidOperationException("Nada para roubar");

        // Limite diário escala com level
        var dailyLimit = BaseDailyLimit + (playerLevel / BaseDailyLimit);
        var remainingLimit = dailyLimit - alreadyStolenToday;

        if (remainingLimit <= 0)
            throw new InvalidOperationException("Limite diário atingido");

        // 🎯 Roubo base
        var stolen = 1;
        var gotBonus = false;

        // 🎲 Chance de bônus
        var bonusChance = Math.Min(MinBonusChance + (playerLevel / 20.0), MaxBonusChance);
        var random = _random.NextDouble();

        // Design: roubo sempre tira 1 item.
        // Existe uma chance baixa de bônus (2 itens),
        // nunca permitindo roubar o último item do dono.
        if (random < bonusChance && remainingYield > 2 && remainingLimit > 1)
        {
            stolen = 2;
            gotBonus = true;
        }

        // 🔒 Garantias finais
        stolen = Math.Min(stolen, remainingLimit);
        // Nunca pode roubar o último item
        stolen = Math.Min(stolen, remainingYield - 1);

        var xpGained = gotBonus
            ? stolen * XpPerItem + 2
            : stolen * XpPerItem; 

        return new TheftResult
        {
            StolenAmount = stolen,
            OwnerAmount = remainingYield - stolen,
            XpGained = xpGained,
            GotBonus = gotBonus
        };
    }
}
