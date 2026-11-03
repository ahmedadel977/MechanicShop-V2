using System.Net;
using System.Text.Json;

using MechanicShop.Api.IntegrationTests.Common;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public sealed class HealthControllerTests(WebAppFactory webAppFactory)
{
    private readonly AppHttpClient _client = webAppFactory.CreateAppHttpClient();

    [Fact]
    public async Task GetHealth_WhenSqlServerAndRedisAreAvailable_ShouldReportHealthyDependencies()
    {
        using var response = await _client.GetAsync(
            "/health",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        var root = document.RootElement;
        Assert.Equal("Healthy", root.GetProperty("status").GetString());

        var checks = root.GetProperty("checks").EnumerateArray()
            .ToDictionary(
                check => check.GetProperty("name").GetString()!,
                check => check.GetProperty("status").GetString());

        Assert.Equal("Healthy", checks["sqlserver"]);
        Assert.Equal("Healthy", checks["redis"]);
    }

[Fact]
    public async Task GetHealth_WhenRedisIsUnavailable_ShouldReportRedisUnhealthy()
    {
        await webAppFactory.PauseRedisContainerAsync();

        try
        {
            using var response = await _client.GetAsync(
                "/health",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
                cancellationToken: TestContext.Current.CancellationToken);
            var checks = document.RootElement.GetProperty("checks").EnumerateArray()
                .ToDictionary(
                    check => check.GetProperty("name").GetString()!,
                    check => check.GetProperty("status").GetString());

            Assert.Equal("Healthy", checks["sqlserver"]);
            Assert.Equal("Unhealthy", checks["redis"]);
        }
        finally
        {
            await webAppFactory.UnpauseRedisContainerAsync();
        }
    }

[Fact]
    public async Task GetHealth_WhenSqlServerIsUnavailable_ShouldReportSqlServerUnhealthy()
    {
        await webAppFactory.PauseSqlContainerAsync();

        try
        {
            using var response = await _client.GetAsync(
                "/health",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
                cancellationToken: TestContext.Current.CancellationToken);
            var checks = document.RootElement.GetProperty("checks").EnumerateArray()
                .ToDictionary(
                    check => check.GetProperty("name").GetString()!,
                    check => check.GetProperty("status").GetString());

            Assert.Equal("Unhealthy", checks["sqlserver"]);
            Assert.Equal("Healthy", checks["redis"]);
        }
        finally
        {
            await webAppFactory.UnpauseSqlContainerAsync();
        }
    }
}
