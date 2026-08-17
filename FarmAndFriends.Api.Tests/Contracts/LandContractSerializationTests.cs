using System.Text.Json;
using FarmAndFriends.Api.Contracts.Land;
using Xunit;

namespace FarmAndFriends.Api.Tests.Contracts;

public sealed class LandContractSerializationTests
{
    [Fact]
    public void ExpansionDimensions_SerializeAsColumnsAndRows()
    {
        var offer = new LandOfferResponse(
            Guid.NewGuid(),
            9,
            28,
            4,
            new LandPricesResponse(4_000, 4),
            new LandDimensionsResponse(7, 4));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(
            offer,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var expandsTo = json.RootElement.GetProperty("expandsTo");

        Assert.Equal(7, expandsTo.GetProperty("columns").GetInt32());
        Assert.Equal(4, expandsTo.GetProperty("rows").GetInt32());
        Assert.False(expandsTo.TryGetProperty("width", out _));
        Assert.False(expandsTo.TryGetProperty("height", out _));
    }
}
