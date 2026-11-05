using MechanicShop.Infrastructure.Settings;

namespace MechanicShop.Api.IntegrationTests.Infrastructure;

public sealed class JwtSettingsTests
{
    [Fact]
    public void IsValid_RequiresAtLeast32Utf8BytesAndCompleteTokenConfiguration()
    {
        var settings = new JwtSettings
        {
            Secret = new string('k', 31),
            Issuer = "MechanicShop",
            Audience = "MechanicShop.Client",
            TokenExpirationInMinutes = 60
        };

        Assert.False(settings.IsValid);

        settings.Secret = new string('k', 32);

        Assert.True(settings.IsValid);
    }

    [Fact]
    public void IsValid_RejectsMissingIssuerAudienceAndNonPositiveExpiry()
    {
        var settings = new JwtSettings
        {
            Secret = new string('k', 64),
            Issuer = " ",
            Audience = "MechanicShop.Client",
            TokenExpirationInMinutes = 60
        };

        Assert.False(settings.IsValid);

        settings.Issuer = "MechanicShop";
        settings.Audience = "";
        Assert.False(settings.IsValid);

        settings.Audience = "MechanicShop.Client";
        settings.TokenExpirationInMinutes = 0;
        Assert.False(settings.IsValid);
    }
}
