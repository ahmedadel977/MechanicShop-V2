using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Employees.Commands.CreateLabor;

public sealed record CreateLaborCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid BranchId) : IRequest<Result<EmployeeAccountDto>>;
