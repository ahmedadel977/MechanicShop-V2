using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;

namespace MechanicShop.Application.Features.Branches.Commands.SetEmployeeAccessScope;

public sealed record SetEmployeeAccessScopeCommand(Guid EmployeeId, AccessScope AccessScope)
    : IRequest<Result<Updated>>;
