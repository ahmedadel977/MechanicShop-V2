using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.common.AssignLabor;

public sealed record AssignLaborCommand(Guid WorkOrderId, Guid LaborId, byte[]? RowVersion = null) : IRequest<Result<Updated>>;
