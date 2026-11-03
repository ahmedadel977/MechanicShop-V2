using System.Net;
using System.Net.Http.Json;

using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Scheduling.Dtos;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Contracts.Requests.WorkOrders;
using MechanicShop.Domain.Workorders.Enums;
using MechanicShop.Tests.Common.Security;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class WorkOrdersControllerTests : IDisposable
{
    private readonly AppHttpClient _client;
    private readonly IServiceScope _scope;
    private readonly IAppDbContext _context;

    public WorkOrdersControllerTests(WebAppFactory webAppFactory)
    {
        _client = webAppFactory.CreateAppHttpClient();
        _scope = webAppFactory.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<IAppDbContext>();
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task GetWorkOrders_WithValidPagination_ShouldReturnPaginatedList()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var response = await _client.GetAsync(
            "/api/v1.0/workorders?page=1&pageSize=10",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<WorkOrderListItemDto>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.NotNull(result!.Items);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);
    }

    [Theory]
    [InlineData(0, 10, "Page must be greater than 0")]
    [InlineData(-1, 10, "Page must be greater than 0")]
    [InlineData(1, 0, "PageSize must be between 1 and 100")]
    [InlineData(1, 101, "PageSize must be between 1 and 100")]
    [InlineData(1, -1, "PageSize must be between 1 and 100")]
    public async Task GetWorkOrders_WithInvalidPagination_ShouldReturnBadRequest(
        int page,
        int pageSize,
        string expectedError)
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var response = await _client.GetAsync(
            $"/api/v1.0/workorders?page={page}&pageSize={pageSize}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task GetWorkOrders_WithFilters_ShouldApplyFiltersCorrectly()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var vehicle = await _context.Vehicles.OrderBy(vehicle => vehicle.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var repairTask = await _context.RepairTasks.OrderBy(task => task.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var startAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var workOrder = WorkOrderTestDataBuilder.Create()
            .WithVehicle(vehicle.Id)
            .WithLabor(TestUsers.Labor01.Id)
            .WithTimeSlot(startAt, startAt.AddHours(1))
            .WithState(WorkOrderState.InProgress)
            .AtSpot(MechanicShop.Domain.Workorders.Enums.Spot.A)
            .WithRepairTasks(repairTask)
            .Build();

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var startDate = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var endDate = DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var query = $"page=1&pageSize=10&searchTerm={Uri.EscapeDataString(vehicle.Make)}" +
            $"&state={(int)WorkOrderState.InProgress}&vehicleId={vehicle.Id}" +
            $"&laborId={TestUsers.Labor01.Id}&spot={(int)Contracts.Common.Spot.A}" +
            $"&startDateFrom={startDate}&startDateTo={endDate}";

        try
        {
            var response = await _client.GetAsync(
                $"/api/v1.0/workorders?{query}",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<PaginatedList<WorkOrderListItemDto>>(
                TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Contains(result!.Items!, item => item.WorkOrderId == workOrder.Id);
            Assert.Equal(1, result!.PageNumber);
            Assert.Equal(10, result!.PageSize);
            Assert.True(result!.TotalCount >= 1);
        }
        finally
        {
            await _context.WorkOrders.Where(item => item.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GetWorkOrders_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(
            "/api/v1.0/workorders?page=1&pageSize=10",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkOrderById_WithValidId_ShouldReturnWorkOrder()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var workOrder = WorkOrderTestDataBuilder
            .Create()
            .ForToday()
            .WithRepairTasks(
                await _context
                    .RepairTasks.OrderBy(task => task.Id).Take(1)
                    .ToListAsync(TestContext.Current.CancellationToken))
            .WithVehicle((await _context.Vehicles.OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).FirstAsync(TestContext.Current.CancellationToken)))
            .WithLabor(TestUsers.Labor01.Id)
            .Build();

        _context.WorkOrders.Add(workOrder);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var response = await _client.GetAsync(
                $"/api/v1.0/workorders/{workOrder.Id}",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<WorkOrderDto>(
                TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal(workOrder.Id, result!.WorkOrderId);
        }
        finally
        {
            await _context
                .WorkOrders.Where(w => w.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GetWorkOrderById_WithInvalidId_ShouldReturnNotFound()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync(
            $"/api/v1.0/workorders/{nonExistentId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkOrderById_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var workOrderId = Guid.NewGuid();

        var response = await _client.GetAsync(
            $"/api/v1.0/workorders/{workOrderId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateWorkOrder_WithValidRequest_ShouldCreateWorkOrder()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var laborId = Guid.Parse(TestUsers.Labor01.Id);
        var customer = (
            await _context
                .Customers.Include(c => c.Vehicles)
                .OrderBy(customer => customer.Id).FirstAsync(TestContext.Current.CancellationToken)
        )!;
        var vehicle = customer.Vehicles.OrderBy(vehicle => vehicle.Id).First();
        var repairTaskIds = _context.RepairTasks.OrderBy(rt => rt.Id).Select(rt => rt.Id).Take(2).ToList()!;

        var request = new CreateWorkOrderRequest
        {
            Spot = Contracts.Common.Spot.B,
            VehicleId = vehicle.Id,
            StartAtUtc = DateTimeOffset.UtcNow.Date.AddDays(1).AddHours(12),
            LaborId = laborId,
            RepairTaskIds = repairTaskIds,
        };

        WorkOrderDto? dto = null;
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1.0/workorders",
                request,
                TestContext.Current.CancellationToken);

            dto = await response.Content.ReadFromJsonAsync<WorkOrderDto>(
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            Assert.NotNull(dto);
            Assert.Equal(request.VehicleId, dto!.Vehicle!.VehicleId);
            Assert.Equal(request.LaborId, dto!.Labor!.LaborId);
            Assert.Equal(request.Spot.ToString(), dto!.Spot.ToString());
            var persisted = await _context.WorkOrders.AsNoTracking()
                .Include(item => item.RepairTasks)
                .SingleAsync(item => item.Id == dto.WorkOrderId, TestContext.Current.CancellationToken);
            Assert.Equal(request.VehicleId, persisted.VehicleId);
            Assert.Equal(request.LaborId, persisted.LaborId);
            Assert.Equal(request.RepairTaskIds.Order(), persisted.RepairTasks.Select(task => task.Id).Order());
        }
        finally
        {
            if (dto is not null)
            {
                await _context
                    .WorkOrders.Where(w => w.Id == dto.WorkOrderId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }
    }

    [Fact]
    public async Task CreateWorkOrder_WithInvalidRequest_ShouldReturnBadRequest()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var request = new CreateWorkOrderRequest
        {
            VehicleId = Guid.Empty,
            StartAtUtc = default,
            RepairTaskIds = [],
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/workorders",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateWorkOrder_WhenVehicleHasOverlappingWorkOrder_ShouldReturnConflict()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);
        _client.SetAuthorizationHeader(token);

        var vehicleId = await _context.Vehicles.OrderBy(vehicle => vehicle.Id)
            .Select(vehicle => vehicle.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var repairTaskIds = await _context.RepairTasks.OrderBy(task => task.Id)
            .Select(task => task.Id)
            .Take(1)
            .ToListAsync(TestContext.Current.CancellationToken);
        var startAt = DateTimeOffset.UtcNow.Date.AddDays(20).AddHours(12);
        var existing = WorkOrderTestDataBuilder.Create()
            .WithVehicle(vehicleId)
            .WithTimeSlot(startAt, startAt.AddHours(1))
            .AtSpot(MechanicShop.Domain.Workorders.Enums.Spot.A)
            .WithRepairTasks(await _context.RepairTasks.Where(task => repairTaskIds.Contains(task.Id))
                .ToListAsync(TestContext.Current.CancellationToken))
            .Build();

        _context.WorkOrders.Add(existing);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var request = new CreateWorkOrderRequest
            {
                Spot = Contracts.Common.Spot.B,
                VehicleId = vehicleId,
                StartAtUtc = startAt.AddMinutes(15),
                LaborId = Guid.Parse(TestUsers.Labor02.Id),
                RepairTaskIds = repairTaskIds,
            };

            var response = await _client.PostAsJsonAsync(
                "/api/v1.0/workorders", request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Single(await _context.WorkOrders.AsNoTracking()
                .Where(order => order.VehicleId == vehicleId && order.StartAtUtc >= startAt.AddDays(-1))
                .ToListAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await _context.WorkOrders.Where(order => order.Id == existing.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task CreateWorkOrder_WithoutManagerRole_ShouldReturnForbidden()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Labor02, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var request = new CreateWorkOrderRequest
        {
            Spot = Contracts.Common.Spot.A,
            VehicleId = Guid.NewGuid(),
            StartAtUtc = DateTime.UtcNow.AddHours(1),
            RepairTaskIds = [Guid.NewGuid()],
            LaborId = Guid.NewGuid(),
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/workorders",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RelocateWorkOrder_WithValidRequest_ShouldUpdateWorkOrder()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var workOrder = WorkOrderTestDataBuilder
            .Create()
            .ForToday()
            .WithRepairTasks(
                await _context
                    .RepairTasks.OrderBy(task => task.Id).Take(1)
                    .ToListAsync(TestContext.Current.CancellationToken))
            .WithVehicle((await _context.Vehicles.OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).FirstAsync(TestContext.Current.CancellationToken)))
            .WithLabor(TestUsers.Labor01.Id)
            .Build();

        _context.WorkOrders.Add(workOrder);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RelocateWorkOrderRequest
        {
            NewStartAtUtc = DateTime.UtcNow.AddHours(2),
            NewSpot = Contracts.Common.Spot.B,
        };

        try
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/v1.0/workorders/{workOrder.Id}/relocation",
                request,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var persisted = await _context.WorkOrders.AsNoTracking()
                .SingleAsync(item => item.Id == workOrder.Id, TestContext.Current.CancellationToken);
            Assert.Equal(request.NewSpot.ToString(), persisted.Spot.ToString());
            Assert.Equal(request.NewStartAtUtc, persisted.StartAtUtc);
        }
        finally
        {
            await _context
                .WorkOrders.Where(w => w.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task RelocateWorkOrder_WithInvalidId_ShouldReturnNotFound()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var nonExistentId = Guid.NewGuid();

        var request = new RelocateWorkOrderRequest
        {
            NewStartAtUtc = DateTime.UtcNow.AddHours(2),
            NewSpot = Contracts.Common.Spot.B,
        };

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/workorders/{nonExistentId}/relocation",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignLabor_WithValidRequest_ShouldAssignLabor()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var workOrder = WorkOrderTestDataBuilder
            .Create()
            .ForToday()
            .WithRepairTasks(
                await _context
                    .RepairTasks.OrderBy(task => task.Id).Take(1)
                    .ToListAsync(TestContext.Current.CancellationToken))
            .WithVehicle((await _context.Vehicles.OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).FirstAsync(TestContext.Current.CancellationToken)))
            .WithLabor(TestUsers.Labor01.Id)
            .Build();

        _context.WorkOrders.Add(workOrder);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new AssignLaborRequest { LaborId = TestUsers.Labor02.Id };

        try
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/v1.0/workorders/{workOrder.Id}/labor",
                request,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var persisted = await _context.WorkOrders.AsNoTracking()
                .SingleAsync(item => item.Id == workOrder.Id, TestContext.Current.CancellationToken);
            Assert.Equal(Guid.Parse(request.LaborId), persisted.LaborId);
        }
        finally
        {
            await _context
                .WorkOrders.Where(w => w.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task AssignLabor_WithInvalidLaborId_ShouldReturnBadRequest()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var request = new AssignLaborRequest { LaborId = Guid.Empty.ToString() };

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/workorders/{Guid.NewGuid()}/labor",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkOrderState_WithValidRequest_ShouldUpdateState()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var workOrder = WorkOrderTestDataBuilder
            .Create()
            .ForToday()
            .WithRepairTasks(
                await _context
                    .RepairTasks.OrderBy(task => task.Id).Take(1)
                    .ToListAsync(TestContext.Current.CancellationToken))
            .WithVehicle((await _context.Vehicles.OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).FirstAsync(TestContext.Current.CancellationToken)))
            .WithTimeSlot(
                DateTimeOffset.UtcNow.AddMinutes(-3),
                DateTimeOffset.UtcNow.AddMinutes(30))
            .WithLabor(TestUsers.Labor01.Id)
            .Build();

        _context.WorkOrders.Add(workOrder);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var request = new UpdateWorkOrderStateRequest
            {
                State = Contracts.Common.WorkOrderState.InProgress,
            };

            var response = await _client.PutAsJsonAsync(
                $"/api/v1.0/workorders/{workOrder.Id}/state",
                request,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var persisted = await _context.WorkOrders.AsNoTracking()
                .SingleAsync(item => item.Id == workOrder.Id, TestContext.Current.CancellationToken);
            Assert.Equal(WorkOrderState.InProgress, persisted.State);
        }
        finally
        {
            await _context
                .WorkOrders.Where(w => w.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task UpdateWorkOrderState_AsLabor_WithSelfScopedAccess_ShouldSucceed()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Labor01, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var workOrder = WorkOrderTestDataBuilder
            .Create()
            .ForToday()
            .WithRepairTasks(
                await _context
                    .RepairTasks.OrderBy(task => task.Id).Take(1)
                    .ToListAsync(TestContext.Current.CancellationToken))
            .WithTimeSlot(
                DateTimeOffset.UtcNow.AddMinutes(-3),
                DateTimeOffset.UtcNow.AddMinutes(30))
            .WithLabor(TestUsers.Labor01.Id)
            .WithVehicle((await _context.Vehicles.OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).FirstAsync(TestContext.Current.CancellationToken)))
            .Build();

        _context.WorkOrders.Add(workOrder);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var request = new UpdateWorkOrderStateRequest
            {
                State = Contracts.Common.WorkOrderState.InProgress,
            };

            var response = await _client.PutAsJsonAsync(
                $"/api/v1.0/workorders/{workOrder.Id}/state",
                request,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        finally
        {
            await _context
                .WorkOrders.Where(w => w.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task UpdateWorkOrderState_AsLabor_WithoutSelfScopedAccess_ShouldReturnForbidden()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Labor02, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var workOrder = WorkOrderTestDataBuilder
            .Create()
            .ForToday()
            .WithRepairTasks(
                await _context
                    .RepairTasks.OrderBy(task => task.Id).Take(1)
                    .ToListAsync(TestContext.Current.CancellationToken))
            .WithVehicle((await _context.Vehicles.OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).FirstAsync(TestContext.Current.CancellationToken)))
            .WithLabor(TestUsers.Labor01.Id)
            .Build();

        _context.WorkOrders.Add(workOrder);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var request = new UpdateWorkOrderStateRequest
            {
                State = Contracts.Common.WorkOrderState.InProgress,
            };

            var response = await _client.PutAsJsonAsync(
                $"/api/v1.0/workorders/{workOrder.Id}/state",
                request,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await _context
                .WorkOrders.Where(w => w.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task UpdateRepairTasks_WithValidRequest_ShouldUpdateTasks()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var workOrder = WorkOrderTestDataBuilder
            .Create()
            .ForToday()
            .WithRepairTasks(
                await _context
                    .RepairTasks.OrderBy(task => task.Id).Take(1)
                    .ToListAsync(TestContext.Current.CancellationToken))
            .WithVehicle((await _context.Vehicles.OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).FirstAsync(TestContext.Current.CancellationToken)))
            .WithLabor(TestUsers.Labor01.Id)
            .Build();

        _context.WorkOrders.Add(workOrder);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var assignedRepairTaskIds = workOrder.RepairTasks.Select(rt => rt.Id).ToHashSet();

        var newRepairTask = await _context
            .RepairTasks.AsNoTracking()
            .Where(rt => !assignedRepairTaskIds.Contains(rt.Id))
            .FirstAsync(TestContext.Current.CancellationToken);

        var request = new ModifyRepairTaskRequest { RepairTaskIds = [newRepairTask.Id] };

        try
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/v1.0/workorders/{workOrder.Id}/repair-task",
                request,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var persistedRepairTaskIds = await _context.WorkOrders
                .Where(item => item.Id == workOrder.Id)
                .SelectMany(item => item.RepairTasks.Select(task => task.Id))
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal([newRepairTask.Id], persistedRepairTaskIds);
        }
        finally
        {
            await _context
                .WorkOrders.Where(w => w.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task UpdateRepairTasks_WithoutManagerRole_ShouldReturnForbidden()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Labor01, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var workOrderId = Guid.NewGuid();

        var request = new ModifyRepairTaskRequest { RepairTaskIds = [Guid.NewGuid()] };

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/workorders/{workOrderId}/repair-task",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteWorkOrder_WithValidId_ShouldDeleteWorkOrder()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var workOrder = WorkOrderTestDataBuilder
            .Create()
            .ForToday()
            .WithRepairTasks(
                await _context
                    .RepairTasks.OrderBy(task => task.Id).Take(1)
                    .ToListAsync(TestContext.Current.CancellationToken))
            .WithVehicle((await _context.Vehicles.OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).FirstAsync(TestContext.Current.CancellationToken)))
            .WithLabor(TestUsers.Labor01.Id)
            .Build();

        _context.WorkOrders.Add(workOrder);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var response = await _client.DeleteAsync(
                $"/api/v1.0/workorders/{workOrder.Id}",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.False(await _context.WorkOrders.AnyAsync(
                item => item.Id == workOrder.Id,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await _context
                .WorkOrders.Where(w => w.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task DeleteWorkOrder_WithInvalidId_ShouldReturnNotFound()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var nonExistentId = Guid.NewGuid();

        var response = await _client.DeleteAsync(
            $"/api/v1.0/workorders/{nonExistentId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteWorkOrder_WithoutManagerRole_ShouldReturnForbidden()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Labor01, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var workOrderId = Guid.NewGuid();

        var response = await _client.DeleteAsync(
            $"/api/v1.0/workorders/{workOrderId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSchedule_WithSpecificDate_ShouldReturnSchedule()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Labor01, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1.0/workorders/schedule/{today:yyyy-MM-dd}");

        request.Headers.Add("X-TimeZone", "America/Montreal");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ScheduleDto>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(today, result!.OnDate);
        Assert.NotEmpty(result.Spots);
    }

    [Fact]
    public async Task GetSchedule_WithLaborFilter_ShouldReturnFilteredSchedule()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Labor01, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var workOrder = WorkOrderTestDataBuilder
            .Create()
            .ForToday()
            .WithRepairTasks(
                await _context
                    .RepairTasks.OrderBy(task => task.Id).Take(1)
                    .ToListAsync(TestContext.Current.CancellationToken))
            .WithVehicle((await _context.Vehicles.OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).FirstAsync(TestContext.Current.CancellationToken)))
            .WithLabor(TestUsers.Labor01.Id)
            .Build();

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/v1.0/workorders/schedule/{today:yyyy-MM-dd}");

            request.Headers.Add("X-TimeZone", "America/Montreal");

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ScheduleDto>(
                TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Contains(
                result!.Spots,
                spot => spot.Slots.Any(slot => slot.WorkOrderId == workOrder.Id));
        }
        finally
        {
            _context.WorkOrders.Remove(workOrder);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GetSchedule_WhenWorkOrderFallsOnPreviousUtcDate_ShouldIncludeItInLocalDate()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);
        _client.SetAuthorizationHeader(token);

        var vehicleId = await _context.Vehicles.OrderBy(vehicle => vehicle.Id)
            .Select(vehicle => vehicle.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var task = await _context.RepairTasks.OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var startAt = new DateTimeOffset(2099, 2, 6, 2, 0, 0, TimeSpan.Zero);
        var workOrder = WorkOrderTestDataBuilder.Create()
            .WithVehicle(vehicleId)
            .WithTimeSlot(startAt, startAt.AddMinutes((int)task.EstimatedDurationInMins))
            .WithRepairTasks(task)
            .Build();

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/v1.0/workorders/schedule/2099-02-05");
            request.Headers.Add("X-TimeZone", "America/Montreal");

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ScheduleDto>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(result);
            Assert.Contains(result!.Spots, spot =>
                spot.Slots.Any(slot => slot.WorkOrderId == workOrder.Id));
        }
        finally
        {
            await _context.WorkOrders.Where(order => order.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task AssignLabor_WithNonExistingWorkOrder_ShouldReturnNotFound()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);

        var request = new AssignLaborRequest { LaborId = TestUsers.Labor01.Id };

        var nonExistentId = Guid.NewGuid();

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/workorders/{nonExistentId}/labor",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignLabor_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var request = new AssignLaborRequest { LaborId = TestUsers.Labor01.Id };
        var workOrderId = Guid.NewGuid();

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/workorders/{workOrderId}/labor",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkOrderState_WithInvalidWorkOrderId_ShouldReturnNotFound()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);
        _client.SetAuthorizationHeader(token);

        var request = new UpdateWorkOrderStateRequest
        {
            State = Contracts.Common.WorkOrderState.InProgress,
        };

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/workorders/{Guid.NewGuid()}/state",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkOrderState_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var request = new UpdateWorkOrderStateRequest
        {
            State = Contracts.Common.WorkOrderState.InProgress,
        };

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/workorders/{Guid.NewGuid()}/state",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRepairTasks_WithInvalidWorkOrderId_ShouldReturnNotFound()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);
        _client.SetAuthorizationHeader(token);

        var request = new ModifyRepairTaskRequest { RepairTaskIds = [Guid.NewGuid()] };

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/workorders/{Guid.NewGuid()}/repair-task",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRepairTasks_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var request = new ModifyRepairTaskRequest { RepairTaskIds = [Guid.NewGuid()] };

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/workorders/{Guid.NewGuid()}/repair-task",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteWorkOrder_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.DeleteAsync(
            $"/api/v1.0/workorders/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSchedule_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1.0/workorders/schedule/{today:yyyy-MM-dd}");

        request.Headers.Add("X-TimeZone", "America/Montreal");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

