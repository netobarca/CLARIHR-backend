using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.System.GetApiStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Anonymous system/meta surface (liveness + API status). Not a tenant resource, so canonical CRUD
// criteria do not apply; [AuthorizationPolicySet] does NOT apply by design (stays out of
// GovernedFamilyRegex). Enrolled in the OpenAPI guardrail ("System"); route canonically versioned
// under `api/v1/system`.
[ApiController]
[Route("api/v1/system")]
[Tags("System")]
public sealed class SystemController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("status")]
    [ProducesResponseType<ApiStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Get API status",
        Description = """
            Anonymous liveness/status probe. Returns `200 OK` with the API status payload (e.g. service
            name and version) for monitoring and client bootstrap checks. No authentication required.
            """)]
    public async Task<ActionResult<ApiStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetApiStatusQuery(), cancellationToken);
        return this.ToActionResult(result);
    }
}
