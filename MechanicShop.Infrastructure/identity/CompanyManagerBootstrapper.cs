using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;
using MechanicShop.Infrastructure.Date;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Infrastructure.identity;

public sealed class CompanyManagerBootstrapper(
    AppDbContext context,
    UserManager<AppUser> userManager,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<CompanyManagerBootstrapper> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Company Manager bootstrap is only available in Development.");
        }

        var email = configuration["Bootstrap:CompanyManagerEmail"]?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("Company Manager bootstrap was skipped because no bootstrap email is configured.");
            return;
        }

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            await EnsureExistingAccountIsCompanyManagerAsync(existingUser, cancellationToken);
            return;
        }

        var existingCompanyManager = await context.Employees.AnyAsync(
            employee => employee.Role == Role.Manager && employee.AccessScope == AccessScope.Company,
            cancellationToken);
        if (existingCompanyManager)
        {
            logger.LogWarning("Company Manager bootstrap was skipped because a company-scoped manager already exists.");
            return;
        }

        var credentialPath = GetCredentialPath(email);
        var credentials = await ReadOrCreateCredentialsAsync(email, credentialPath, cancellationToken);
        var employeeResult = Employee.Create(
            credentials.EmployeeId,
            "Company",
            "Manager",
            Role.Manager,
            AccessScope.Company);
        if (employeeResult.IsError)
        {
            throw new InvalidOperationException("The initial Company Manager employee could not be created.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var user = new AppUser
        {
            Id = credentials.EmployeeId.ToString(),
            Email = email,
            UserName = email,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, credentials.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(FormatIdentityFailure("create user", createResult));
        }

        var roleResult = await userManager.AddToRoleAsync(user, Role.Manager.ToString());
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(FormatIdentityFailure("assign manager role", roleResult));
        }

        await context.Employees.AddAsync(employeeResult.Value, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Initial Company Manager account and employee record were created.");
    }

    private async Task EnsureExistingAccountIsCompanyManagerAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.Id, out var employeeId))
        {
            throw new InvalidOperationException("The configured bootstrap email belongs to an invalid Identity account.");
        }

        var employee = await context.Employees.SingleOrDefaultAsync(
            item => item.Id == employeeId,
            cancellationToken);
        var roles = await userManager.GetRolesAsync(user);
        if (employee?.Role != Role.Manager
            || employee.AccessScope != AccessScope.Company
            || roles.Count != 1
            || !string.Equals(roles[0], Role.Manager.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The configured bootstrap email is already used by a non-Company-Manager account.");
        }
    }

    private static async Task<BootstrapCredentials> ReadOrCreateCredentialsAsync(
        string email,
        string credentialPath,
        CancellationToken cancellationToken)
    {
        if (File.Exists(credentialPath))
        {
            var existingJson = await File.ReadAllTextAsync(credentialPath, cancellationToken);
            var existingCredentials = JsonSerializer.Deserialize<BootstrapCredentials>(existingJson);
            if (existingCredentials is null
                || !string.Equals(existingCredentials.Email, email, StringComparison.OrdinalIgnoreCase)
                || existingCredentials.EmployeeId == Guid.Empty
                || string.IsNullOrWhiteSpace(existingCredentials.Password))
            {
                throw new InvalidOperationException("The local bootstrap credential file is invalid or belongs to another email.");
            }

            return existingCredentials;
        }

        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var credentials = new BootstrapCredentials(email, password, Guid.NewGuid());
        var directory = Path.GetDirectoryName(credentialPath)
            ?? throw new InvalidOperationException("The local bootstrap credential directory is invalid.");
        Directory.CreateDirectory(directory);
        await using var file = new FileStream(
            credentialPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(file, credentials, JsonOptions, cancellationToken);
        await file.FlushAsync(cancellationToken);
        return credentials;
    }

    private static string GetCredentialPath(string email)
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("The local application data directory is unavailable for bootstrap credentials.");
        }

        var emailHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.ToUpperInvariant())))
            .ToLowerInvariant()[..16];
        return Path.Combine(localApplicationData, "MechanicShop", "bootstrap", $"company-manager-{emailHash}.json");
    }

    private static string FormatIdentityFailure(string operation, IdentityResult result)
    {
        var codes = string.Join(",", result.Errors.Select(error => error.Code));
        return $"Could not {operation} for the initial Company Manager. Identity error codes: {codes}.";
    }

    private sealed record BootstrapCredentials(string Email, string Password, Guid EmployeeId);
}
