using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Application.Features.Leave.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Module-level operations of the leave-configuration masters family. No
/// <c>[ResourceActions]</c> on purpose: the controller exposes no PUT/PATCH resource
/// mutations — its single POST is an idempotent template-load whose response is an
/// operation summary, not a listable resource.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Leave Configuration")]
[AuthorizationPolicySet(LeaveConfigurationPolicies.Read, LeaveConfigurationPolicies.Manage)]
public sealed class LeaveConfigurationController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost("companies/{companyId:guid}/leave-configuration/load-template")]
    [ProducesResponseType<LeaveTemplateSeedResultResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Load the El Salvador leave-configuration template",
        Description = """
            Applies the El Salvador template to the company's leave-configuration masters: the
            five incapacity risks of Anexo A.2 (with their subsidy tranches), the minimum
            incapacity types (including `LACTANCIA`) and — only when the optional `year` query
            parameter travels — the national holidays of Art. 190 CT for that year (Semana
            Santa is derived from the Easter computus). Idempotent by code/date: rows the
            company already has are never overwritten (even when edited or inactive) and are
            reported as skipped; only the missing template rows are created. Returns `200 OK`
            with the created/skipped summary.
            """)]
    public async Task<ActionResult<LeaveTemplateSeedResultResponse>> LoadTemplate(
        Guid companyId,
        [FromQuery] int? year,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new LoadLeaveTemplateCommand(companyId, year),
            cancellationToken);

        return this.ToActionResult(result);
    }
}
