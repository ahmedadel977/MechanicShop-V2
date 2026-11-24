using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace MechanicShop.Infrastructure.identity.policies
{
    public class LaborAssignedRequirement : IAuthorizationRequirement { }

    public class LaborAssignedHandler : AuthorizationHandler<LaborAssignedRequirement>
    {
        private readonly IAppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LaborAssignedHandler(IAppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, LaborAssignedRequirement requirement)
        {
            var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                context.Fail();
                return;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            var workOrderIdString = httpContext?.Request?.RouteValues["workOrderId"]?.ToString();
            if (string.IsNullOrEmpty(workOrderIdString) || !Guid.TryParse(workOrderIdString, out var workOrderId))
            {
                context.Fail();
                return;
            }

            var cancellationToken = httpContext?.RequestAborted ?? CancellationToken.None;

            var isAssigned = await _context.WorkOrders
                .AnyAsync(wo => wo.Id == workOrderId && wo.LaborId == userId, cancellationToken);

            if (isAssigned)
            {
                context.Succeed(requirement);
                return;
            }
            if (context.User.IsInRole(nameof(Role.Manager)))
            {
                context.Succeed(requirement);
                return;
            }
            else
            {
                context.Fail();
            }
        }
    }
}
