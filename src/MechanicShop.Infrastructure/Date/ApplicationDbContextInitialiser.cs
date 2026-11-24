using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Branches;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Enums;
using MechanicShop.Domain.RepairTasks.Parts;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Enums;
using MechanicShop.Infrastructure.identity;



using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MechanicShop.Infrastructure.Date;

public class ApplicationDbContextInitialiser(
    ILogger<ApplicationDbContextInitialiser> logger,
    AppDbContext context, UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IHostEnvironment environment,
    IConfiguration configuration,
    CompanyManagerBootstrapper companyManagerBootstrapper)
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger = logger;
    private readonly AppDbContext _context = context;
    private readonly UserManager<AppUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly IHostEnvironment _environment = environment;
    private readonly IConfiguration _configuration = configuration;
    private readonly CompanyManagerBootstrapper _companyManagerBootstrapper = companyManagerBootstrapper;

    public async Task InitialiseAsync()
    {
        try
        {
            if (_configuration.GetValue<bool>("Database:ResetOnStartup"))
            {
                if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
                {
                    throw new InvalidOperationException("إعادة إنشاء قاعدة البيانات مسموحة في Development وTesting فقط.");
                }

                var databaseName = _context.Database.GetDbConnection().Database;
                if (new[] { "master", "model", "msdb", "tempdb" }
                    .Contains(databaseName, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("إعادة ضبط قواعد بيانات SQL Server النظامية ممنوعة.");
                }

                await _context.Database.EnsureDeletedAsync();
            }

            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        await SeedBranchesAsync();

        await EnsureRoleAsync(Role.Manager);
        await EnsureRoleAsync(Role.Labor);

        await EnsureEmployeeAsync("b6327240-0aea-46fc-863a-777fc4e42560", "John", "S.", Role.Labor);
        await EnsureEmployeeAsync("8104ab20-26c2-4651-b1de-c0baf04dbbd9", "Peter", "R.", Role.Labor);
        await EnsureEmployeeAsync("e17c83de-1089-4f19-bf79-5f789133d37f", "Kevin", "M.", Role.Labor);
        await EnsureEmployeeAsync("54cd01ba-b9ae-4c14-bab6-f3df0219ba4c", "Suzan", "L.", Role.Labor);

        if (_environment.IsEnvironment("Testing"))
        {
            await EnsureTestAccountAsync(
                "19a59129-6c20-417a-834d-11a208d32d96", "pm@localhost", "Primary", "Manager", Role.Manager, AccessScope.Company);
            await EnsureTestAccountAsync(
                "b6327240-0aea-46fc-863a-777fc4e42560", "john.labor@localhost", "John", "S.", Role.Labor, AccessScope.Branch);
            await EnsureTestAccountAsync(
                "8104ab20-26c2-4651-b1de-c0baf04dbbd9", "peter.labor@localhost", "Peter", "R.", Role.Labor, AccessScope.Branch);
            await EnsureTestAccountAsync(
                "e17c83de-1089-4f19-bf79-5f789133d37f", "kevin.labor@localhost", "Kevin", "M.", Role.Labor, AccessScope.Branch);
            await EnsureTestAccountAsync(
                "54cd01ba-b9ae-4c14-bab6-f3df0219ba4c", "suzan.labor@localhost", "Suzan", "L.", Role.Labor, AccessScope.Branch);
        }
        else if (_environment.IsDevelopment())
        {
            await _companyManagerBootstrapper.BootstrapAsync(CancellationToken.None);
        }

        await _context.SaveChangesAsync(CancellationToken.None);

        await SeedEmployeeBranchMembershipsAsync();

        if (!_context.Customers.Any())
        {
            List<Vehicle> vehicles = [
                        Vehicle.Create(id: Guid.Parse("61401e63-007b-4b1c-8914-9eb6e9bd95c5"), make: "Toyota", model: "Camry", year: 2020, licensePlate: "ABC123").Value,
                        Vehicle.Create(id: Guid.Parse("13c80914-41ad-4d46-b7bb-60f6c89ad01e"), make: "Honda", model: "Civic", year: 2018, licensePlate: "XYZ456").Value,
                    ];

            _context.Customers.AddRange(
            [
                Customer.Create(id: Guid.Parse("f522bbe5-e3b1-4e2c-a8a3-c41550dcf39d"), name: "John Doe", phoneNumber: "123456789", email: "yfhjjkhgjkhcdh@gmail.com", vehicles: vehicles).Value,
                Customer.Create(id: Guid.Parse("73a04dd3-c81a-4a54-9882-ef1017eb192d"), name: "Sarah Peter", phoneNumber: "987654321", email: "yfhjjkhgjkhcdh@gmail.com", vehicles: [Vehicle.Create(id: Guid.Parse("a04f329d-0f5a-46a0-beae-699c034ae401"), make: "Ford", model: "Focus", year: 2021, licensePlate: "DEF789").Value, Vehicle.Create(id: Guid.Parse("cf60e95b-5752-4c26-aa07-31a34164606c"), make: "Chevrolet", model: "Malibu", year: 2019, licensePlate: "GHI012").Value,]).Value,
            ]);
        }

        if (!_context.RepairTasks.Any())
        {
            _context.RepairTasks.AddRange([
                RepairTask.Create(id: Guid.Parse("616aebb1-d515-4b40-8d47-8d5c0b67a313"), name: "Engine Oil Change", laborCost: 50.00m, estimatedDurationInMins: RepairDurationInMinutes.Min60, parts: [Part.Create(Guid.Parse("ec65225c-9066-4a1c-974f-f183c39fdd16"), "Engine Oil", 25.00m, 1).Value, Part.Create(Guid.Parse("62ad80e3-2cff-41af-ab40-16fab8db8b38"), "Oil Filter", 10.00m, 1).Value]).Value,
                RepairTask.Create(id: Guid.Parse("4fa0be55-06f6-4616-b086-e1f0c9354cd8"), name: "Brake Replacement", laborCost: 150.00m, estimatedDurationInMins: RepairDurationInMinutes.Min90, parts: [Part.Create(Guid.Parse("86375a12-715e-4aa4-aad9-c0f9ccf44a14"), "Brake Pads", 40.00m, 2).Value, Part.Create(Guid.Parse("526d89c3-a971-4ea7-ba15-de6b50b13c21"), "Brake Fluid", 15.00m, 1).Value]).Value,
                RepairTask.Create(id: Guid.Parse("a376b5d1-6b2d-4dd8-883e-d3d1721c1316"), name: "Tire Rotation", laborCost: 30.00m, estimatedDurationInMins: RepairDurationInMinutes.Min45, parts: [Part.Create(Guid.Parse("a46f974e-a198-4098-8a1f-6be6e68ec743"), "Tire Valve", 5.00m, 4).Value]).Value,
                RepairTask.Create(id: Guid.Parse("a770cc6e-0c8b-4ac5-9ee6-6928682bd47e"), name: "Battery Replacement", laborCost: 70.00m, estimatedDurationInMins: RepairDurationInMinutes.Min30, parts: [Part.Create(Guid.Parse("d4fd3255-29dc-4d45-9d87-f58ab98bc28b"), "Car Battery", 120.00m, 1).Value]).Value,
                RepairTask.Create(id: Guid.Parse("e4c2b675-4a60-488f-a7b4-61966e7e80e3"), name: "Wheel Alignment", laborCost: 80.00m, estimatedDurationInMins: RepairDurationInMinutes.Min60, parts: [Part.Create(Guid.Parse("fa3b9a7e-1c2d-4e3f-9b8a-0c1d2e3f4a5b"), "Alignment Shim Kit (per wheel)", 5.00m, 4).Value]).Value,
                RepairTask.Create(id: Guid.Parse("1cb1608c-3bc7-4325-99c3-8244c0fb412f"), name: "Air Conditioning Recharge", laborCost: 100.00m, estimatedDurationInMins: RepairDurationInMinutes.Min30, parts: [Part.Create(Guid.Parse("526dca0a-d236-47d3-8e8f-c83d555b2de9"), "Refrigerant", 50.00m, 1).Value]).Value,
                RepairTask.Create(id: Guid.Parse("a8e9b4e0-8581-40df-967d-51a0f4fabc0e"), name: "Spark Plug Replacement", laborCost: 90.00m, estimatedDurationInMins: RepairDurationInMinutes.Min60, parts: [Part.Create(Guid.Parse("019f5eab-a8a5-44d4-92b3-1f998e3f10c2"), "Spark Plug", 10.00m, 4).Value]).Value,
                RepairTask.Create(id: Guid.Parse("90f2f3ef-3357-439e-9689-628aa08200c1"), name: "Engine Diagnostic", laborCost: 120.00m, estimatedDurationInMins: RepairDurationInMinutes.Min120, parts: [Part.Create(Guid.Parse("c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f"), "Smoke Leak Detector Fluid Cartridge", 20.00m, 1).Value]).Value,
                RepairTask.Create(id: Guid.Parse("d124651e-ca72-467e-ba28-81ea4a2080bc"), name: "Timing Belt Replacement", laborCost: 200.00m, estimatedDurationInMins: RepairDurationInMinutes.Min120, parts: [Part.Create(Guid.Parse("06b764a0-73a2-4c37-b279-adae3856499c"), "Timing Belt", 75.00m, 1).Value]).Value,
                RepairTask.Create(id: Guid.Parse("cee9b309-8620-4028-8d38-2532771ab3ea"), name: "Transmission Fluid Change", laborCost: 100.00m, estimatedDurationInMins: RepairDurationInMinutes.Min45, parts: [Part.Create(Guid.Parse("0a8b0c19-873a-4da0-811b-45ff85bca0ed"), "Transmission Fluid", 60.00m, 1).Value]).Value
            ]);
        }

        await _context.SaveChangesAsync();

        await SeedWorkOrdersAsync();
    }
    private async Task SeedBranchesAsync()
    {
        var branches = new[]
        {
            Branch.Create(SeedDataIds.MainBranch, "Central Workshop", "MAIN").Value,
            Branch.Create(SeedDataIds.NorthBranch, "North Workshop", "NORTH").Value
        };

        foreach (var branch in branches)
        {
            if (!await _context.Branches.AnyAsync(item => item.Id == branch.Id))
            {
                await _context.Branches.AddAsync(branch);
            }
        }

        await _context.SaveChangesAsync(CancellationToken.None);
    }

    private async Task EnsureRoleAsync(Role role)
    {
        var roleName = role.ToString();
        if (await _roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the {roleName} identity role. Error codes: {string.Join(",", result.Errors.Select(error => error.Code))}.");
        }
    }

    private async Task EnsureTestAccountAsync(
        string id,
        string email,
        string firstName,
        string lastName,
        Role role,
        AccessScope accessScope)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                Id = id,
                Email = email,
                UserName = email,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, email);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create a test identity user. Error codes: {string.Join(",", createResult.Errors.Select(error => error.Code))}.");
            }
        }
        else if (!string.Equals(user.Id, id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A test identity email is already associated with another user ID.");
        }

        if (!await _userManager.IsInRoleAsync(user, role.ToString()))
        {
            var roleResult = await _userManager.AddToRoleAsync(user, role.ToString());
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign a test identity role. Error codes: {string.Join(",", roleResult.Errors.Select(error => error.Code))}.");
            }
        }

        await EnsureEmployeeAsync(id, firstName, lastName, role, accessScope);
    }

    private async Task EnsureEmployeeAsync(string id, string firstName, string lastName, Role role, AccessScope accessScope = AccessScope.Branch)
    {
        var employeeId = Guid.Parse(id);
        if (!await _context.Employees.AnyAsync(employee => employee.Id == employeeId))
        {
            var employee = Employee.Create(employeeId, firstName, lastName, role, accessScope);
            if (employee.IsError)
            {
                throw new InvalidOperationException(employee.TopError.Description);
            }

            await _context.Employees.AddAsync(employee.Value);
        }
    }

    private async Task SeedEmployeeBranchMembershipsAsync()
    {
        var memberships = new List<(Guid EmployeeId, Guid BranchId, bool IsDefault)>
        {
            (EmployeeId: Guid.Parse("b6327240-0aea-46fc-863a-777fc4e42560"), BranchId: SeedDataIds.MainBranch, IsDefault: true),
            (EmployeeId: Guid.Parse("b6327240-0aea-46fc-863a-777fc4e42560"), BranchId: SeedDataIds.NorthBranch, IsDefault: false),
            (EmployeeId: Guid.Parse("e17c83de-1089-4f19-bf79-5f789133d37f"), BranchId: SeedDataIds.MainBranch, IsDefault: true),
            (EmployeeId: Guid.Parse("e17c83de-1089-4f19-bf79-5f789133d37f"), BranchId: SeedDataIds.NorthBranch, IsDefault: false),
            (EmployeeId: Guid.Parse("8104ab20-26c2-4651-b1de-c0baf04dbbd9"), BranchId: SeedDataIds.MainBranch, IsDefault: false),
            (EmployeeId: Guid.Parse("8104ab20-26c2-4651-b1de-c0baf04dbbd9"), BranchId: SeedDataIds.NorthBranch, IsDefault: true),
            (EmployeeId: Guid.Parse("54cd01ba-b9ae-4c14-bab6-f3df0219ba4c"), BranchId: SeedDataIds.MainBranch, IsDefault: false),
            (EmployeeId: Guid.Parse("54cd01ba-b9ae-4c14-bab6-f3df0219ba4c"), BranchId: SeedDataIds.NorthBranch, IsDefault: true)
        };

        var testManagerId = Guid.Parse("19a59129-6c20-417a-834d-11a208d32d96");
        if (await _context.Employees.AnyAsync(employee => employee.Id == testManagerId))
        {
            memberships.Add((testManagerId, SeedDataIds.MainBranch, true));
            memberships.Add((testManagerId, SeedDataIds.NorthBranch, false));
        }

        foreach (var item in memberships)
        {
            if (await _context.Employees.AnyAsync(employee => employee.Id == item.EmployeeId)
                && !await _context.EmployeeBranchMemberships.AnyAsync(membership =>
                    membership.EmployeeId == item.EmployeeId && membership.BranchId == item.BranchId))
            {
                var membership = EmployeeBranchMembership.Create(item.EmployeeId, item.BranchId, item.IsDefault);
                if (membership.IsError)
                {
                    throw new InvalidOperationException(membership.TopError.Description);
                }

                await _context.EmployeeBranchMemberships.AddAsync(membership.Value);
            }
        }

        await _context.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SeedWorkOrdersAsync()
    {
        var tomorrow = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero);
        var now = DateTimeOffset.UtcNow;
        var oilChange = await _context.RepairTasks.SingleAsync(task => task.Id == Guid.Parse("616aebb1-d515-4b40-8d47-8d5c0b67a313"));
        var brakeReplacement = await _context.RepairTasks.SingleAsync(task => task.Id == Guid.Parse("4fa0be55-06f6-4616-b086-e1f0c9354cd8"));
        var vehicles = await _context.Vehicles.OrderBy(vehicle => vehicle.Id).Take(4).ToListAsync();
        if (vehicles.Count != 4)
        {
            throw new InvalidOperationException("Development seed data requires four vehicles.");
        }

        var seedWorkOrders = new[]
        {
            (Id: SeedDataIds.MainWorkOrderTomorrow, BranchId: SeedDataIds.MainBranch, VehicleId: vehicles[0].Id, LaborId: Guid.Parse("b6327240-0aea-46fc-863a-777fc4e42560"), StartAt: tomorrow.AddHours(9), Task: oilChange, State: WorkOrderState.Scheduled, Spot: Spot.A),
            (Id: SeedDataIds.MainWorkOrderInProgress, BranchId: SeedDataIds.MainBranch, VehicleId: vehicles[1].Id, LaborId: Guid.Parse("e17c83de-1089-4f19-bf79-5f789133d37f"), StartAt: now.AddMinutes(-30), Task: oilChange, State: WorkOrderState.InProgress, Spot: Spot.B),
            (Id: SeedDataIds.NorthWorkOrderTomorrow, BranchId: SeedDataIds.NorthBranch, VehicleId: vehicles[2].Id, LaborId: Guid.Parse("8104ab20-26c2-4651-b1de-c0baf04dbbd9"), StartAt: tomorrow.AddHours(11), Task: brakeReplacement, State: WorkOrderState.Scheduled, Spot: Spot.A),
            (Id: SeedDataIds.NorthWorkOrderInProgress, BranchId: SeedDataIds.NorthBranch, VehicleId: vehicles[3].Id, LaborId: Guid.Parse("54cd01ba-b9ae-4c14-bab6-f3df0219ba4c"), StartAt: now.AddMinutes(-30), Task: oilChange, State: WorkOrderState.InProgress, Spot: Spot.B)
        };

        foreach (var seed in seedWorkOrders)
        {
            if (await _context.WorkOrders.IgnoreQueryFilters().AnyAsync(workOrder => workOrder.Id == seed.Id))
            {
                continue;
            }

            var workOrder = WorkOrder.Create(
                seed.Id,
                seed.BranchId,
                seed.VehicleId,
                seed.StartAt,
                seed.StartAt.AddMinutes((int)seed.Task.EstimatedDurationInMins),
                seed.LaborId,
                seed.Spot,
                [seed.Task]);

            if (workOrder.IsError)
            {
                throw new InvalidOperationException(workOrder.TopError.Description);
            }

            if (seed.State == WorkOrderState.InProgress)
            {
                workOrder.Value.UpdateState(WorkOrderState.InProgress);
            }

            await _context.WorkOrders.AddAsync(workOrder.Value);
        }

        await _context.SaveChangesAsync(CancellationToken.None);
    }
}

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();

        await initialiser.SeedAsync();
    }
}
