using System.Net;
using System.Net.Http.Json;

using MechanicShop.Api.IntegrationTests.Common;
using Xunit;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Labors.Dtos;
using MechanicShop.Tests.Common.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class LaborsControllerTests : IDisposable
{
    private readonly AppHttpClient _client;
    private readonly IServiceScope _scope;
    private readonly IAppDbContext _context;

    public LaborsControllerTests(WebAppFactory webAppFactory)
    {
        _client = webAppFactory.CreateAppHttpClient();
        _scope = webAppFactory.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<IAppDbContext>();
    }

    public void Dispose() => _scope.Dispose();

[Fact]
    public async Task GetLabors_WhenAuthenticated_ShouldReturnPersistedLaborers()
    {
        await AuthenticateAsManagerAsync();

        var expectedLaborIds = await _context.Employees
            .Select(employee => employee.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        var response = await _client.GetAsync(
            "/api/v1.0/labors",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<LaborDto>>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result!);
        Assert.All(result!, labor => Assert.Contains(labor.LaborId, expectedLaborIds));
    }


    private async Task AuthenticateAsManagerAsync()
    {
        var token = await _client.GenerateTokenAsync(
            TestUsers.Manager,
            TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);
    }
}
