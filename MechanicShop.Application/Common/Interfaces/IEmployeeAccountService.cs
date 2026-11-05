using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;

namespace MechanicShop.Application.Common.Interfaces;

public interface IEmployeeAccountService
{
    Task<Result<Guid>> CreateAsync(
        string firstName,
        string lastName,
        string email,
        string password,
        Role role,
        AccessScope accessScope,
        Guid branchId,
        CancellationToken cancellationToken);
}
