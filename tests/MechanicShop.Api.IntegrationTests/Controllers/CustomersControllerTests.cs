using System.Net;
using System.Net.Http.Json;

using MechanicShop.Api.IntegrationTests.Common;
using Xunit;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Contracts.Requests.Customers;
using MechanicShop.Tests.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class CustomersControllerTests : IDisposable
{
    private readonly AppHttpClient _client;
    private readonly IServiceScope _scope;
    private readonly IAppDbContext _context;

    public CustomersControllerTests(WebAppFactory webAppFactory)
    {
        _client = webAppFactory.CreateAppHttpClient();
        _scope = webAppFactory.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<IAppDbContext>();
    }

    public void Dispose() => _scope.Dispose();

[Fact]
    public async Task GetCustomers_WhenAuthenticated_ShouldReturnPersistedCustomers()
    {
        await AuthenticateAsManagerAsync();

        var expectedCustomer = await _context.Customers
            .OrderBy(customer => customer.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var response = await _client.GetAsync(
            "/api/v1.0/customers",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<CustomerDto>>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Contains(result!, customer => customer.CustomerId == expectedCustomer.Id);
    }

[Fact]
    public async Task GetCustomerById_WhenCustomerExists_ShouldReturnMatchingCustomer()
    {
        await AuthenticateAsManagerAsync();

        var expectedCustomer = await _context.Customers
            .OrderBy(customer => customer.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var response = await _client.GetAsync(
            $"/api/v1.0/customers/{expectedCustomer.Id}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CustomerDto>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(expectedCustomer.Id, result!.CustomerId);
        Assert.Equal(expectedCustomer.Name, result.Name);
    }

[Fact]
    public async Task GetCustomerById_WhenCustomerDoesNotExist_ShouldReturnNotFound()
    {
        await AuthenticateAsAsync(TestUsers.Manager);

        var response = await _client.GetAsync(
            $"/api/v1.0/customers/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

[Fact]
    public async Task GetCustomerById_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(
            $"/api/v1.0/customers/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

[Fact]
    public async Task GetCustomers_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(
            "/api/v1.0/customers",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

[Fact]
    public async Task CreateCustomer_WhenRequestIsEmpty_ShouldReturnBadRequest()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/customers",
            new CreateCustomerRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

[Fact]
    public async Task UpdateCustomer_WhenRequestIsEmpty_ShouldReturnBadRequest()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/customers/{Guid.NewGuid()}",
            new UpdateCustomerRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

[Fact]
    public async Task DeleteCustomer_WhenCustomerDoesNotExist_ShouldReturnNotFound()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.DeleteAsync(
            $"/api/v1.0/customers/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

[Fact]
    public async Task CustomerEndpoints_WhenManagerCreatesUpdatesAndDeletes_ShouldPersistEachChange()
    {
        await AuthenticateAsManagerAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var phoneNumber = $"+20{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";
        Guid? customerId = null;

        try
        {
            var createRequest = new CreateCustomerRequest
            {
                Name = $"Customer {suffix[..8]}",
                PhoneNumber = phoneNumber,
                Email = $"c-{suffix[..8]}@example.test",
                Vehicles =
                [
                    new CreateVehicleRequest
                    {
                        Make = "Toyota",
                        Model = "Corolla",
                        Year = 2022,
                        LicensePlate = $"C{suffix[..7]}"
                    }
                ]
            };

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1.0/customers",
                createRequest,
                TestContext.Current.CancellationToken);

            Assert.True(
                createResponse.StatusCode == HttpStatusCode.Created,
                await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(created);
            customerId = created!.CustomerId;
            var createdVehicle = Assert.Single(created.Vehicles);

            var updateRequest = new UpdateCustomerRequest
            {
                Name = $"Updated {suffix[..8]}",
                PhoneNumber = $"+20{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}",
                Email = $"u-{suffix[..8]}@example.test",
                Vehicles =
                [
                    new UpdateVehicleRequest
                    {
                        VehicleId = createdVehicle.VehicleId,
                        Make = "Honda",
                        Model = "Civic",
                        Year = 2024,
                        LicensePlate = $"U{suffix[..7]}"
                    }
                ]
            };

            var updateResponse = await _client.PutAsJsonAsync(
                $"/api/v1.0/customers/{customerId}",
                updateRequest,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);
            Assert.Empty(await updateResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            var updateMetadata = typeof(global::CustomersController)
                .GetMethod(nameof(global::CustomersController.Update))!
                .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: false)
                .Cast<ProducesResponseTypeAttribute>();
            Assert.Contains(updateMetadata, metadata =>
                metadata.StatusCode == StatusCodes.Status204NoContent && metadata.Type == typeof(void));

            var persisted = await _context.Customers.AsNoTracking()
                .Include(customer => customer.Vehicles)
                .SingleAsync(customer => customer.Id == customerId, TestContext.Current.CancellationToken);
            Assert.Equal(updateRequest.PhoneNumber, persisted.PhoneNumber);
            Assert.Equal("Civic", Assert.Single(persisted.Vehicles).Model);

            var deleteResponse = await _client.DeleteAsync(
                $"/api/v1.0/customers/{customerId}",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            Assert.False(await _context.Customers.AnyAsync(
                customer => customer.Id == customerId,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            if (customerId.HasValue)
            {
                await _context.Vehicles.Where(vehicle => vehicle.CustomerId == customerId.Value)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
                await _context.Customers.Where(customer => customer.Id == customerId.Value)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }
    }

[Fact]
    public async Task CreateCustomer_WhenEmailAlreadyExists_ShouldReturnConflict()
    {
        await AuthenticateAsAsync(TestUsers.Manager);

        var existing = await _context.Customers.Where(customer => customer.Email != null)
            .OrderBy(customer => customer.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var suffix = Guid.NewGuid().ToString("N");
        var request = new CreateCustomerRequest
        {
            Name = $"Duplicate {suffix[..8]}",
            PhoneNumber = $"+20{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}",
            Email = existing.Email!,
            Vehicles =
            [
                new CreateVehicleRequest
                {
                    Make = "Toyota",
                    Model = "Corolla",
                    Year = 2022,
                    LicensePlate = $"D{suffix[..7]}"
                }
            ]
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/customers",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

[Fact]
    public async Task CustomerMutationEndpoints_AsLabor_ShouldReturnForbiddenWithoutChangingCustomer()
    {
        await AuthenticateAsAsync(TestUsers.Labor01);

        var customer = await _context.Customers.Include(item => item.Vehicles)
            .OrderBy(item => item.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var existingName = customer.Name;
        var suffix = Guid.NewGuid().ToString("N");

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1.0/customers",
            new CreateCustomerRequest
            {
                Name = $"Denied {suffix[..8]}",
                PhoneNumber = $"+20{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}",
                Email = $"denied-{suffix[..8]}@example.test",
                Vehicles = []
            },
            TestContext.Current.CancellationToken);
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1.0/customers/{customer.Id}",
            new UpdateCustomerRequest
            {
                Name = "Unauthorized update",
                PhoneNumber = "+201234567890",
                Email = $"denied-{suffix[..8]}@example.test",
                Vehicles = []
            },
            TestContext.Current.CancellationToken);
        var deleteResponse = await _client.DeleteAsync(
            $"/api/v1.0/customers/{customer.Id}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        var unchanged = await _context.Customers.AsNoTracking()
            .SingleAsync(item => item.Id == customer.Id, TestContext.Current.CancellationToken);
        Assert.Equal(existingName, unchanged.Name);
    }


    private async Task AuthenticateAsManagerAsync()
    {
        await AuthenticateAsAsync(TestUsers.Manager);
    }

    private async Task AuthenticateAsAsync(MechanicShop.Infrastructure.identity.AppUser user)
    {
        var token = await _client.GenerateTokenAsync(user, TestContext.Current.CancellationToken);
        _client.SetAuthorizationHeader(token);
    }
}
