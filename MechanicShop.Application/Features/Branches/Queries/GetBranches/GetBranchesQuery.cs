using MechanicShop.Application.Features.Branches.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Branches.Queries.GetBranches;

public sealed record GetBranchesQuery : IRequest<Result<List<BranchDto>>>;
