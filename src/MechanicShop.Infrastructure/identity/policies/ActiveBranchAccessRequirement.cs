using Microsoft.AspNetCore.Authorization;

namespace MechanicShop.Infrastructure.identity.policies;

public sealed class ActiveBranchAccessRequirement : IAuthorizationRequirement
{
}
