using System.Net;
using System.Net.Http.Json;
using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Contracts.Requests.Employees;
using MechanicShop.Domain.Branches;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;
using MechanicShop.Infrastructure.Date;
using MechanicShop.Infrastructure.identity;
using MechanicShop.Tests.Common.Branches;
using MechanicShop.Tests.Common.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public sealed class EmployeesControllerTests : IDisposable
{
    private readonly AppHttpClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;

    public EmployeesControllerTests(WebAppFactory factory)
    {
        _client = factory.CreateAppHttpClient();
        _scope = factory.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _client.Dispose();
    }

    [Fact]
    public async Task CreateLabor_CompanyManager_ShouldCreateIdentityEmployeeAndDefaultMembership()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var email = $"labor.{Guid.NewGuid():N}@localhost";
        const string password = "TestPassword123!";

        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/employees/labors",
            new CreateLaborRequest("New", "Labor", email, password, TestBranchIds.North),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EmployeeAccountDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(email, body!.Email);
        Assert.Equal(Role.Labor, body.Role);
        Assert.Equal(AccessScope.Branch, body.AccessScope);
        Assert.Equal(TestBranchIds.North, body.BranchId);

        var employee = await _context.Employees.AsNoTracking().SingleAsync(
            item => item.Id == body.EmployeeId,
            TestContext.Current.CancellationToken);
        var membership = await _context.EmployeeBranchMemberships.AsNoTracking().SingleAsync(
            item => item.EmployeeId == body.EmployeeId,
            TestContext.Current.CancellationToken);
        var user = await _userManager.FindByEmailAsync(email);

        Assert.Equal(Role.Labor, employee.Role);
        Assert.Equal(AccessScope.Branch, employee.AccessScope);
        Assert.Equal(TestBranchIds.North, membership.BranchId);
        Assert.True(membership.IsDefault);
        Assert.NotNull(user);
        Assert.Equal(body.EmployeeId.ToString(), user!.Id);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.True(await _userManager.CheckPasswordAsync(user, password));

        await DeleteEmployeeAsync(body.EmployeeId, user);
    }

    [Fact]
    public async Task CreateLabor_BranchManager_ShouldOnlyCreateInAuthorizedBranch()
    {
        var manager = await CreateBranchManagerAsync(TestBranchIds.Main);
        try
        {
            await AuthenticateAsync(manager.User);
            var request = new CreateLaborRequest(
                "Branch", "Labor", $"labor.{Guid.NewGuid():N}@localhost", "TestPassword123!", TestBranchIds.North);

            var forbidden = await _client.PostAsJsonAsync(
                "/api/v1.0/employees/labors", request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            Assert.Null(await _userManager.FindByEmailAsync(request.Email));

            var allowedRequest = request with { BranchId = TestBranchIds.Main };
            var created = await _client.PostAsJsonAsync(
                "/api/v1.0/employees/labors", allowedRequest, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var body = await created.Content.ReadFromJsonAsync<EmployeeAccountDto>(TestContext.Current.CancellationToken);
            Assert.NotNull(body);
            Assert.Equal(manager.EmployeeId, (await _context.Employees.AsNoTracking().SingleAsync(
                item => item.Id == manager.EmployeeId,
                TestContext.Current.CancellationToken)).Id);
            Assert.True(await _context.EmployeeBranchMemberships.AnyAsync(
                item => item.EmployeeId == body!.EmployeeId && item.BranchId == TestBranchIds.Main && item.IsDefault,
                TestContext.Current.CancellationToken));
            await DeleteEmployeeAsync(body!.EmployeeId, await _userManager.FindByEmailAsync(allowedRequest.Email));
        }
        finally
        {
            await DeleteEmployeeAsync(manager.EmployeeId, manager.User);
        }
    }

    [Fact]
    public async Task CreateLabor_LaborUser_ShouldBeForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);
        var request = new CreateLaborRequest(
            "Blocked", "Labor", $"labor.{Guid.NewGuid():N}@localhost", "TestPassword123!", TestBranchIds.Main);

        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/employees/labors", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(await _userManager.FindByEmailAsync(request.Email));
    }

    [Fact]
    public async Task CreateLabor_InactiveBranch_ShouldBeRejected()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var branch = await _context.Branches.SingleAsync(
            item => item.Id == TestBranchIds.North,
            TestContext.Current.CancellationToken);
        branch.SetActive(false);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var email = $"inactive.{Guid.NewGuid():N}@localhost";
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1.0/employees/labors",
                new CreateLaborRequest("Inactive", "Labor", email, "TestPassword123!", TestBranchIds.North),
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Null(await _userManager.FindByEmailAsync(email));
        }
        finally
        {
            branch.SetActive(true);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task CreateLabor_DuplicateEmail_ShouldReturnConflictWithoutCreatingAnotherEmployee()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var request = new CreateLaborRequest(
            "Duplicate", "Labor", $"duplicate.{Guid.NewGuid():N}@localhost", "TestPassword123!", TestBranchIds.Main);
        var first = await _client.PostAsJsonAsync(
            "/api/v1.0/employees/labors", request, TestContext.Current.CancellationToken);
        var body = await first.Content.ReadFromJsonAsync<EmployeeAccountDto>(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.NotNull(body);

        try
        {
            var duplicate = await _client.PostAsJsonAsync(
                "/api/v1.0/employees/labors", request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Equal(1, await _context.Employees.CountAsync(
                item => item.Id == body!.EmployeeId,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await DeleteEmployeeAsync(body!.EmployeeId, await _userManager.FindByEmailAsync(request.Email));
        }
    }

    [Fact]
    public async Task CreateBranchManager_CompanyManager_ShouldCreateDefaultBranchMembership()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var email = $"branch.manager.{Guid.NewGuid():N}@localhost";
        const string password = "ManagerPassword123!";

        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/employees/branch-managers",
            new CreateBranchManagerRequest("Branch", "Manager", email, password, TestBranchIds.North),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EmployeeAccountDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        var employee = await _context.Employees.AsNoTracking().SingleAsync(
            item => item.Id == body!.EmployeeId,
            TestContext.Current.CancellationToken);
        var membership = await _context.EmployeeBranchMemberships.AsNoTracking().SingleAsync(
            item => item.EmployeeId == body.EmployeeId,
            TestContext.Current.CancellationToken);
        var user = await _userManager.FindByEmailAsync(email);

        Assert.Equal(Role.Manager, employee.Role);
        Assert.Equal(AccessScope.Branch, employee.AccessScope);
        Assert.Equal(TestBranchIds.North, membership.BranchId);
        Assert.True(membership.IsDefault);
        Assert.NotNull(user);
        Assert.Equal(body.EmployeeId.ToString(), user!.Id);
        Assert.True(await _userManager.CheckPasswordAsync(user, password));

        await DeleteEmployeeAsync(body.EmployeeId, user);
    }

    [Fact]
    public async Task CreateBranchManager_BranchManager_ShouldBeForbidden()
    {
        var manager = await CreateBranchManagerAsync(TestBranchIds.Main);
        try
        {
            await AuthenticateAsync(manager.User);
            var request = new CreateBranchManagerRequest(
                "Blocked", "Manager", $"manager.{Guid.NewGuid():N}@localhost", "ManagerPassword123!", TestBranchIds.Main);

            var response = await _client.PostAsJsonAsync(
                "/api/v1.0/employees/branch-managers", request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Null(await _userManager.FindByEmailAsync(request.Email));
        }
        finally
        {
            await DeleteEmployeeAsync(manager.EmployeeId, manager.User);
        }
    }

    [Fact]
    public async Task CreateBranchManager_Labor_ShouldBeForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);
        var request = new CreateBranchManagerRequest(
            "Blocked", "Manager", $"manager.{Guid.NewGuid():N}@localhost", "ManagerPassword123!", TestBranchIds.Main);

        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/employees/branch-managers", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(await _userManager.FindByEmailAsync(request.Email));
    }

    [Fact]
    public async Task CreateBranchManager_InactiveBranch_ShouldBeRejected()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var branch = await _context.Branches.SingleAsync(
            item => item.Id == TestBranchIds.North,
            TestContext.Current.CancellationToken);
        branch.SetActive(false);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var email = $"inactive.manager.{Guid.NewGuid():N}@localhost";

        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1.0/employees/branch-managers",
                new CreateBranchManagerRequest("Inactive", "Manager", email, "ManagerPassword123!", TestBranchIds.North),
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Null(await _userManager.FindByEmailAsync(email));
        }
        finally
        {
            branch.SetActive(true);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task CreateBranchManager_DuplicateEmail_ShouldReturnConflict()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var request = new CreateBranchManagerRequest(
            "Duplicate", "Manager", $"duplicate.manager.{Guid.NewGuid():N}@localhost", "ManagerPassword123!", TestBranchIds.Main);
        var first = await _client.PostAsJsonAsync(
            "/api/v1.0/employees/branch-managers", request, TestContext.Current.CancellationToken);
        var body = await first.Content.ReadFromJsonAsync<EmployeeAccountDto>(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.NotNull(body);

        try
        {
            var duplicate = await _client.PostAsJsonAsync(
                "/api/v1.0/employees/branch-managers", request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Equal(1, await _context.Employees.CountAsync(
                employee => employee.Id == body!.EmployeeId,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await DeleteEmployeeAsync(body!.EmployeeId, await _userManager.FindByEmailAsync(request.Email));
        }
    }

    private async Task<(Guid EmployeeId, AppUser User)> CreateBranchManagerAsync(Guid branchId)
    {
        var employeeId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = employeeId.ToString(),
            Email = $"branch.manager.{employeeId:N}@localhost",
            UserName = $"branch.manager.{employeeId:N}@localhost",
            EmailConfirmed = true
        };
        Assert.True((await _userManager.CreateAsync(user, user.Email!)).Succeeded);
        Assert.True((await _userManager.AddToRoleAsync(user, nameof(Role.Manager))).Succeeded);
        _context.Employees.Add(Employee.Create(employeeId, "Branch", "Manager", Role.Manager, AccessScope.Branch).Value);
        _context.EmployeeBranchMemberships.Add(EmployeeBranchMembership.Create(employeeId, branchId, true).Value);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (employeeId, user);
    }

    private async Task DeleteEmployeeAsync(Guid employeeId, AppUser? user)
    {
        await _context.EmployeeBranchMemberships
            .Where(membership => membership.EmployeeId == employeeId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await _context.Employees.Where(employee => employee.Id == employeeId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        if (user is not null)
        {
            await _userManager.DeleteAsync(user);
        }
    }

    private async Task AuthenticateAsync(AppUser user)
    {
        var token = await _client.GenerateTokenAsync(user, TestContext.Current.CancellationToken);
        _client.SetAuthorizationHeader(token);
    }
}
