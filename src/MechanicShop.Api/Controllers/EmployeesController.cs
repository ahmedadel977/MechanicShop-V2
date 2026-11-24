using Asp.Versioning;
using MechanicShop.Application.Features.Employees.Commands.CreateBranchManager;
using MechanicShop.Application.Features.Employees.Commands.CreateLabor;
using MechanicShop.Contracts.Requests.Employees;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MechanicShop.Api.Controllers;

[Route("api/v{version:apiVersion}/employees")]
[ApiVersion("1.0")]
[Authorize]
[Authorize(Policy = "ActiveBranchAccess")]
public sealed class EmployeesController(ISender sender) : ApiController
{
    [HttpPost("labors")]
    [EnableRateLimiting("RegisterPolicy")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Creates a labor employee account.")]
    [EndpointDescription("Creates a labor account in an active branch. Branch managers are limited to branches they are authorized to manage.")]
    [EndpointName("CreateLabor")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> CreateLabor(
        CreateLaborRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateLaborCommand(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Password,
                request.BranchId),
            cancellationToken);

        return result.Match(
            employee => Created($"/api/v1.0/employees/{employee.EmployeeId}", employee),
            Problem);
    }

    [HttpPost("branch-managers")]
    [EnableRateLimiting("RegisterPolicy")]
    [Authorize(Policy = "CompanyOnly")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Creates a branch manager account.")]
    [EndpointDescription("Creates a branch-scoped manager and assigns the employee to the selected active branch as the default membership. Only company managers can perform this operation.")]
    [EndpointName("CreateBranchManager")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> CreateBranchManager(
        CreateBranchManagerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateBranchManagerCommand(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Password,
                request.BranchId),
            cancellationToken);

        return result.Match(
            employee => Created($"/api/v1.0/employees/{employee.EmployeeId}", employee),
            Problem);
    }
}
