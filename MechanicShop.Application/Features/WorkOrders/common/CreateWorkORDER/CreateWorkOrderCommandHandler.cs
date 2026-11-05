using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.common.CreateWorkORDER;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Application.Features.WorkOrders.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Events;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

public class CreateWorkOrderCommandHandler(
    ILogger<CreateWorkOrderCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache,
    IWorkOrderPolicy workOrderValidator,
    ICurrentBranchContext currentBranchContext
    )
    : IRequestHandler<CreateWorkOrderCommand, Result<WorkOrderDto>>
{
    private readonly ILogger<CreateWorkOrderCommandHandler> _logger = logger;
    private readonly IAppDbContext _context = context;
    private readonly HybridCache _cache = cache;
    private readonly IWorkOrderPolicy _workOrderPolicy = workOrderValidator;
    private readonly ICurrentBranchContext _currentBranchContext = currentBranchContext;

    public async Task<Result<WorkOrderDto>> Handle(CreateWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var branchId = _currentBranchContext.IsCompanyWide
            ? command.BranchId
            : _currentBranchContext.BranchId;
        if (!branchId.HasValue)
        {
            return Error.Validation("Branch.Id.Required", "An active branch must be selected to create a work order.");
        }

        if (!_currentBranchContext.IsCompanyWide
            && command.BranchId.HasValue
            && command.BranchId.Value != branchId.Value)
        {
            return Error.Forbidden("Branch.Access.Denied", "A branch-scoped user cannot create a work order in another branch.");
        }

        var branchIsActive = await _context.Branches.AnyAsync(
            branch => branch.Id == branchId.Value && branch.IsActive,
            cancellationToken);
        if (!branchIsActive)
        {
            return Error.NotFound("Branch.NotFound", "The selected branch does not exist or is inactive.");
        }

        var repairTasks =await _context.RepairTasks
            .Where(x => command.RepairTaskIds.Contains(x.Id))
           .ToListAsync(cancellationToken);
        if (repairTasks.Count != command.RepairTaskIds.Count)
        {
            var missingIds = command.RepairTaskIds.Except(repairTasks       .Select(t => t.Id)).ToArray();

            _logger.LogError("Some RepairTaskIds not found: {MissingIds}", string.Join(", ", missingIds));

            return ApplicationErrors.RepairTaskNotFound; 
        }
        var totalEstimatedDuration = TimeSpan.FromMinutes(repairTasks.Sum(r => (int)r.EstimatedDurationInMins));
        var endAt = command.StartAt.Add(totalEstimatedDuration);
        if (_workOrderPolicy.IsOutsideOperatingHours(command.StartAt, totalEstimatedDuration))
        {
            _logger.LogError("The WorkOrder time ({StartAt} ? {EndAt}) is outside of store operating hours.", command.StartAt, endAt);

            return ApplicationErrors.WorkOrderOutsideOperatingHour(command.StartAt, endAt);
        }
        var checkMinRequirementResult = _workOrderPolicy.ValidateMinimumRequirement(command.StartAt, endAt);

        if (checkMinRequirementResult.IsError)
        {
            _logger.LogError("WorkOrder duration is shorter than the configured minimum.");

            return checkMinRequirementResult.Errors;
        }
        var checkSpotAvailabilityResult = await _workOrderPolicy.CheckSpotAvailabilityAsync(
            command.Spot,
            command.StartAt,
            endAt,
            excludeWorkOrderId: null,
            cancellationToken);

        if (checkSpotAvailabilityResult.IsError)
        {
            _logger.LogError("Spot: {Spot} is not available.", command.Spot.ToString());
            return checkSpotAvailabilityResult.Errors;
        }
        var vehicle = await _context.Vehicles.Include(v => v.Customer).FirstOrDefaultAsync(v => v.Id == command.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            _logger.LogError("Vehicle with Id '{VehicleId}' does not exist.", command.VehicleId);

            return ApplicationErrors.VehicleNotFound;
        }

        var labor = command.LaborId.HasValue
            ? await _context.Employees.FindAsync([command.LaborId.Value], cancellationToken)
            : null;

        if (command.LaborId.HasValue && labor is null)
        {
            _logger.LogError("Invalid LaborId: {LaborId}", command.LaborId.ToString());
            return ApplicationErrors.LaborNotFound;
        }

        if (command.LaborId.HasValue && !await _context.EmployeeBranchMemberships.AnyAsync(
                membership => membership.EmployeeId == command.LaborId.Value && membership.BranchId == branchId.Value,
                cancellationToken))
        {
            return Error.Conflict("Labor.Branch.Mismatch", "The labor employee is not a member of the selected branch.");
        }

        var hasVehicleConflict = await _context.WorkOrders
           .AnyAsync(
               a =>
               a.VehicleId == command.VehicleId &&
               a.StartAtUtc.Date == command.StartAt.Date &&
               a.StartAtUtc < endAt &&
               a.EndAtUtc > command.StartAt
               );

        if (hasVehicleConflict)
        {
            _logger.LogError("Vehicle with Id '{VehicleId}' already has an overlapping WorkOrder.", command.VehicleId);
            return Error.Conflict(
                code: "Vehicle_Overlapping_WorkOrders",
                description: "The vehicle already has an overlapping WorkOrder.");
        }

        var isLaborOccupied = command.LaborId.HasValue && await _context.WorkOrders
            .AnyAsync(
                a =>
                a.LaborId == command.LaborId.Value &&
                a.StartAtUtc < endAt &&
                a.EndAtUtc > command.StartAt,
                cancellationToken);

        if (isLaborOccupied)
        {
            _logger.LogError("Labor with Id '{LaborId}' is already occupied during the requested time.", command.LaborId);
            return Error.Conflict(
                code: "Labor_Occupied",
                description: "Labor is already occupied during the requested time.");
        }
        var createWorkOrderResult = WorkOrder.Create(
           Guid.NewGuid(),
           branchId.Value,
           command.VehicleId,
           command.StartAt,
           endAt,
           command.LaborId,
           command.Spot,
           repairTasks);
        if (createWorkOrderResult.IsError)
        {
            _logger.LogError("Failed to create WorkOrder: {Errors}", string.Join(", ", createWorkOrderResult.Errors.Select(e => e.Description)));
            return createWorkOrderResult.Errors;
        }
        var workOrder = createWorkOrderResult.Value;
        await _context.WorkOrders.AddAsync(workOrder, cancellationToken);

        workOrder.AddDomainEvent(new WorkOrderCollectionModified());
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("WorkOrder with Id '{WorkOrderId}' created successfully.", workOrder.Id);


        await _cache.RemoveByTagAsync("work-order",cancellationToken);
        return workOrder.ToDto();





    }
}
