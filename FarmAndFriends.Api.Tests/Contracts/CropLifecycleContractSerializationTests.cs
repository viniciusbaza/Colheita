using System.Text.Json;
using FarmAndFriends.Api.Contracts.Plots;
using FarmAndFriends.Api.Contracts.Seeds;
using Xunit;

namespace FarmAndFriends.Api.Tests.Contracts;

public sealed class CropLifecycleContractSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void SeedCatalogResponse_ExposesMultiHarvestConfiguration()
    {
        var response = new SeedCatalogResponse(
            "apple_tree",
            "Macieira",
            "Maçã",
            "🍎",
            90,
            30,
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(1),
            100,
            5,
            "apple_crop",
            3,
            3);

        using var json = JsonDocument.Parse(
            JsonSerializer.Serialize(response, JsonOptions));

        Assert.Equal(
            "Maçã",
            json.RootElement.GetProperty("cropName").GetString());
        Assert.Equal(
            "02:00:00",
            json.RootElement.GetProperty("growTime").GetString());
        Assert.Equal(
            "01:00:00",
            json.RootElement.GetProperty("regrowTime").GetString());
        Assert.Equal(
            3,
            json.RootElement.GetProperty("harvestCycles").GetInt32());
        Assert.Equal(
            3,
            json.RootElement.GetProperty("cropAmount").GetInt32());
    }

    [Fact]
    public void HarvestContracts_UseExpectedAndCurrentCycleNames()
    {
        var requestJson = JsonSerializer.Serialize(
            new HarvestCropRequest(2),
            JsonOptions);
        var responseJson = JsonSerializer.Serialize(
            new HarvestResponse(
                Guid.NewGuid(),
                "apple_crop",
                3,
                6,
                125,
                3,
                new DateTime(
                    2026, 8, 23, 13, 0, 0, DateTimeKind.Utc)),
            JsonOptions);

        Assert.Equal("{\"expectedHarvestCycle\":2}", requestJson);
        Assert.Contains("\"currentHarvestCycle\":3", responseJson);
        Assert.Contains("\"readyAt\":", responseJson);
    }
}
