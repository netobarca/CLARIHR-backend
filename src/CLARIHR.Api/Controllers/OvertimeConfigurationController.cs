using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles.Overtime;
using CLARIHR.Application.Features.PersonnelFiles.Overtime.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Module-level operations of the overtime configuration masters family (REQ-007). No
/// <c>[ResourceActions]</c> on purpose: the controller exposes no PUT/PATCH resource mutations — its single
/// POST is an idempotent template-load whose response is an operation summary, not a listable resource.
/// Mirrors <c>EmployeeRelationsConfigurationController</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Overtime Configuration")]
[AuthorizationPolicySet(OvertimeConfigurationPolicies.Read, OvertimeConfigurationPolicies.Manage)]
public sealed class OvertimeConfigurationController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost("companies/{companyId:guid}/overtime-configuration/load-template")]
    [ProducesResponseType<OvertimeTemplateSeedResultResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Load the El Salvador overtime configuration template",
        Description = """
            Applies the El Salvador template to the company's overtime configuration masters: the 4 overtime
            types (with the reference factors HED 2.00 / HEN 2.50 / HEDF 4.00 / HENF 5.00 of Anexo A.2 —
            confirm with the accountant before running in production, the factors are editable) and the 6
            justification types. Idempotent by normalized code: rows the company already has are never
            overwritten (even when edited or inactive) and are reported as skipped; only the missing template
            rows are created. Returns `200 OK` with the created/skipped summary.
            """)]
    public async Task<ActionResult<OvertimeTemplateSeedResultResponse>> LoadTemplate(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new LoadOvertimeTemplateCommand(companyId),
            cancellationToken);

        return this.ToActionResult(result);
    }
}
