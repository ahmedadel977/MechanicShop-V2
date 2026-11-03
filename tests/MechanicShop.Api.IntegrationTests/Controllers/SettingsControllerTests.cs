using System.Net;
using System.Net.Http.Json;

using MechanicShop.Api.IntegrationTests.Common;
using Xunit;
using MechanicShop.Contracts.Responses;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class SettingsControllerTests(WebAppFactory webAppFactory)
{
    private readonly AppHttpClient _client = webAppFactory.CreateAppHttpClient();

[Fact]
    public async Task GetOperatingHours_WhenRequested_ShouldReturnConfiguredHours()
    {
        var response = await _client.GetAsync(
            "/api/settings/operating-hours",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<OperatingHoursResponse>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result!.OpeningTime < result.ClosingTime);
    }

}
