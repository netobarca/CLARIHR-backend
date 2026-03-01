using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.System.GetApiStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("status")]
    [ProducesResponseType<ApiStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetApiStatusQuery(), cancellationToken);
        return this.ToActionResult(result);
    }
}
