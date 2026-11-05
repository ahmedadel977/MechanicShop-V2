using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Branches.Commands.SetBranchActive;

public sealed record SetBranchActiveCommand(Guid BranchId, bool IsActive) : IRequest<Result<Updated>>;
