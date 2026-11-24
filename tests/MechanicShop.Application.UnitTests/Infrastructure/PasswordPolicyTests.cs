using MechanicShop.Infrastructure.identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using MechanicShop.Application.Common.Interfaces;

namespace MechanicShop.Application.UnitTests.Infrastructure;

public sealed class PasswordPolicyTests
{
    [Fact]
    public async Task IdentityPasswordValidator_UsesCentralizedTwelveCharacterPolicy()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=MechanicShopTests;User Id=test;Password=test;TrustServerCertificate=True",
                ["ConnectionStrings:Redis"] = "localhost",
                ["JwtSettings:Secret"] = new string('k', 32),
                ["JwtSettings:Issuer"] = "MechanicShop.Test",
                ["JwtSettings:Audience"] = "MechanicShop.Test",
                ["JwtSettings:TokenExpirationInMinutes"] = "60"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IUser>(new TestUser(null));
        services.AddMediatR(options =>
            options.RegisterServicesFromAssembly(
                typeof(MechanicShop.Application.Features.Identity.Commands.ChangePassword.ChangePasswordCommand).Assembly));
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<AppUser>>();
        var validator = Assert.Single(userManager.PasswordValidators);

        Assert.Equal(12, userManager.Options.Password.RequiredLength);
        Assert.False(userManager.Options.Password.RequireDigit);
        Assert.False(userManager.Options.Password.RequireUppercase);
        Assert.False(userManager.Options.Password.RequireLowercase);
        Assert.False(userManager.Options.Password.RequireNonAlphanumeric);

        var shortPassword = await validator.ValidateAsync(userManager, new AppUser(), "Short1!");
        var validPassword = await validator.ValidateAsync(userManager, new AppUser(), "long-enough-password");

        Assert.False(shortPassword.Succeeded);
        Assert.True(validPassword.Succeeded);
    }

    private sealed record TestUser(string? Id) : IUser;
}
