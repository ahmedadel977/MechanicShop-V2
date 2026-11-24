namespace MechanicShop.Contracts.Requests.Employees;

public sealed record CreateBranchManagerRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid BranchId);
