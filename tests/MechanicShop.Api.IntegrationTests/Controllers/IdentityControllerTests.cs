using System.Net;
using System.Net.Http.Json;

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity;
using MechanicShop.Api.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using MechanicShop.Tests.Common.Security;
using MechanicShop.Domain.Branches;
using MechanicShop.Infrastructure.Date;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;
using MechanicShop.Infrastructure.identity;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class IdentityControllerTests(WebAppFactory webAppFactory)
{
    private readonly AppHttpClient _client = webAppFactory.CreateAppHttpClient();

    [Fact]
    public async Task Authorize_WhenPolicyNameIsNull_ShouldDenyAccess()
    {
        using var scope = webAppFactory.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        var authorized = await identityService.AuthorizeAsync(TestUsers.Manager.Id, null);

        Assert.False(authorized);
    }

[Fact]
    public async Task GetCurrentUserClaims_WhenAuthenticated_ShouldReturnManagerIdentity()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.GetAsync(
            "/identity/current-user/claims",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(TestUsers.Manager.Id, body);
    }

[Fact]
    public async Task GetCurrentUserClaims_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(
            "/identity/current-user/claims",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

[Fact]
    public async Task RefreshToken_WhenTokensAreInvalid_ShouldReturnConflict()
    {
        var response = await _client.PostAsJsonAsync(
            "/identity/token/refresh-token",
            new { RefreshToken = "invalid-refresh-token", ExpiredAccessToken = "invalid-access-token" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

[Fact]
    public async Task GenerateToken_WhenCredentialsAreInvalid_ShouldReturnConflict()
    {
        var response = await _client.PostAsJsonAsync(
            "/identity/token/generate",
            new { Email = TestUsers.Manager.Email, Password = "incorrect-password" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

[Fact]
    public async Task GenerateToken_WhenCredentialsAreEmpty_ShouldReturnBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/identity/token/generate",
            new { Email = string.Empty, Password = string.Empty },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

[Fact]
    public async Task GetCurrentUserClaims_WhenLaborIsAuthenticated_ShouldReturnLaborIdentity()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Labor01, TestContext.Current.CancellationToken);
        _client.SetAuthorizationHeader(token);

        var response = await _client.GetAsync(
            "/identity/current-user/claims",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(TestUsers.Labor01.Id, body);
        Assert.Contains("Labor", body);
    }

    [Fact]
    public async Task GenerateToken_CompanyManagerWithoutMembership_ShouldSucceedWithoutBranchClaim()
    {
        using var scope = webAppFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var managerId = Guid.Parse(TestUsers.Manager.Id);
        var memberships = await context.EmployeeBranchMemberships
            .Where(membership => membership.EmployeeId == managerId)
            .ToListAsync(TestContext.Current.CancellationToken);
        context.EmployeeBranchMemberships.RemoveRange(memberships);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var response = await _client.PostAsJsonAsync(
                "/identity/token/generate",
                new { Email = TestUsers.Manager.Email, Password = TestUsers.Manager.Email },
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(TestContext.Current.CancellationToken);
            Assert.NotNull(tokenResponse?.AccessToken);
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
                .ReadJwtToken(tokenResponse!.AccessToken);
            Assert.Equal("Company", jwt.Claims.Single(claim => claim.Type == "access_scope").Value);
            Assert.DoesNotContain(jwt.Claims, claim => claim.Type == "branch_id");

            _client.SetAuthorizationHeader(tokenResponse.AccessToken!);
            var branchesResponse = await _client.GetAsync(
                "/api/v1.0/branches",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, branchesResponse.StatusCode);
        }
        finally
        {
            context.EmployeeBranchMemberships.AddRange(memberships);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GenerateToken_BranchScopedLabor_ShouldUseActiveDefaultBranchWithoutLoginBranchId()
    {
        var response = await _client.PostAsJsonAsync(
            "/identity/token/generate",
            new { Email = TestUsers.Labor01.Email, Password = TestUsers.Labor01.Email },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(tokenResponse?.AccessToken);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(tokenResponse!.AccessToken);
        Assert.Equal(
            MechanicShop.Tests.Common.Branches.TestBranchIds.Main.ToString(),
            jwt.Claims.Single(claim => claim.Type == "branch_id").Value);
        Assert.Equal("Branch", jwt.Claims.Single(claim => claim.Type == "access_scope").Value);
    }

    [Fact]
    public async Task GenerateToken_BranchManager_ShouldUseDefaultMembershipWithoutLoginBranchId()
    {
        using var scope = webAppFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId.ToString(),
            Email = $"branch.manager.{userId:N}@localhost",
            UserName = $"branch.manager.{userId:N}@localhost",
            EmailConfirmed = true
        };
        Assert.True((await userManager.CreateAsync(user, user.Email!)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, nameof(Role.Manager))).Succeeded);
        context.Employees.Add(Employee.Create(userId, "Branch", "Manager", Role.Manager, AccessScope.Branch).Value);
        context.EmployeeBranchMemberships.Add(
            EmployeeBranchMembership.Create(userId, MechanicShop.Tests.Common.Branches.TestBranchIds.North, true).Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var response = await _client.PostAsJsonAsync(
                "/identity/token/generate",
                new { Email = user.Email, Password = user.Email },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(TestContext.Current.CancellationToken);
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
                .ReadJwtToken(tokenResponse!.AccessToken);
            Assert.Equal(
                MechanicShop.Tests.Common.Branches.TestBranchIds.North.ToString(),
                jwt.Claims.Single(claim => claim.Type == "branch_id").Value);
            Assert.Equal("Branch", jwt.Claims.Single(claim => claim.Type == "access_scope").Value);

            _client.SetAuthorizationHeader(tokenResponse.AccessToken!);
            var allowedBranch = await _client.GetAsync(
                $"/api/v1.0/branches/{MechanicShop.Tests.Common.Branches.TestBranchIds.North}",
                TestContext.Current.CancellationToken);
            var unauthorizedBranch = await _client.GetAsync(
                $"/api/v1.0/branches/{MechanicShop.Tests.Common.Branches.TestBranchIds.Main}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedBranch.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, unauthorizedBranch.StatusCode);
        }
        finally
        {
            await context.EmployeeBranchMemberships.Where(item => item.EmployeeId == userId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await context.Employees.Where(item => item.Id == userId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await userManager.DeleteAsync(user);
        }
    }

    [Fact]
    public async Task GenerateToken_BranchScopedUserWithoutDefaultMembership_ShouldBeRejected()
    {
        using var scope = webAppFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId.ToString(),
            Email = $"no.default.{userId:N}@localhost",
            UserName = $"no.default.{userId:N}@localhost",
            EmailConfirmed = true
        };
        Assert.True((await userManager.CreateAsync(user, user.Email!)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, nameof(Role.Labor))).Succeeded);
        context.Employees.Add(Employee.Create(userId, "No", "Default", Role.Labor).Value);
        context.EmployeeBranchMemberships.Add(EmployeeBranchMembership.Create(
            userId, MechanicShop.Tests.Common.Branches.TestBranchIds.Main, false).Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var response = await _client.PostAsJsonAsync(
                "/identity/token/generate",
                new { Email = user.Email, Password = user.Email },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await context.EmployeeBranchMemberships.Where(item => item.EmployeeId == userId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await context.Employees.Where(item => item.Id == userId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await userManager.DeleteAsync(user);
        }
    }

    [Fact]
    public async Task GenerateToken_WhenIdentityRoleDoesNotMatchEmployeeRole_ShouldBeRejected()
    {
        using var scope = webAppFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId.ToString(),
            Email = $"role.mismatch.{userId:N}@localhost",
            UserName = $"role.mismatch.{userId:N}@localhost",
            EmailConfirmed = true
        };
        Assert.True((await userManager.CreateAsync(user, user.Email!)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, nameof(Role.Manager))).Succeeded);
        context.Employees.Add(Employee.Create(userId, "Role", "Mismatch", Role.Labor).Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var response = await _client.PostAsJsonAsync(
                "/identity/token/generate",
                new { Email = user.Email, Password = user.Email },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await context.Employees.Where(item => item.Id == userId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await userManager.DeleteAsync(user);
        }
    }

    [Fact]
    public async Task GenerateToken_WhenDefaultBranchIsInactive_ShouldBeRejected()
    {
        using var scope = webAppFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var userId = Guid.NewGuid();
        var branch = Branch.Create(Guid.NewGuid(), "Inactive Login Branch", $"I{userId:N}"[..20]).Value;
        branch.SetActive(false);
        context.Branches.Add(branch);
        var user = new AppUser
        {
            Id = userId.ToString(),
            Email = $"inactive.default.{userId:N}@localhost",
            UserName = $"inactive.default.{userId:N}@localhost",
            EmailConfirmed = true
        };
        Assert.True((await userManager.CreateAsync(user, user.Email!)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, nameof(Role.Labor))).Succeeded);
        context.Employees.Add(Employee.Create(userId, "Inactive", "Default", Role.Labor).Value);
        context.EmployeeBranchMemberships.Add(EmployeeBranchMembership.Create(userId, branch.Id, true).Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var response = await _client.PostAsJsonAsync(
                "/identity/token/generate",
                new { Email = user.Email, Password = user.Email },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await context.EmployeeBranchMemberships.Where(item => item.EmployeeId == userId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await context.Employees.Where(item => item.Id == userId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await context.Branches.Where(item => item.Id == branch.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await userManager.DeleteAsync(user);
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
