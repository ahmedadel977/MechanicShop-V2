using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using MechanicShop.Api.IntegrationTests.Common;
using Xunit;
using MechanicShop.Application.Features.Billing.Dtos;
using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Tests.Common.Security;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Workorders.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class DashboardControllerTests : IDisposable
{
    private readonly AppHttpClient _client;
    private readonly IServiceScope _scope;
    private readonly IAppDbContext _context;

    public DashboardControllerTests(WebAppFactory webAppFactory)
    {
        _client = webAppFactory.CreateAppHttpClient();
        _scope = webAppFactory.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<IAppDbContext>();
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task GetDashboardStats_WhenAuthenticated_ShouldReturnRequestedDate()
    {
        await AuthenticateAsManagerAsync();
        var requestedDate = new DateOnly(2026, 11, 3);
        var fromUtc = new DateTimeOffset(requestedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var toUtc = fromUtc.AddDays(1);

        var response = await _client.GetAsync(
            $"/api/v1.0/dashboard/stats?fromUtc={UtcIso(fromUtc)}&toUtc={UtcIso(toUtc)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<TodayWorkOrderStatsDto>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(requestedDate, result!.Date);
    }

    [Fact]
    public async Task GetDashboardStats_WhenDateHasNoWorkOrders_ShouldReturnZeroTotals()
    {
        await AuthenticateAsManagerAsync();
        var requestedDate = new DateOnly(2099, 2, 4);
        var fromUtc = new DateTimeOffset(requestedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var toUtc = fromUtc.AddDays(1);

        var response = await _client.GetAsync(
            $"/api/v1.0/dashboard/stats?fromUtc={UtcIso(fromUtc)}&toUtc={UtcIso(toUtc)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TodayWorkOrderStatsDto>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(requestedDate, result!.Date);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.TotalRevenue);
        Assert.Equal(0, result.TotalPartsCost);
        Assert.Equal(0, result.TotalLaborCost);
        Assert.Equal(0, result.UniqueVehicles);
        Assert.Equal(0, result.UniqueCustomers);
    }

    [Fact]
    public async Task GetDashboardStats_WhenCompletedWorkOrderAndInvoiceExist_ShouldReturnCalculatedTotals()
    {
        await AuthenticateAsManagerAsync();

        var customer = await _context.Customers.Include(item => item.Vehicles)
            .OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var vehicle = customer.Vehicles.OrderBy(item => item.Id).First();
        var repairTask = await _context.RepairTasks.Include(item => item.Parts)
            .OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var requestedDate = new DateOnly(2099, 2, 5);
        var startAt = new DateTimeOffset(requestedDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));
        var workOrder = WorkOrderTestDataBuilder.Create()
            .WithVehicle(vehicle.Id)
            .WithLabor(TestUsers.Labor01.Id)
            .WithTimeSlot(startAt, startAt.AddMinutes((int)repairTask.EstimatedDurationInMins))
            .WithRepairTasks(repairTask)
            .Build();
        Assert.False(workOrder.UpdateState(WorkOrderState.InProgress).IsError);
        Assert.False(workOrder.UpdateState(WorkOrderState.Completed).IsError);
        Guid? invoiceId = null;

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var issueResponse = await _client.PostAsJsonAsync<object?>(
                $"/api/v1.0/invoices/workorders/{workOrder.Id}",
                null,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
            var invoice = await issueResponse.Content.ReadFromJsonAsync<InvoiceDto>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(invoice);
            invoiceId = invoice!.InvoiceId;

            var fromUtc = new DateTimeOffset(requestedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            var toUtc = fromUtc.AddDays(1);
            var response = await _client.GetAsync(
                $"/api/v1.0/dashboard/stats?fromUtc={UtcIso(fromUtc)}&toUtc={UtcIso(toUtc)}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<TodayWorkOrderStatsDto>(
                TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal(requestedDate, result!.Date);
            Assert.Equal(1, result.Total);
            Assert.Equal(1, result.Completed);
            Assert.Equal(0, result.Scheduled);
            Assert.Equal(0, result.InProgress);
            Assert.Equal(0, result.Cancelled);
            Assert.Equal(invoice!.Total, result.TotalRevenue);
            Assert.Equal(repairTask.Parts.Sum(part => part.Cost), result.TotalPartsCost);
            Assert.Equal(repairTask.LaborCost, result.TotalLaborCost);
            Assert.Equal(1, result.UniqueVehicles);
            Assert.Equal(1, result.UniqueCustomers);
            Assert.Equal(result.TotalRevenue - result.TotalPartsCost - result.TotalLaborCost, result.NetProfit);
            Assert.Equal(100m, result.CompletionRate);
            Assert.Equal(invoice.Total, result.AverageRevenuePerOrder);
        }
        finally
        {
            if (invoiceId.HasValue)
            {
                await _context.Invoices.Where(invoice => invoice.Id == invoiceId.Value)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }

            await _context.WorkOrders.Where(item => item.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GetDashboardStats_WhenWorkOrderStartsAtCairoMidnight_ShouldIncludeItInSelectedDate()
    {
        await AuthenticateAsManagerAsync();
        var requestedDate = new DateOnly(2026, 11, 27);
        var fromUtc = new DateTimeOffset(requestedDate.Year, requestedDate.Month, requestedDate.Day, 0, 0, 0, TimeSpan.FromHours(2));
        var toUtc = fromUtc.AddDays(1);
        var repairTask = await _context.RepairTasks.Include(item => item.Parts)
            .OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var customer = await _context.Customers.Include(item => item.Vehicles)
            .OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var vehicleId = customer.Vehicles.OrderBy(item => item.Id).First().Id;
        var workOrder = WorkOrderTestDataBuilder.Create()
            .WithVehicle(vehicleId)
            .WithLabor(TestUsers.Labor01.Id)
            .WithTimeSlot(fromUtc, fromUtc.AddMinutes((int)repairTask.EstimatedDurationInMins))
            .WithRepairTasks(repairTask)
            .Build();

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var response = await _client.GetAsync(
                $"/api/v1.0/dashboard/stats?fromUtc={UtcIso(fromUtc)}&toUtc={UtcIso(toUtc)}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<TodayWorkOrderStatsDto>(
                TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal(1, result!.Total);
            Assert.Equal(1, result.Scheduled);
        }
        finally
        {
            await _context.WorkOrders.Where(item => item.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GetDashboardStats_WhenWorkOrderStartsBeforeCairoMidnight_ShouldExcludeItFromSelectedDate()
    {
        await AuthenticateAsManagerAsync();
        var requestedDate = new DateOnly(2026, 11, 27);
        var fromUtc = new DateTimeOffset(requestedDate.Year, requestedDate.Month, requestedDate.Day, 0, 0, 0, TimeSpan.FromHours(2));
        var toUtc = fromUtc.AddDays(1);
        var repairTask = await _context.RepairTasks.Include(item => item.Parts)
            .OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var customer = await _context.Customers.Include(item => item.Vehicles)
            .OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var vehicleId = customer.Vehicles.OrderBy(item => item.Id).First().Id;
        var workOrder = WorkOrderTestDataBuilder.Create()
            .WithVehicle(vehicleId)
            .WithLabor(TestUsers.Labor01.Id)
            .WithTimeSlot(fromUtc.AddMinutes(-1), fromUtc.AddMinutes(-1).AddMinutes(30))
            .WithRepairTasks(repairTask)
            .Build();

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var response = await _client.GetAsync(
                $"/api/v1.0/dashboard/stats?fromUtc={UtcIso(fromUtc)}&toUtc={UtcIso(toUtc)}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<TodayWorkOrderStatsDto>(
                TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal(0, result!.Total);
        }
        finally
        {
            await _context.WorkOrders.Where(item => item.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GetDashboardStats_WhenWorkOrderStartsAtNextCairoMidnight_ShouldExcludeItFromSelectedDate()
    {
        await AuthenticateAsManagerAsync();
        var requestedDate = new DateOnly(2026, 11, 27);
        var fromUtc = new DateTimeOffset(requestedDate.Year, requestedDate.Month, requestedDate.Day, 0, 0, 0, TimeSpan.FromHours(2));
        var toUtc = fromUtc.AddDays(1);
        var repairTask = await _context.RepairTasks.Include(item => item.Parts)
            .OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var customer = await _context.Customers.Include(item => item.Vehicles)
            .OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var vehicleId = customer.Vehicles.OrderBy(item => item.Id).First().Id;
        var workOrder = WorkOrderTestDataBuilder.Create()
            .WithVehicle(vehicleId)
            .WithLabor(TestUsers.Labor01.Id)
            .WithTimeSlot(toUtc, toUtc.AddMinutes(30))
            .WithRepairTasks(repairTask)
            .Build();

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var response = await _client.GetAsync(
                $"/api/v1.0/dashboard/stats?fromUtc={UtcIso(fromUtc)}&toUtc={UtcIso(toUtc)}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<TodayWorkOrderStatsDto>(
                TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal(0, result!.Total);
        }
        finally
        {
            await _context.WorkOrders.Where(item => item.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GetDashboardStats_WhenWorkOrderStartsAtCairoMidnightDuringDaylightSavingTime_ShouldIncludeItInSelectedDate()
    {
        await AuthenticateAsManagerAsync();
        var requestedDate = new DateOnly(2026, 7, 15);
        var fromUtc = new DateTimeOffset(requestedDate.Year, requestedDate.Month, requestedDate.Day, 0, 0, 0, TimeSpan.FromHours(3));
        var toUtc = fromUtc.AddDays(1);
        var repairTask = await _context.RepairTasks.Include(item => item.Parts)
            .OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var customer = await _context.Customers.Include(item => item.Vehicles)
            .OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var vehicleId = customer.Vehicles.OrderBy(item => item.Id).First().Id;
        var workOrder = WorkOrderTestDataBuilder.Create()
            .WithVehicle(vehicleId)
            .WithLabor(TestUsers.Labor01.Id)
            .WithTimeSlot(fromUtc, fromUtc.AddMinutes(30))
            .WithRepairTasks(repairTask)
            .Build();

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var response = await _client.GetAsync(
                $"/api/v1.0/dashboard/stats?fromUtc={UtcIso(fromUtc)}&toUtc={UtcIso(toUtc)}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<TodayWorkOrderStatsDto>(
                TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal(1, result!.Total);
        }
        finally
        {
            await _context.WorkOrders.Where(item => item.Id == workOrder.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    private static string UtcIso(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "Z";

    private async Task AuthenticateAsManagerAsync()
    {
        var token = await _client.GenerateTokenAsync(
            TestUsers.Manager,
            TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);
    }
}
