using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.common.DeleteWorkOrder;

public sealed record DeleteWorkOrderCommand(Guid WorkOrderId, byte[]? RowVersion = null) : IRequest<Result<Deleted>>;
