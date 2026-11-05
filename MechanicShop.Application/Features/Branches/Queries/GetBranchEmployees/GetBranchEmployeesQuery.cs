using MechanicShop.Application.Features.Branches.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Branches.Queries.GetBranchEmployees;

public sealed record GetBranchEmployeesQuery(Guid BranchId) : IRequest<Result<List<BranchEmployeeDto>>>;
