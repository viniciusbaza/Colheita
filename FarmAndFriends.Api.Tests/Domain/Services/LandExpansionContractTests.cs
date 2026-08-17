using System.Security.Claims;
using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Land;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmAndFriends.Api.Tests.Domain.Services;

public sealed class LandExpansionContractTests
{
    [Fact]
    public void FeatureFlag_DisabledHidesOffer_AndEnabledExposesCanonicalOffer()
    {
        using var context = CreateContext();
        var farmId = Guid.NewGuid();
        var plots = FarmLayoutRules.CreateInitialPlots(farmId).ToList();

        Assert.Null(Service(context, enabled: false).GetOffer(farmId, plots));

        var offer = Service(context, enabled: true).GetOffer(farmId, plots);
        Assert.NotNull(offer);
        Assert.Equal(7, offer.PlotNumber);
        Assert.Equal(28, offer.MaxPlots);
        Assert.Equal(2, offer.MinLevel);
        Assert.Equal(500, offer.Prices.Coins);
        Assert.Equal(2, offer.Prices.PremiumCoins);
        Assert.Null(offer.ExpandsTo);
        Assert.Equal(
            (0, 2),
            (plots.Single(plot => plot.Id == offer.PlotId).X,
                plots.Single(plot => plot.Id == offer.PlotId).Y));
    }

    [Fact]
    public void UnsupportedLayout_HasNoOffer()
    {
        using var context = CreateContext();
        var farmId = Guid.NewGuid();
        var plots = FarmLayoutRules.CreateInitialPlots(farmId).ToList();
        plots[0].X = 99;

        Assert.Null(Service(context, enabled: true).GetOffer(farmId, plots));
    }

    [Fact]
    public void NinthOffer_MapsExpansionAsSevenColumnsAndFourRows()
    {
        using var context = CreateContext();
        var farmId = Guid.NewGuid();
        var plots = FarmLayoutRules.CreateInitialPlots(farmId).ToList();
        FarmLayoutRules.Evaluate(plots).OfferedPlot!.Unlocked = true;
        FarmLayoutRules.Evaluate(plots).OfferedPlot!.Unlocked = true;

        var offer = Service(context, enabled: true).GetOffer(farmId, plots);

        Assert.Equal(9, offer!.PlotNumber);
        Assert.Equal(7, offer.ExpandsTo!.Columns);
        Assert.Equal(4, offer.ExpandsTo.Rows);
    }

    [Fact]
    public async Task PurchaseEndpoint_RequiresUuidIdempotencyKey()
    {
        using var context = CreateContext();
        var controller = Controller(context);

        var result = Assert.IsType<ObjectResult>(await controller.Purchase(
            Guid.NewGuid(),
            new PurchaseLandRequest("coins"),
            idempotencyKeyValue: null,
            CancellationToken.None));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(
            LandErrorCodes.IdempotencyKeyRequired,
            problem.Extensions["code"]);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("premium")]
    [InlineData("Coins")]
    public async Task PurchaseEndpoint_RejectsUnsupportedCurrency(string? currency)
    {
        using var context = CreateContext();
        var controller = Controller(context);

        var result = Assert.IsType<ObjectResult>(await controller.Purchase(
            Guid.NewGuid(),
            currency == null ? null : new PurchaseLandRequest(currency),
            Guid.NewGuid().ToString(),
            CancellationToken.None));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(
            LandErrorCodes.InvalidPaymentCurrency,
            problem.Extensions["code"]);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static LandPurchaseController Controller(AppDbContext context)
    {
        var userId = Guid.NewGuid();
        return new LandPurchaseController(Service(context, enabled: true))
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
    }

    private static LandExpansionService Service(
        AppDbContext context,
        bool enabled) =>
        new(
            context,
            Options.Create(new LandExpansionOptions { Enabled = enabled }),
            TimeProvider.System,
            NullLogger<LandExpansionService>.Instance);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);
}
