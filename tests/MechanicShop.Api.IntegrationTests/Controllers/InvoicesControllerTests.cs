using System.Net;
using System.Net.Http.Json;

using MechanicShop.Api.IntegrationTests.Common;
using Xunit;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Billing.Dtos;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Enums;
using MechanicShop.Domain.RepairTasks.Parts;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Billing;
using MechanicShop.Tests.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class InvoicesControllerTests : IDisposable
{
    private readonly AppHttpClient _client;
    private readonly IServiceScope _scope;
    private readonly IAppDbContext _context;

    public InvoicesControllerTests(WebAppFactory webAppFactory)
    {
        _client = webAppFactory.CreateAppHttpClient();
        _scope = webAppFactory.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<IAppDbContext>();
    }

    public void Dispose() => _scope.Dispose();

[Fact]
    public async Task GetInvoice_WhenInvoiceDoesNotExist_ShouldReturnNotFound()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.GetAsync(
            $"/api/v1.0/invoices/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

[Fact]
    public async Task IssueInvoice_WhenWorkOrderDoesNotExist_ShouldReturnNotFound()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.PostAsJsonAsync<object?>(
            $"/api/v1.0/invoices/workorders/{Guid.NewGuid()}",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

[Fact]
    public async Task GetInvoicePdf_WhenInvoiceDoesNotExist_ShouldReturnNotFound()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.GetAsync(
            $"/api/v1.0/invoices/{Guid.NewGuid()}/pdf",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

[Fact]
    public async Task SettleInvoice_WhenInvoiceDoesNotExist_ShouldReturnNotFound()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.PutAsJsonAsync<object?>(
            $"/api/v1.0/invoices/{Guid.NewGuid()}/payments",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

[Fact]
    public async Task InvoiceEndpoints_WhenWorkOrderIsCompleted_ShouldIssueRetrieveAndSettle()
    {
        await AuthenticateAsManagerAsync();

        var vehicle = await _context.Vehicles.OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var repairTask = await _context.RepairTasks.OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var workOrder = WorkOrderTestDataBuilder.Create()
            .WithVehicle(vehicle.Id)
            .WithLabor(TestUsers.Labor01.Id)
            .WithRepairTasks(repairTask)
            .InProgress()
            .Build();
        Assert.False(workOrder.UpdateState(MechanicShop.Domain.Workorders.Enums.WorkOrderState.Completed).IsError);
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
            var issued = await issueResponse.Content.ReadFromJsonAsync<InvoiceDto>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(issued);
            invoiceId = issued!.InvoiceId;
            Assert.Equal(workOrder.Id, issued.WorkOrderId);
            Assert.NotEmpty(issued.Items);

            var duplicateIssueResponse = await _client.PostAsJsonAsync<object?>(
                $"/api/v1.0/invoices/workorders/{workOrder.Id}",
                null,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, duplicateIssueResponse.StatusCode);

            var getResponse = await _client.GetAsync(
                $"/api/v1.0/invoices/{invoiceId}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = await getResponse.Content.ReadFromJsonAsync<InvoiceDto>(
                TestContext.Current.CancellationToken);
            Assert.Equal(invoiceId, fetched!.InvoiceId);

            var settleResponse = await _client.PutAsJsonAsync<object?>(
                $"/api/v1.0/invoices/{invoiceId}/payments",
                null,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, settleResponse.StatusCode);

            var duplicateSettlementResponse = await _client.PutAsJsonAsync<object?>(
                $"/api/v1.0/invoices/{invoiceId}/payments",
                null,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, duplicateSettlementResponse.StatusCode);

            var persisted = await _context.Invoices.AsNoTracking()
                .SingleAsync(invoice => invoice.Id == invoiceId, TestContext.Current.CancellationToken);
            Assert.Equal(InvoiceStatus.Paid, persisted.Status);
            Assert.NotNull(persisted.PaidAt);
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
    public async Task GetInvoicePdf_WhenInvoiceContainsArabicCustomerAndRepairTask_ShouldReturnPdf()
    {
        await AuthenticateAsManagerAsync();

        var suffix = Guid.NewGuid();
        var vehicle = Vehicle.Create(Guid.NewGuid(), "Toyota", "Corolla", 2022, $"A{suffix:N}"[..8]).Value;
        var customer = Customer.Create(
            Guid.NewGuid(),
            "أحمد محمد",
            $"+20{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}",
            $"arabic-{suffix:N}@example.test",
            [vehicle]).Value;
        var part = Part.Create(Guid.NewGuid(), "فلتر زيت", 25m, 1).Value;
        var repairTask = RepairTask.Create(
            Guid.NewGuid(),
            "تغيير زيت المحرك",
            75m,
            RepairDurationInMinutes.Min30,
            [part]).Value;
        var startAt = new DateTimeOffset(2099, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var workOrder = WorkOrderTestDataBuilder.Create()
            .WithVehicle(vehicle.Id)
            .WithLabor(TestUsers.Labor01.Id)
            .WithTimeSlot(startAt, startAt.AddMinutes(30))
            .WithRepairTasks(repairTask)
            .Build();
        Assert.False(workOrder.UpdateState(MechanicShop.Domain.Workorders.Enums.WorkOrderState.InProgress).IsError);
        Assert.False(workOrder.UpdateState(MechanicShop.Domain.Workorders.Enums.WorkOrderState.Completed).IsError);
        Guid? invoiceId = null;

        _context.Customers.Add(customer);
        _context.RepairTasks.Add(repairTask);
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var issueResponse = await _client.PostAsJsonAsync<object?>(
                $"/api/v1.0/invoices/workorders/{workOrder.Id}",
                null,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
            var issued = await issueResponse.Content.ReadFromJsonAsync<InvoiceDto>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(issued);
            invoiceId = issued!.InvoiceId;

            var pdfResponse = await _client.GetAsync(
                $"/api/v1.0/invoices/{invoiceId}/pdf",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
            Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);
            Assert.EndsWith(".pdf", pdfResponse.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
            var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
            Assert.True(pdfBytes.Length > 1_000);
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdfBytes, 0, 5));
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
            await _context.RepairTasks.Where(item => item.Id == repairTask.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await _context.Vehicles.Where(item => item.CustomerId == customer.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await _context.Customers.Where(item => item.Id == customer.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

[Fact]
    public async Task InvoiceEndpoints_WithoutManagerRole_ShouldReturnForbidden()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Labor01, TestContext.Current.CancellationToken);
        _client.SetAuthorizationHeader(token);
        var invoiceId = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();

        var issueResponse = await _client.PostAsJsonAsync<object?>(
            $"/api/v1.0/invoices/workorders/{workOrderId}", null, TestContext.Current.CancellationToken);
        var getResponse = await _client.GetAsync(
            $"/api/v1.0/invoices/{invoiceId}", TestContext.Current.CancellationToken);
        var pdfResponse = await _client.GetAsync(
            $"/api/v1.0/invoices/{invoiceId}/pdf", TestContext.Current.CancellationToken);
        var settleResponse = await _client.PutAsJsonAsync<object?>(
            $"/api/v1.0/invoices/{invoiceId}/payments", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, issueResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, pdfResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, settleResponse.StatusCode);
    }

[Fact]
    public async Task InvoiceEndpoints_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var invoiceId = Guid.NewGuid();
        var issueResponse = await _client.PostAsJsonAsync<object?>(
            $"/api/v1.0/invoices/workorders/{Guid.NewGuid()}", null, TestContext.Current.CancellationToken);
        var getResponse = await _client.GetAsync(
            $"/api/v1.0/invoices/{invoiceId}", TestContext.Current.CancellationToken);
        var pdfResponse = await _client.GetAsync(
            $"/api/v1.0/invoices/{invoiceId}/pdf", TestContext.Current.CancellationToken);
        var settleResponse = await _client.PutAsJsonAsync<object?>(
            $"/api/v1.0/invoices/{invoiceId}/payments", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, issueResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, pdfResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, settleResponse.StatusCode);
    }


    private async Task AuthenticateAsManagerAsync()
    {
        var token = await _client.GenerateTokenAsync(
            TestUsers.Manager,
            TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);
    }
}

