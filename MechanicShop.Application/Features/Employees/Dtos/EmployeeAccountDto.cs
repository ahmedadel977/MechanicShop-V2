using MechanicShop.Domain.Identity;

namespace MechanicShop.Application.Features.Employees.Dtos;

public sealed record EmployeeAccountDto(
    Guid EmployeeId,
    string Email,
    Role Role,
    AccessScope AccessScope,
    Guid BranchId);
