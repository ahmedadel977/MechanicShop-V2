using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Branches.Commands.AddEmployeeToBranch;

public sealed record AddEmployeeToBranchCommand(Guid BranchId, Guid EmployeeId) : IRequest<Result<Created>>;
