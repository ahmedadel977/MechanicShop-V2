using MechanicShop.Application.Features.Branches.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Branches.Commands.CreateBranch;

public sealed record CreateBranchCommand(string Name, string Code) : IRequest<Result<BranchDto>>;
