using MechanicShop.Application.Common.Models;

namespace MechanicShop.Application.Common.Interfaces;

public interface ICurrentEmployeeAccess
{
    Task<CurrentEmployeeAccess?> GetCurrentAsync(CancellationToken cancellationToken);
}
