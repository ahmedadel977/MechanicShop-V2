using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Workorders.Enums;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.common.UpdateOrderState;

public sealed record UpdateWorkOrderStateCommand(
    Guid WorkOrderId,
    WorkOrderState State,
    byte[]? RowVersion = null) : IRequest<Result<Updated>>;
