using MechanicShop.Application.Features.Branches.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Branches.Queries.GetBranchById;

public sealed record GetBranchByIdQuery(Guid BranchId) : IRequest<Result<BranchDto>>;
