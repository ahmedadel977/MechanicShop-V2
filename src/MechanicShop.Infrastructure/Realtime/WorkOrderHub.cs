using MechanicShop.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.Realtime;

[Authorize(Policy = "ActiveBranchAccess")]
public sealed class WorkOrderHub(
    ICurrentBranchContext branchContext,
    IAppDbContext context) : Hub
{
    public const string HubUrl = "/hubs/workorders";

    public static string BranchGroup(Guid branchId) => $"branch:{branchId:N}";

    public static string LaborGroup(Guid employeeId) => $"labor:{employeeId:N}";

    public override async Task OnConnectedAsync()
    {
        if (!branchContext.EmployeeId.HasValue)
        {
            Context.Abort();
            return;
        }

        if (branchContext.IsLabor)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, LaborGroup(branchContext.EmployeeId.Value));
        }
        else if (branchContext.IsCompanyWide)
        {
            var activeBranchIds = await context.Branches
                .Where(branch => branch.IsActive)
                .Select(branch => branch.Id)
                .ToListAsync(Context.ConnectionAborted);

            foreach (var branchId in activeBranchIds)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, BranchGroup(branchId));
            }
        }
        else if (branchContext.BranchId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BranchGroup(branchContext.BranchId.Value));
        }
        else
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }
}
