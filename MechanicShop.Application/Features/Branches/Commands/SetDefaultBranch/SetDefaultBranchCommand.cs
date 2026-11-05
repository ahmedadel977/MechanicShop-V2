using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Branches.Commands.SetDefaultBranch;

public sealed record SetDefaultBranchCommand(Guid BranchId) : IRequest<Result<Updated>>;
