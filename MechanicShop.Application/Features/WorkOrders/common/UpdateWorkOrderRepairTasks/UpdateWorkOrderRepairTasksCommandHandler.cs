using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Events;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace MechanicShop.Application.Features.WorkOrders.common.UpdateWorkOrderRepairTasks;

public class UpdateWorkOrderRepairTasksCommandHandler(
    ILogger<UpdateWorkOrderRepairTasksCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache,
    IWorkOrderPolicy workOrderValidator)
    : IRequestHandler<UpdateWorkOrderRepairTasksCommand, Result<Updated>>
{
    public async Task<Result<Updated>> Handle(UpdateWorkOrderRepairTasksCommand command, CancellationToken ct)
    {
        var workOrder = await context.WorkOrders
            .Include(x => x.RepairTasks)
            .FirstOrDefaultAsync(x => x.Id == command.WorkOrderId, ct);
        if (workOrder is null)
        {
            logger.LogWarning("Work order {WorkOrderId} not found.", command.WorkOrderId);
            return Error.NotFound("WorkOrder.NotFound", "Work order not found.");
        }
        var repairTasks = await context.RepairTasks
            .Where(x => command.RepairTaskIds.Contains(x.Id))
            .ToListAsync(ct);
        if (repairTasks.Count != command.RepairTaskIds.Length)
        {
            logger.LogWarning("One or more repair tasks not found for work order {WorkOrderId}.", command.WorkOrderId);
            return Error.NotFound("RepairTask.NotFound", "One or more repair tasks not found.");
        }
        var clearExistingResult = workOrder.ClearRepairTasks();

        if (clearExistingResult.IsError)
        {
            logger.LogWarning("Failed to clear existing repair tasks for work order {WorkOrderId}.", command.WorkOrderId);
            return clearExistingResult;
        }
        foreach (var repairTask in repairTasks)
        {
            var addResult = workOrder.AddRepairTask(repairTask);
            if (addResult.IsError)
            {
                logger.LogWarning("Failed to add repair task {RepairTaskId} to work order {WorkOrderId}.", repairTask.Id, command.WorkOrderId);
                return addResult;
            }
        }
        var totalDuration = TimeSpan.FromMinutes(repairTasks.Sum(x => (int)x.EstimatedDurationInMins));
        var newEndAt = workOrder.StartAtUtc + totalDuration;
        if (workOrderValidator.IsOutsideOperatingHours(workOrder.StartAtUtc, totalDuration))
        {
            return Error.Conflict("WorkOrder_Outside_OperatingHours", "WorkOrder timing exceeds business hours.");
        }

        var spotCheckResult = await workOrderValidator.CheckSpotAvailabilityAsync(
            workOrder.Spot,
            workOrder.StartAtUtc,
            newEndAt,
            excludeWorkOrderId: workOrder.Id,
            ct: ct);

        if (spotCheckResult.IsError)
        {
            return spotCheckResult.Errors;
        }

        if (workOrder.LaborId.HasValue && await workOrderValidator.IsLaborOccupied(workOrder.LaborId.Value, workOrder.Id, workOrder.StartAtUtc, newEndAt))
        {
            return ApplicationErrors.LaborOccupied;
        }

        workOrder.UpdateTiming(workOrder.StartAtUtc, newEndAt);

        context.WorkOrders.Entry(workOrder).Property(order => order.RowVersion).OriginalValue = command.RowVersion!;
        try
        {
            workOrder.AddDomainEvent(new WorkOrderCollectionModified(workOrder.BranchId, workOrder.LaborId));
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return WorkOrderErrors.ConcurrencyConflict;
        }

        await cache.RemoveByTagAsync("work-order", ct);
        return Result.Updated;



    }
};
