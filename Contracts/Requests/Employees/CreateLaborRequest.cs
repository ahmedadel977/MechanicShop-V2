namespace MechanicShop.Contracts.Requests.Employees;

public sealed record CreateLaborRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid BranchId);
