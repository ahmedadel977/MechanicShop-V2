using MechanicShop.Domain.Identity;

namespace MechanicShop.Application.Common.Models;

public sealed record CurrentEmployeeAccess(Guid EmployeeId, Role Role, AccessScope AccessScope);
