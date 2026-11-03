using System.Net;
using System.Net.Http.Json;

using MechanicShop.Api.IntegrationTests.Common;
using Xunit;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Dtos;
using MechanicShop.Contracts.Requests.RepairTasks;
using MechanicShop.Tests.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class RepairTasksControllerTests : IDisposable
{
    private readonly AppHttpClient _client;
    private readonly IServiceScope _scope;
    private readonly IAppDbContext _context;

    public RepairTasksControllerTests(WebAppFactory webAppFactory)
    {
        _client = webAppFactory.CreateAppHttpClient();
        _scope = webAppFactory.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<IAppDbContext>();
    }

    public void Dispose() => _scope.Dispose();

[Fact]
    public async Task GetRepairTasks_WhenAuthenticated_ShouldReturnPersistedRepairTasks()
    {
        await AuthenticateAsManagerAsync();

        var expectedTaskIds = await _context.RepairTasks
            .Select(task => task.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        var response = await _client.GetAsync(
            "/api/v1.0/repair-tasks",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<RepairTaskDto>>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result!);
        Assert.All(result!, task => Assert.Contains(task.RepairTaskId, expectedTaskIds));
    }

[Fact]
    public async Task GetRepairTaskById_WhenTaskExists_ShouldReturnTheRequestedTask()
    {
        await AuthenticateAsManagerAsync();

        var expected = await _context.RepairTasks.OrderBy(task => task.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var response = await _client.GetAsync(
            $"/api/v1.0/repair-tasks/{expected.Id}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = await response.Content.ReadFromJsonAsync<RepairTaskDto>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual!.RepairTaskId);
        Assert.Equal(expected.Name, actual.Name);
    }

[Fact]
    public async Task GetRepairTaskById_WhenTaskDoesNotExist_ShouldReturnNotFound()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.GetAsync(
            $"/api/v1.0/repair-tasks/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

[Fact]
    public async Task RepairTaskMutationEndpoints_AsLabor_ShouldReturnForbidden()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Labor01, TestContext.Current.CancellationToken);
        _client.SetAuthorizationHeader(token);

        var existing = await _context.RepairTasks.OrderBy(task => task.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1.0/repair-tasks",
            new CreateRepairTaskRequest
            {
                Name = $"Denied {Guid.NewGuid():N}",
                LaborCost = 80,
                EstimatedDurationInMins = Contracts.Common.RepairDurationInMinutes.Min30,
                Parts = []
            },
            TestContext.Current.CancellationToken);
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1.0/repair-tasks/{existing.Id}",
            new UpdateRepairTaskRequest
            {
                Name = "Unauthorized update",
                LaborCost = 80,
                EstimatedDurationInMins = Contracts.Common.RepairDurationInMinutes.Min30,
                Parts = []
            },
            TestContext.Current.CancellationToken);
        var deleteResponse = await _client.DeleteAsync(
            $"/api/v1.0/repair-tasks/{existing.Id}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

[Fact]
    public async Task CreateRepairTask_WhenRequestIsEmpty_ShouldReturnBadRequest()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/repair-tasks",
            new CreateRepairTaskRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

[Fact]
    public async Task UpdateRepairTask_WhenRequestIsEmpty_ShouldReturnBadRequest()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/repair-tasks/{Guid.NewGuid()}",
            new UpdateRepairTaskRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

[Fact]
    public async Task DeleteRepairTask_WhenRepairTaskDoesNotExist_ShouldReturnNotFound()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.DeleteAsync(
            $"/api/v1.0/repair-tasks/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

[Fact]
    public async Task RepairTaskEndpoints_WhenManagerCreatesUpdatesAndDeletes_ShouldPersistEachChange()
    {
        await AuthenticateAsManagerAsync();
        var taskName = $"Integration Repair Task {Guid.NewGuid():N}";
        Guid? repairTaskId = null;

        try
        {
            var createRequest = new CreateRepairTaskRequest
            {
                Name = taskName,
                LaborCost = 125,
                EstimatedDurationInMins = Contracts.Common.RepairDurationInMinutes.Min30,
                Parts = [new CreateRepairTaskPartRequest { Name = "Filter", Cost = 25, Quantity = 2 }]
            };

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1.0/repair-tasks",
                createRequest,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<RepairTaskDto>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(created);
            repairTaskId = created!.RepairTaskId;
            var createdPart = Assert.Single(created.Parts);

            var updateRequest = new UpdateRepairTaskRequest
            {
                Name = $"Updated {taskName}",
                LaborCost = 175,
                EstimatedDurationInMins = Contracts.Common.RepairDurationInMinutes.Min60,
                Parts =
                [
                    new UpdateRepairTaskPartRequest
                    {
                        PartId = createdPart.PartId,
                        Name = "Premium Filter",
                        Cost = 35,
                        Quantity = 1
                    }
                ]
            };

            var updateResponse = await _client.PutAsJsonAsync(
                $"/api/v1.0/repair-tasks/{repairTaskId}",
                updateRequest,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var persisted = await _context.RepairTasks.AsNoTracking()
                .Include(task => task.Parts)
                .SingleAsync(task => task.Id == repairTaskId, TestContext.Current.CancellationToken);
            Assert.Equal(updateRequest.Name, persisted.Name);
            Assert.Equal(updateRequest.LaborCost, persisted.LaborCost);
            Assert.Equal("Premium Filter", Assert.Single(persisted.Parts).Name);

            var deleteResponse = await _client.DeleteAsync(
                $"/api/v1.0/repair-tasks/{repairTaskId}",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            Assert.False(await _context.RepairTasks.AnyAsync(
                task => task.Id == repairTaskId,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            if (repairTaskId.HasValue)
            {
                await _context.RepairTasks.Where(task => task.Id == repairTaskId.Value)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }
    }


    private async Task AuthenticateAsManagerAsync()
    {
        var token = await _client.GenerateTokenAsync(
            TestUsers.Manager,
            TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);
    }
}
