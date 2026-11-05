using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Workorders.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace MechanicShop.Application.Features.WorkOrders.common.CreateWorkORDER
{

    public sealed record CreateWorkOrderCommand(
    Spot Spot,
    Guid VehicleId,
    DateTimeOffset StartAt,
    List<Guid> RepairTaskIds,
    Guid? LaborId,
    Guid? BranchId = null)
: IRequest<Result<WorkOrderDto>>;
}
