using Asp.Versioning;

using MechanicShop.Application.Features.Dashboard.Dtos;

using MechanicShop.Application.Features.Dashbord.Queris.GetWOkorderstats;
using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MechanicShop.Api.Controllers;

[ApiVersion("1.0")]
[Authorize]
[Authorize(Policy = "ActiveBranchAccess")]
[Route("api/v{version:apiVersion}/dashboard")]
public sealed class DashboardController(ISender sender) : ApiController
{
    [HttpGet("stats")]
    [ProducesResponseType(typeof(TodayWorkOrderStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTodayStats([FromQuery(Name = "fromUtc")] DateTimeOffset fromUtc, [FromQuery(Name = "toUtc")] DateTimeOffset toUtc, CancellationToken ct)
    {
        var result = await sender.Send(new GetWorkorderStatsQuery(fromUtc, toUtc), ct);

        return result.Match(
            response => Ok(response),
            Problem);
    }
}
