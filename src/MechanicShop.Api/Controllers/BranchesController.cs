
using Asp.Versioning;
using MechanicShop.Application.Features.Branches.Commands.AddEmployeeToBranch;
using MechanicShop.Application.Features.Branches.Commands.CreateBranch;
using MechanicShop.Application.Features.Branches.Commands.SetBranchActive;
using MechanicShop.Application.Features.Branches.Commands.SetDefaultBranch;
using MechanicShop.Application.Features.Branches.Commands.SetEmployeeAccessScope;
using MechanicShop.Application.Features.Branches.Queries.GetBranches;
using MechanicShop.Application.Features.Branches.Queries.GetBranchById;
using MechanicShop.Application.Features.Branches.Queries.GetBranchEmployees;
using MechanicShop.Contracts.Requests.Branches;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MechanicShop.Api.Controllers;

[Route("api/v{version:apiVersion}/branches")]
[ApiVersion("1.0")]
[Authorize]
[Authorize(Policy = "ActiveBranchAccess")]
public sealed class BranchesController(ISender sender) : ApiController
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves all accessible branches.")]
    [EndpointDescription("Returns the branches accessible to the current authenticated employee based on their access scope.")]
    [EndpointName("GetBranches")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetBranches(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBranchesQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{branchId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves a branch by its ID.")]
    [EndpointDescription("Returns the details of the specified branch if it is accessible to the current authenticated employee.")]
    [EndpointName("GetBranchById")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetBranch(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBranchByIdQuery(branchId), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{branchId:guid}/employees")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves employees assigned to a branch.")]
    [EndpointDescription("Returns the employees assigned to the specified branch. Only authorized managers can perform this operation.")]
    [EndpointName("GetBranchEmployees")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetEmployees(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBranchEmployeesQuery(branchId), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost]
    [Authorize(Policy = "CompanyOnly")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Creates a new branch.")]
    [EndpointDescription("Creates a new branch within the company. Only managers with company-wide access can perform this operation.")]
    [EndpointName("CreateBranch")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> CreateBranch(
        CreateBranchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateBranchCommand(request.Name, request.Code),
            cancellationToken);

        return result.Match(
            branch => Created($"/api/v1.0/branches/{branch.BranchId}", branch),
            Problem);
    }

    [HttpPut("{branchId:guid}/active")]
    [Authorize(Policy = "CompanyOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Changes the active status of a branch.")]
    [EndpointDescription("Activates or deactivates the specified branch. Only managers with company-wide access can perform this operation.")]
    [EndpointName("SetBranchActive")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> SetActive(
        Guid branchId,
        SetBranchActiveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SetBranchActiveCommand(branchId, request.IsActive),
            cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPost("{branchId:guid}/employees/{employeeId:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Adds an employee to a branch.")]
    [EndpointDescription("Assigns an employee to the specified branch. Branch managers can assign employees only to branches they are authorized to manage.")]
    [EndpointName("AddEmployeeToBranch")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> AddEmployee(
        Guid branchId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddEmployeeToBranchCommand(branchId, employeeId),
            cancellationToken);

        return result.Match(_ => StatusCode(StatusCodes.Status201Created), Problem);
    }

    [HttpPut("employees/{employeeId:guid}/access-scope")]
    [Authorize(Policy = "CompanyOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Changes an employee's access scope.")]
    [EndpointDescription("Updates the access scope of the specified employee between branch-level and company-wide access. Only company managers can perform this operation.")]
    [EndpointName("SetEmployeeAccessScope")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> SetEmployeeAccessScope(
        Guid employeeId,
        SetEmployeeAccessScopeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SetEmployeeAccessScopeCommand(employeeId, request.AccessScope),
            cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPut("me/default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Changes the current employee's default branch.")]
    [EndpointDescription("Sets the specified branch as the default branch for the currently authenticated employee.")]
    [EndpointName("SetMyDefaultBranch")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> SetMyDefaultBranch(
        SetDefaultBranchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SetDefaultBranchCommand(request.BranchId),
            cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }
}
