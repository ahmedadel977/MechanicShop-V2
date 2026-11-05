using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Events;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.common.AssignLabor;

public class AssignLaborCommandHandler(
    ILogger<AssignLaborCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache,
    IWorkOrderPolicy WorkOrderRuleService
    )
    : IRequestHandler<AssignLaborCommand, Result<Updated>>
{
    private readonly ILogger<AssignLaborCommandHandler> _logger = logger;
    private readonly IAppDbContext _context = context;
    private readonly HybridCache _cache = cache;
    private readonly IWorkOrderPolicy _workOrderValidator = WorkOrderRuleService;

    public async Task<Result<Updated>> Handle(AssignLaborCommand command, CancellationToken ct)
    {
        var workOrder = await _context.WorkOrders
            .FirstOrDefaultAsync(a => a.Id == command.WorkOrderId, ct);

        if (workOrder is null)
        {
            _logger.LogError("WorkOrder with Id '{WorkOrderId}' does not exist.", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }

        var labor = await _context.Employees.FindAsync([command.LaborId], ct);

        if (labor is null)
        {
            _logger.LogError("Invalid LaborId: {LaborId}", command.LaborId);
            return ApplicationErrors.LaborNotFound;
        }

        if (!await _context.EmployeeBranchMemberships.AnyAsync(
                membership => membership.EmployeeId == command.LaborId && membership.BranchId == workOrder.BranchId,
                ct))
        {
            return Error.Conflict("Labor.Branch.Mismatch", "الفني مش تابع لفرع أمر الشغل.");
        }

        if (await _workOrderValidator.IsLaborOccupied(command.LaborId, command.WorkOrderId, workOrder.StartAtUtc, workOrder.EndAtUtc))
        {
            _logger.LogError("Labor with Id '{LaborId}' is already occupied during the requested time.", workOrder.LaborId);
            return ApplicationErrors.LaborOccupied;
        }

        var updateLaborResult = workOrder.UpdateLabor(command.LaborId);

        if (updateLaborResult.IsError)
        {
            foreach (var error in updateLaborResult.Errors)
            {
                _logger.LogError("[LaborUpdate] {ErrorCode}: {ErrorDescription}", error.Code, error.Description);
            }

            return updateLaborResult.Errors;
        }

        _context.WorkOrders.Entry(workOrder).Property(order => order.RowVersion).OriginalValue = command.RowVersion!;
        try
        {
            workOrder.AddDomainEvent(new WorkOrderCollectionModified());
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return WorkOrderErrors.ConcurrencyConflict;
        }

        await _cache.RemoveByTagAsync("work-order", ct);

        return Result.Updated;
    }
}
