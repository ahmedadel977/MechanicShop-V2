using MechanicShop.Domain.Identity;

namespace MechanicShop.Application.Features.Branches.Dtos;

public sealed record BranchEmployeeDto(
    Guid EmployeeId,
    string Name,
    Role Role,
    AccessScope AccessScope,
    bool IsDefault);
