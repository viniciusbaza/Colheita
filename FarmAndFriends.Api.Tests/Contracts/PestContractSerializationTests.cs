using System.Text.Json;
using FarmAndFriends.Api.Contracts.Pests;
using FarmAndFriends.Api.Contracts.Shop;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Dtos.Theft;
using Xunit;

namespace FarmAndFriends.Api.Tests.Contracts;

public sealed class PestContractSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(PestStatus.None, "none")]
    [InlineData(PestStatus.Scheduled, "scheduled")]
    [InlineData(PestStatus.Active, "active")]
    [InlineData(PestStatus.Removed, "removed")]
    [InlineData(PestStatus.Consumed, "consumed")]
    [InlineData(PestStatus.CancelledByTheft, "cancelledByTheft")]
    [InlineData(PestStatus.CancelledByHarvest, "cancelledByHarvest")]
    [InlineData(PestStatus.CancelledByProtection, "cancelledByProtection")]
    public void PestStatus_UsesStabilizedCamelCaseValue(
        PestStatus status,
        string expected)
    {
        Assert.Equal(expected, PestService.ToContractStatus(status));
    }

    [Fact]
    public void BuyItemResponse_UsesStabilizedCamelCaseNames()
    {
        var json = JsonSerializer.Serialize(
            new BuyItemResponse("natural_repellent", 2, 5, 40),
            JsonOptions);

        Assert.Contains("\"itemId\":\"natural_repellent\"", json);
        Assert.Contains("\"quantity\":2", json);
        Assert.Contains("\"inventoryQuantity\":5", json);
        Assert.Contains("\"coinsLeft\":40", json);
        Assert.DoesNotContain("\"itemQuantity\"", json);
    }

    [Fact]
    public void PestActionResponse_IncludesUpdatedPestProjection()
    {
        var now = new DateTime(
            2026,
            7,
            30,
            12,
            0,
            0,
            DateTimeKind.Utc);
        var response = new PestActionResponse(
            Guid.NewGuid(),
            "removed",
            2,
            new PestStateResponse(
                "caterpillar",
                "removed",
                now.AddMinutes(-20),
                now.AddMinutes(-15),
                now.AddMinutes(-15),
                now,
                now.AddMinutes(-1),
                0,
                false,
                Guid.NewGuid()),
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            5,
            102,
            true,
            false);

        var json = JsonSerializer.Serialize(response, JsonOptions);

        Assert.Contains("\"plotId\":", json);
        Assert.Contains("\"status\":\"removed\"", json);
        Assert.Contains("\"remainingYield\":2", json);
        Assert.Contains("\"pest\":{", json);
        Assert.Contains("\"type\":\"caterpillar\"", json);
        Assert.Contains("\"canRemove\":false", json);
        Assert.Contains("\"occurrenceId\":", json);
        Assert.Contains("\"completionId\":", json);
        Assert.Contains("\"pestOccurrenceId\":", json);
        Assert.Contains("\"coinsGained\":2", json);
        Assert.Contains("\"xpGained\":5", json);
        Assert.Contains("\"coins\":102", json);
        Assert.Contains("\"rewardGranted\":true", json);
        Assert.Contains("\"replayed\":false", json);
    }

    [Fact]
    public void RemovePestRequest_UsesOccurrenceId()
    {
        var occurrenceId = Guid.NewGuid();

        var json = JsonSerializer.Serialize(
            new RemovePestRequest(occurrenceId),
            JsonOptions);

        Assert.Equal(
            $"{{\"pestOccurrenceId\":\"{occurrenceId}\"}}",
            json);
    }

    [Fact]
    public void ApplyPestProtectionResponse_UsesStabilizedCamelCaseNames()
    {
        var protectedUntil = new DateTime(
            2026,
            7,
            30,
            16,
            0,
            0,
            DateTimeKind.Utc);
        var response = new ApplyPestProtectionResponse(
            Guid.NewGuid(),
            "none",
            null,
            null,
            "natural_repellent",
            3,
            protectedUntil);

        var json = JsonSerializer.Serialize(response, JsonOptions);

        Assert.Contains("\"plotId\":", json);
        Assert.Contains("\"status\":\"none\"", json);
        Assert.Contains("\"remainingYield\":null", json);
        Assert.Contains("\"pest\":null", json);
        Assert.Contains("\"itemId\":\"natural_repellent\"", json);
        Assert.Contains("\"remainingItemQuantity\":3", json);
        Assert.Contains("\"protectedUntil\":", json);
    }

    [Fact]
    public void TheftResponse_IncludesAuthoritativePestCancellation()
    {
        var json = JsonSerializer.Serialize(
            new TheftResponse(
                Guid.NewGuid(),
                Stolen: 1,
                OwnerWillReceive: 2,
                XpGained: 45,
                PestCancelled: true),
            JsonOptions);

        Assert.Contains("\"pestCancelled\":true", json);
        Assert.Contains("\"ownerWillReceive\":2", json);
    }
}
