using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Workorders.Enums;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.common.RelocateWorkOrder;

public sealed record RelocateWorkOrderCommand(
    Guid WorkOrderId,
    DateTimeOffset NewStartAt,
    Spot NewSpot,
    byte[]? RowVersion = null) : IRequest<Result<Updated>>;
