using System.Net;
using System.Net.Http.Json;
using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Application.Features.Branches.Dtos;
using MechanicShop.Contracts.Requests.Branches;
using MechanicShop.Domain.Branches;
using MechanicShop.Infrastructure.Date;
using MechanicShop.Infrastructure.identity;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;
using MechanicShop.Tests.Common.Branches;
using MechanicShop.Tests.Common.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public sealed class BranchesControllerTests : IDisposable
{
    private readonly AppHttpClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _context;

    public BranchesControllerTests(WebAppFactory factory)
    {
        _client = factory.CreateAppHttpClient();
        _scope = factory.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _client.Dispose();
    }

    [Fact]
    public async Task GetBranches_CompanyManager_ShouldReturnAllBranches()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await _client.GetAsync("/api/v1.0/branches", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var branches = await response.Content.ReadFromJsonAsync<List<BranchDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(branches);
        Assert.Contains(branches!, branch => branch.BranchId == TestBranchIds.Main);
        Assert.Contains(branches!, branch => branch.BranchId == TestBranchIds.North);
    }

    [Fact]
    public async Task GetBranches_Employee_ShouldReturnOnlyAssignedBranches()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await _client.GetAsync("/api/v1.0/branches", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var branches = await response.Content.ReadFromJsonAsync<List<BranchDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(branches);
        Assert.Equal(2, branches!.Count);
        Assert.All(branches, branch => Assert.Contains(branch.BranchId, new[] { TestBranchIds.Main, TestBranchIds.North }));
    }

    [Fact]
    public async Task CreateBranch_CompanyManager_ShouldPersistBranch()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var request = new CreateBranchRequest { Name = "API Branch", Code = "API-TEST" };

        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/branches",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var branch = await response.Content.ReadFromJsonAsync<BranchDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(branch);
        Assert.Equal("API-TEST", branch!.Code);
        Assert.True(await _context.Branches.AnyAsync(
            item => item.Id == branch.BranchId,
            TestContext.Current.CancellationToken));

        _context.Branches.Remove(await _context.Branches.SingleAsync(
            item => item.Id == branch.BranchId,
            TestContext.Current.CancellationToken));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateBranch_BranchEmployee_ShouldBeForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/branches",
            new CreateBranchRequest { Name = "Unauthorized", Code = "NO-CREATE" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetDefaultBranch_ShouldChangeAndRestoreTheEmployeeDefault()
    {
        await AuthenticateAsync(TestUsers.Labor01);
        var request = new SetDefaultBranchRequest { BranchId = TestBranchIds.North };

        var changeResponse = await _client.PutAsJsonAsync(
            "/api/v1.0/branches/me/default",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);
        Assert.True(await _context.EmployeeBranchMemberships.AnyAsync(
            membership => membership.EmployeeId == Guid.Parse(TestUsers.Labor01.Id)
                && membership.BranchId == TestBranchIds.North
                && membership.IsDefault,
            TestContext.Current.CancellationToken));

        var restoreResponse = await _client.PutAsJsonAsync(
            "/api/v1.0/branches/me/default",
            new SetDefaultBranchRequest { BranchId = TestBranchIds.Main },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, restoreResponse.StatusCode);
    }

    [Fact]
    public async Task AssignEmployeeToBranch_WhenMembershipAlreadyExists_ShouldReturnConflict()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await _client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/v1.0/branches/{TestBranchIds.Main}/employees/{TestUsers.Labor01.Id}"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateBranch_WhenItContainsDefaultMemberships_ShouldReturnConflict()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/branches/{TestBranchIds.Main}/active",
            new SetBranchActiveRequest { IsActive = false },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(await _context.Branches.AnyAsync(
            branch => branch.Id == TestBranchIds.Main && branch.IsActive,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetBranchActive_CompanyManager_ShouldActivateAndDeactivateBranchWithoutDefaultMembers()
    {
        var branch = MechanicShop.Domain.Branches.Branch.Create(
            Guid.NewGuid(), "Toggle Branch", $"T{Guid.NewGuid():N}"[..20]).Value;
        branch.SetActive(false);
        _context.Branches.Add(branch);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            await AuthenticateAsync(TestUsers.Manager);

            var activateResponse = await _client.PutAsJsonAsync(
                $"/api/v1.0/branches/{branch.Id}/active",
                new SetBranchActiveRequest { IsActive = true },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, activateResponse.StatusCode);
            Assert.True(await _context.Branches.Where(item => item.Id == branch.Id)
                .Select(item => item.IsActive).SingleAsync(TestContext.Current.CancellationToken));

            var deactivateResponse = await _client.PutAsJsonAsync(
                $"/api/v1.0/branches/{branch.Id}/active",
                new SetBranchActiveRequest { IsActive = false },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);
            Assert.False(await _context.Branches.Where(item => item.Id == branch.Id)
                .Select(item => item.IsActive).SingleAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task CompanyManagerCannotGrantCompanyScopeToLaborEmployee()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/branches/employees/{TestUsers.Labor01.Id}/access-scope",
            new SetEmployeeAccessScopeRequest
            {
                AccessScope = MechanicShop.Domain.Identity.AccessScope.Company
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddEmployeeToBranch_Labor_ShouldBeForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await _client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/v1.0/branches/{TestBranchIds.Main}/employees/{Guid.NewGuid()}"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CompanyManager_BranchMembershipAndAccessScopeOperations_ShouldRejectMissingRecords()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var missingEmployeeResponse = await _client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/v1.0/branches/{TestBranchIds.Main}/employees/{Guid.NewGuid()}"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingEmployeeResponse.StatusCode);

        var missingBranchResponse = await _client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/v1.0/branches/{Guid.NewGuid()}/employees/{TestUsers.Labor01.Id}"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingBranchResponse.StatusCode);

        var missingScopeTargetResponse = await _client.PutAsJsonAsync(
            $"/api/v1.0/branches/employees/{Guid.NewGuid()}/access-scope",
            new SetEmployeeAccessScopeRequest { AccessScope = AccessScope.Branch },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingScopeTargetResponse.StatusCode);
    }

    [Fact]
    public async Task AddEmployeeToBranch_BranchManager_ShouldAllowMemberBranchAndRejectOtherBranch()
    {
        var manager = await CreateBranchManagerAsync(TestBranchIds.Main);
        var authorizedEmployeeId = await CreateLaborEmployeeAsync();
        var unauthorizedEmployeeId = await CreateLaborEmployeeAsync();

        try
        {
            await AuthenticateAsync(manager.User);

            var allowedResponse = await _client.SendAsync(
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"/api/v1.0/branches/{TestBranchIds.Main}/employees/{authorizedEmployeeId}"),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, allowedResponse.StatusCode);
            Assert.True(await _context.EmployeeBranchMemberships.AnyAsync(
                membership => membership.EmployeeId == authorizedEmployeeId
                    && membership.BranchId == TestBranchIds.Main
                    && membership.IsDefault,
                TestContext.Current.CancellationToken));

            var deniedResponse = await _client.SendAsync(
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"/api/v1.0/branches/{TestBranchIds.North}/employees/{unauthorizedEmployeeId}"),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
        }
        finally
        {
            await DeleteEmployeeAsync(authorizedEmployeeId);
            await DeleteEmployeeAsync(unauthorizedEmployeeId);
            await DeleteEmployeeAsync(manager.EmployeeId, manager.User);
        }
    }

    [Fact]
    public async Task AddEmployeeToBranch_CompanyManager_ShouldAllowActiveBranchAndRejectInactiveBranch()
    {
        var inactiveBranch = MechanicShop.Domain.Branches.Branch.Create(
            Guid.NewGuid(), "Inactive Membership Branch", $"I{Guid.NewGuid():N}"[..20]).Value;
        inactiveBranch.SetActive(false);
        _context.Branches.Add(inactiveBranch);
        var activeTargetEmployeeId = await CreateLaborEmployeeAsync();
        var inactiveTargetEmployeeId = await CreateLaborEmployeeAsync();
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            await AuthenticateAsync(TestUsers.Manager);
            var activeResponse = await _client.SendAsync(
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"/api/v1.0/branches/{TestBranchIds.North}/employees/{activeTargetEmployeeId}"),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, activeResponse.StatusCode);

            var inactiveResponse = await _client.SendAsync(
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"/api/v1.0/branches/{inactiveBranch.Id}/employees/{inactiveTargetEmployeeId}"),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, inactiveResponse.StatusCode);
            Assert.False(await _context.EmployeeBranchMemberships.AnyAsync(
                membership => membership.EmployeeId == inactiveTargetEmployeeId
                    && membership.BranchId == inactiveBranch.Id,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await DeleteEmployeeAsync(activeTargetEmployeeId);
            await DeleteEmployeeAsync(inactiveTargetEmployeeId);
            _context.Branches.Remove(inactiveBranch);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task BranchManager_CannotCreateDeactivateOrChangeAccessScope()
    {
        var manager = await CreateBranchManagerAsync(TestBranchIds.Main);
        var targetManager = await CreateBranchManagerAsync(TestBranchIds.Main);

        try
        {
            await AuthenticateAsync(manager.User);

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1.0/branches",
                new CreateBranchRequest { Name = "Branch Manager Attempt", Code = "BR-MGR-1" },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

            var activeResponse = await _client.PutAsJsonAsync(
                $"/api/v1.0/branches/{TestBranchIds.Main}/active",
                new SetBranchActiveRequest { IsActive = false },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, activeResponse.StatusCode);

            var targetScopeResponse = await _client.PutAsJsonAsync(
                $"/api/v1.0/branches/employees/{targetManager.EmployeeId}/access-scope",
                new SetEmployeeAccessScopeRequest { AccessScope = AccessScope.Company },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, targetScopeResponse.StatusCode);

            var selfScopeResponse = await _client.PutAsJsonAsync(
                $"/api/v1.0/branches/employees/{manager.EmployeeId}/access-scope",
                new SetEmployeeAccessScopeRequest { AccessScope = AccessScope.Company },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, selfScopeResponse.StatusCode);

            Assert.Equal(AccessScope.Branch, await _context.Employees
                .Where(employee => employee.Id == manager.EmployeeId)
                .Select(employee => employee.AccessScope)
                .SingleAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await DeleteEmployeeAsync(manager.EmployeeId, manager.User);
            await DeleteEmployeeAsync(targetManager.EmployeeId, targetManager.User);
        }
    }

    [Fact]
    public async Task CompanyManager_CanChangeAccessScopeOfAnotherManager()
    {
        var targetManager = await CreateBranchManagerAsync(TestBranchIds.Main);

        try
        {
            await AuthenticateAsync(TestUsers.Manager);
            var response = await _client.PutAsJsonAsync(
                $"/api/v1.0/branches/employees/{targetManager.EmployeeId}/access-scope",
                new SetEmployeeAccessScopeRequest { AccessScope = AccessScope.Company },
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(AccessScope.Company, await _context.Employees
                .Where(employee => employee.Id == targetManager.EmployeeId)
                .Select(employee => employee.AccessScope)
                .SingleAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await DeleteEmployeeAsync(targetManager.EmployeeId, targetManager.User);
        }
    }

    private async Task<(Guid EmployeeId, AppUser User)> CreateBranchManagerAsync(Guid branchId)
    {
        var userManager = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var employeeId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = employeeId.ToString(),
            Email = $"branch.manager.{employeeId:N}@localhost",
            UserName = $"branch.manager.{employeeId:N}@localhost",
            EmailConfirmed = true
        };
        Assert.True((await userManager.CreateAsync(user, user.Email!)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, nameof(Role.Manager))).Succeeded);
        _context.Employees.Add(Employee.Create(
            employeeId, "Branch", "Manager", Role.Manager, AccessScope.Branch).Value);
        _context.EmployeeBranchMemberships.Add(
            EmployeeBranchMembership.Create(employeeId, branchId, true).Value);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (employeeId, user);
    }

    private async Task<Guid> CreateLaborEmployeeAsync()
    {
        var employeeId = Guid.NewGuid();
        _context.Employees.Add(Employee.Create(employeeId, "Target", "Labor", Role.Labor).Value);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return employeeId;
    }

    private async Task DeleteEmployeeAsync(Guid employeeId, AppUser? user = null)
    {
        await _context.EmployeeBranchMemberships
            .Where(membership => membership.EmployeeId == employeeId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await _context.Employees.Where(employee => employee.Id == employeeId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        if (user is not null)
        {
            var userManager = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await userManager.DeleteAsync(user);
        }
    }

    private async Task AuthenticateAsync(MechanicShop.Infrastructure.identity.AppUser user)
    {
        var token = await _client.GenerateTokenAsync(user, TestContext.Current.CancellationToken);
        _client.SetAuthorizationHeader(token);
    }
}
