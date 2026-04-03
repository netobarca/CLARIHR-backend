using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.CommercialModules;
using CLARIHR.Backoffice.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Backoffice.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform/commercial-modules")]
public sealed class CommercialModulesController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<CommercialModuleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<CommercialModuleResponse>>> List(
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCommercialModulesQuery(), cancellationToken);
        return this.ToActionResult(result);
    }
}
