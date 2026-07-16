using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Application.Features.Payroll.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Module-level operations of the payroll configuration masters family (REQ-012). No
/// <c>[ResourceActions]</c> on purpose: the controller exposes no PUT/PATCH resource mutations — its single
/// POST is an idempotent template-load whose response is an operation summary, not a listable resource.
/// Mirrors <c>OvertimeConfigurationController</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Payroll Configuration")]
[AuthorizationPolicySet(PayrollConfigurationPolicies.Read, PayrollConfigurationPolicies.Manage)]
public sealed class PayrollConfigurationController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost("companies/{companyId:guid}/payroll-configuration/load-template")]
    [ProducesResponseType<PayrollConfigurationTemplateSeedResultResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Load the El Salvador payroll configuration template",
        Description = """
            Applies the El Salvador template to the company's payroll configuration masters: today the
            44-hour LEGAL work schedule (`JORNADA_ORDINARIA` — Mon-Fri 08:00-17:00 with a 12:00-13:00 meal,
            8 h/day, plus Saturday 08:00-12:00, 4 h; golden 11 of the signed A.3). Idempotent by normalized
            code: rows the company already has are never overwritten (even when edited or inactive) and are
            reported as skipped; only the missing template rows are created. Returns `200 OK` with the
            created/skipped summary.
            """)]
    public async Task<ActionResult<PayrollConfigurationTemplateSeedResultResponse>> LoadTemplate(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new LoadPayrollConfigurationTemplateCommand(companyId),
            cancellationToken);

        return this.ToActionResult(result);
    }
}
