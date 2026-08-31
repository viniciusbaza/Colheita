using System.Security.Claims;
using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Pests;
using FarmAndFriends.Api.Contracts.Shop;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmAndFriends.Api.Tests.Domain.Services;

public sealed class PestFeatureFlagTests
{
    private static readonly DateTime Now =
        new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PestOptions_DefaultsToDisabled()
    {
        Assert.False(new PestOptions().Enabled);
    }

    [Fact]
    public async Task DisabledProcessing_AssignsLegacyIdentityWithoutResolving()
    {
        await using var context = CreateContext();
        var service = CreateService(context, enabled: false);
        var farm = new Farm
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };
        var active = new Plot
        {
            Id = Guid.NewGuid(),
            FarmId = farm.Id,
            Unlocked = true,
            SeedId = "corn",
            PlantedAt = Now.AddHours(-1),
            CurrentHarvestCycle = 1,
            CurrentHarvestCycleStartedAt = Now.AddHours(-1),
            ReadyAt = Now.AddMinutes(-30),
            RemainingYield = 3,
            PestType = PestType.Caterpillar,
            PestStatus = PestStatus.Active,
            PestConsumesAt = Now.AddMinutes(-1)
        };
        var eligible = new Plot
        {
            Id = Guid.NewGuid(),
            FarmId = farm.Id,
            Unlocked = true,
            SeedId = "corn",
            PlantedAt = Now.AddHours(-1),
            CurrentHarvestCycle = 1,
            CurrentHarvestCycleStartedAt = Now.AddHours(-1),
            ReadyAt = Now.AddMinutes(-30),
            RemainingYield = 3
        };

        await service.ProcessLockedFarmAsync(
            farm,
            new[] { active, eligible },
            Now);

        Assert.Equal(3, active.RemainingYield);
        Assert.Equal(PestStatus.Active, active.PestStatus);
        Assert.NotNull(active.PestOccurrenceId);
        Assert.Equal(
            active.PestOccurrenceId,
            PestService.ToContractPest(active)!.OccurrenceId);
        Assert.Equal(PestStatus.None, eligible.PestStatus);
        Assert.Empty(context.ChangeTracker.Entries<Notification>());
    }

    [Fact]
    public async Task DisabledProtection_IsRejectedBeforeDatabaseAccess()
    {
        await using var context = CreateContext();
        var attempt = await CreateService(context, enabled: false)
            .ApplyProtectionAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid());

        Assert.Equal(PestActionFailure.FeatureDisabled, attempt.Failure);
        Assert.Null(attempt.Response);
    }

    [Fact]
    public async Task DisabledProtectionEndpoint_ReturnsStableProblemCode()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var controller = new PestController(
            CreateService(context, enabled: false))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[]
                            {
                                new Claim(
                                    ClaimTypes.NameIdentifier,
                                    userId.ToString())
                            },
                            "test"))
                }
            }
        };

        var result = Assert.IsType<ObjectResult>(
            await controller.ApplyProtection(
                Guid.NewGuid(),
                Guid.NewGuid(),
                CancellationToken.None));
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            PestErrorCodes.FeatureDisabled,
            problem.Extensions["code"]);
    }

    [Fact]
    public async Task RemoveEndpoint_RequiresUuidIdempotencyKey()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var controller = new PestController(
            CreateService(context, enabled: true))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[]
                            {
                                new Claim(
                                    ClaimTypes.NameIdentifier,
                                    userId.ToString())
                            },
                            "test"))
                }
            }
        };

        var result = Assert.IsType<ObjectResult>(
            await controller.Remove(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new RemovePestRequest(Guid.NewGuid()),
                idempotencyKeyValue: null,
                cancellationToken: CancellationToken.None));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(
            PestErrorCodes.IdempotencyKeyRequired,
            problem.Extensions["code"]);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [MemberData(nameof(InvalidOccurrenceRequests))]
    public async Task RemoveEndpoint_RequiresOccurrenceId(
        RemovePestRequest? request)
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var controller = new PestController(
            CreateService(context, enabled: true))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[]
                            {
                                new Claim(
                                    ClaimTypes.NameIdentifier,
                                    userId.ToString())
                            },
                            "test"))
                }
            }
        };

        var result = Assert.IsType<ObjectResult>(
            await controller.Remove(
                Guid.NewGuid(),
                Guid.NewGuid(),
                request,
                Guid.NewGuid().ToString(),
                CancellationToken.None));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(
            PestErrorCodes.OccurrenceIdRequired,
            problem.Extensions["code"]);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    public static TheoryData<RemovePestRequest?>
        InvalidOccurrenceRequests => new()
        {
            null,
            new RemovePestRequest(Guid.Empty)
        };

    [Fact]
    public void DisabledCatalog_IsEmpty()
    {
        using var context = CreateContext();
        var controller = new ShopController(
            context,
            Options.Create(new PestOptions { Enabled = false }));

        var result = Assert.IsType<OkObjectResult>(
            controller.GetItems());
        Assert.Empty(
            Assert.IsAssignableFrom<IEnumerable<ShopItemResponse>>(
                result.Value));
    }

    [Fact]
    public async Task DisabledPurchase_ReturnsStableProblemWithoutDatabaseAccess()
    {
        await using var context = CreateContext();
        var controller = new ShopController(
            context,
            Options.Create(new PestOptions { Enabled = false }));

        var result = Assert.IsType<ObjectResult>(
            await controller.BuyItem(new BuyItemRequest(
                PestOptions.NaturalRepellentItemId,
                1)));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            PestErrorCodes.FeatureDisabled,
            problem.Extensions["code"]);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public void NextPestCheck_IsNullWhenDisabled()
    {
        using var context = CreateContext();
        var service = CreateService(context, enabled: false);
        var plot = FutureSafetyPlot();

        Assert.Null(service.GetNextPestCheckAt(new[] { plot }, Now));
    }

    [Fact]
    public void NextPestCheck_UsesEarliestServerSafetyDeadline()
    {
        using var context = CreateContext();
        var service = CreateService(context, enabled: true);
        var first = FutureSafetyPlot();
        first.ReadyAt = Now.AddMinutes(5);
        var second = FutureSafetyPlot();
        second.ReadyAt = Now.AddMinutes(10);

        Assert.Equal(
            Now.AddMinutes(20),
            service.GetNextPestCheckAt(
                new[] { second, first },
                Now));
    }

    private static Plot FutureSafetyPlot() => new()
    {
        Id = Guid.NewGuid(),
        Unlocked = true,
        SeedId = "corn",
        PlantedAt = Now,
        CurrentHarvestCycle = 1,
        CurrentHarvestCycleStartedAt = Now,
        ReadyAt = Now.AddMinutes(5),
        RemainingYield = 3,
        PestStatus = PestStatus.None
    };

    private static PestService CreateService(
        AppDbContext context,
        bool enabled)
    {
        return new PestService(
            context,
            new ExperienceService(),
            Options.Create(new PestOptions
            {
                Enabled = enabled,
                SafetyPeriodMinutes = 15
            }),
            new FixedTimeProvider());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
