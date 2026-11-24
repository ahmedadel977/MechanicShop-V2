namespace MechanicShop.Application.Features.Branches.Dtos;

public sealed record BranchDto(Guid BranchId, string Name, string Code, bool IsActive);
