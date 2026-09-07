using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Dtos.Users;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FarmAndFriends.Api.Tests.Contracts;

public sealed class ProfileAvatarContractTests
{
    [Theory]
    [InlineData("avatar-1")]
    [InlineData("avatar-2")]
    [InlineData("avatar-3")]
    [InlineData("avatar-4")]
    [InlineData("avatar-5")]
    public void Allowlist_AcceptsExactlyApprovedChoices(string avatarId) =>
        Assert.True(ProfileAvatarRules.IsAllowed(avatarId));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("avatar-0")]
    [InlineData("avatar-6")]
    [InlineData("Avatar-1")]
    [InlineData(" avatar-1 ")]
    [InlineData("https://example.com/avatar.png")]
    public async Task UpdateAvatar_InvalidChoiceIsBadRequest(string? avatarId)
    {
        await using var context = CreateContext();
        var controller = CreateController(context, Guid.NewGuid().ToString());
        Assert.IsType<BadRequestObjectResult>(await controller.UpdateAvatar(new(avatarId)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("avatar-6")]
    [InlineData(" avatar-1 ")]
    public async Task UpdateProfile_InvalidAvatarIsBadRequest(string? avatarId)
    {
        await using var context = CreateContext();
        var controller = CreateController(context, Guid.NewGuid().ToString());
        Assert.IsType<BadRequestObjectResult>(
            await controller.UpdateProfile(new(avatarId, "Minha fazenda")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateProfile_MissingFarmNameIsBadRequest(string? farmName)
    {
        await using var context = CreateContext();
        var controller = CreateController(context, Guid.NewGuid().ToString());
        Assert.IsType<BadRequestObjectResult>(
            await controller.UpdateProfile(new("avatar-1", farmName)));
    }

    [Fact]
    public async Task UpdateProfile_FarmNameLongerThanLimitIsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, Guid.NewGuid().ToString());
        Assert.IsType<BadRequestObjectResult>(
            await controller.UpdateProfile(new("avatar-1", new string('a', FarmNameRules.MaxLength + 1))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Identity_IsRequiredForReadAndWrite(string? claim)
    {
        await using var context = CreateContext();
        var controller = CreateController(context, claim);
        Assert.IsType<UnauthorizedResult>(await controller.UpdateAvatar(new("avatar-1")));
        Assert.IsType<UnauthorizedResult>(
            await controller.UpdateProfile(new("avatar-1", "Minha fazenda")));
        Assert.IsType<UnauthorizedResult>(await controller.Me());
        Assert.NotNull(typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void DefaultAndContracts_AreStable()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.AvatarId))!;
        Assert.False(property.IsNullable);
        Assert.Equal(32, property.GetMaxLength());
        Assert.Equal("avatar-1", property.GetDefaultValue());
        Assert.Equal("avatar-1", new User().AvatarId);
        Assert.Equal(30, FarmNameRules.MaxLength);
        Assert.Equal("Minha fazenda", FarmNameRules.Normalize("  Minha fazenda  "));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Null(JsonSerializer.Deserialize<UpdateAvatarRequest>("{}", options)!.AvatarId);
        Assert.Equal("{\"avatarId\":\"avatar-2\"}", JsonSerializer.Serialize(new AvatarResponse("avatar-2"), options));
        var profileRequest = JsonSerializer.Deserialize<UpdateProfileRequest>("{}", options)!;
        Assert.Null(profileRequest.AvatarId);
        Assert.Null(profileRequest.FarmName);
        Assert.Equal(
            "{\"avatarId\":\"avatar-2\",\"farmId\":\"00000000-0000-0000-0000-000000000001\",\"farmName\":\"Minha fazenda\"}",
            JsonSerializer.Serialize(
                new ProfileResponse("avatar-2", Guid.Parse("00000000-0000-0000-0000-000000000001"), "Minha fazenda"),
                options));
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);

    private static UsersController CreateController(AppDbContext context, string? claim) => new(context)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    claim is null ? [] : [new Claim(ClaimTypes.NameIdentifier, claim)], "test"))
            }
        }
    };
}
