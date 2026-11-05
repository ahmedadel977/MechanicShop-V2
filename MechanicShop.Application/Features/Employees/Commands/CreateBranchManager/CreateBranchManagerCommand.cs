using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Employees.Commands.CreateBranchManager;

public sealed record CreateBranchManagerCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid BranchId) : IRequest<Result<EmployeeAccountDto>>;
