using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Workorders.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Dashbord.Queris.GetWOkorderstats
{
    public class GetWorkorderStatsQueryHandler(IAppDbContext context) : IRequestHandler<GetWorkorderStatsQuery, Result<TodayWorkOrderStatsDto>>
    {
        public async Task<Result<TodayWorkOrderStatsDto>> Handle(GetWorkorderStatsQuery request, CancellationToken cancellationToken)
        {
            var startsOfDay = request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var endOfDay = request.Date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var query = context.WorkOrders.Include(x => x.Vehicle!).ThenInclude(x => x!.Customer)
                .Include(x => x.RepairTasks).ThenInclude(x => x.Parts)
                .Include(x => x.Invoice)
                .Where(x => x.StartAtUtc >= startsOfDay && x.StartAtUtc < endOfDay);

            var total = await query.AsNoTracking().CountAsync(cancellationToken);
            if (total == 0)
            {
                // return empty stats for zero total
                var emptyDto = new TodayWorkOrderStatsDto
                {
                    Date = request.Date,
                    Total = 0,
                    Scheduled = 0,
                    InProgress = 0,
                    Completed = 0,
                    Cancelled = 0,
                    TotalRevenue = 0,
                    TotalPartsCost = 0,
                    TotalLaborCost = 0,
                    UniqueVehicles = 0,
                    UniqueCustomers = 0,
                    NetProfit = 0,
                    ProfitMargin = 0,
                    CompletionRate = 0,
                    AverageRevenuePerOrder = 0,
                    OrdersPerVehicle = 0,
                    PartsCostRatio = 0,
                    LaborCostRatio = 0,
                    CancellationRate = 0
                };
                return emptyDto;
            }



            // Non-empty case: compute totals and return
            var invoiceTotals = await query.AsNoTracking()
                .Where(x => x.Invoice != null)
                .Select(x => new
                {
                    Subtotal = x.Invoice!.LineItems.Sum(lineItem => lineItem.Quantity * lineItem.UnitPrice),
                    x.Invoice.DiscountAmount,
                    x.Invoice.TaxAmount
                })
                .ToListAsync(cancellationToken);
            var totalRevenue = invoiceTotals.Sum(invoice =>
                invoice.Subtotal - invoice.DiscountAmount + invoice.TaxAmount);
            var totalPartsCost = await query.AsNoTracking().SumAsync(x => x.RepairTasks.Sum(y => y.Parts.Sum(z => z.Cost)), cancellationToken);
            var totalLaborCost = await query.AsNoTracking().SumAsync(x => x.RepairTasks.Sum(y => y.LaborCost), cancellationToken);
            var uniqueVehicles = await query.AsNoTracking().Select(x => x.VehicleId).Distinct().CountAsync(cancellationToken);
            var uniqueCustomers = await query.AsNoTracking()
                .Where(x => x.Vehicle != null)
                .Select(x => x.Vehicle!.CustomerId)
                .Distinct()
                .CountAsync(cancellationToken);
            var netProfit = totalRevenue - totalPartsCost - totalLaborCost;

            var dto = new TodayWorkOrderStatsDto
            {
                Date = request.Date,
                Total = total,
                Scheduled = await query.AsNoTracking().CountAsync(x => x.State == WorkOrderState.Scheduled, cancellationToken),
                InProgress = await query.AsNoTracking().CountAsync(x => x.State == WorkOrderState.InProgress, cancellationToken),
                Completed = await query.AsNoTracking().CountAsync(x => x.State == WorkOrderState.Completed, cancellationToken),
                Cancelled = await query.AsNoTracking().CountAsync(x => x.State == WorkOrderState.Cancelled, cancellationToken),
                TotalRevenue = totalRevenue,
                TotalPartsCost = totalPartsCost,
                TotalLaborCost = totalLaborCost,
                UniqueVehicles = uniqueVehicles,
                UniqueCustomers = uniqueCustomers,
                NetProfit = netProfit,
                ProfitMargin = totalRevenue == 0 ? 0 : (netProfit / totalRevenue) * 100,
                CompletionRate = total == 0 ? 0 :( (decimal)await query.AsNoTracking().CountAsync(x => x.State == WorkOrderState.Completed, cancellationToken) / total)*100,
                AverageRevenuePerOrder = total == 0 ? 0 : totalRevenue / total,
                OrdersPerVehicle = uniqueVehicles == 0 ? 0 : (decimal)total / uniqueVehicles,
                PartsCostRatio = totalRevenue == 0 ? 0 : (totalPartsCost / totalRevenue) * 100,
                LaborCostRatio = totalRevenue == 0 ? 0 : (totalLaborCost / totalRevenue) * 100,
                CancellationRate = total == 0 ? 0 : (decimal)await query.AsNoTracking().CountAsync(x => x.State == WorkOrderState.Cancelled, cancellationToken) / total
            };


            return dto;

        }
    }
}
            
        
