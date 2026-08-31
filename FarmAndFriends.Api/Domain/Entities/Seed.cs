namespace FarmAndFriends.Api.Domain.Entities;

public class Seed
{
    public string Id { get; set; } = null!; // ex: "carrot", "corn"
    public string Name { get; set; } = null!;
    public string CropName { get; set; } = null!;
    public string Icon { get; set; } = null!; // ex: "🥕", "🌽"

    // Economia sell_price >= buy_price * 2 Ou o jogo quebra.
    public int BuyPrice { get; set; }
    public int SellPrice { get; set; }

    // Tempo
    public TimeSpan GrowTime { get; set; }
    public TimeSpan? RegrowTime { get; set; }

    // Risco / gameplay
    public int TheftChancePercent { get; set; } // 0 - 100

    // Progressão
    public int MinLevel { get; set; } = 1;

    // Retorno por colheita
    public string CropId { get; set; } = null!;
    public int CropAmount { get; set; }
    public int HarvestCycles { get; set; } = 1;
}
