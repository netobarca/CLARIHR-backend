using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.EmployeeRelations;
using CLARIHR.Application.Features.EmployeeRelations.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Module-level operations of the employee-relations configuration masters family (REQ-003). No
/// <c>[ResourceActions]</c> on purpose: the controller exposes no PUT/PATCH resource mutations — its
/// single POST is an idempotent template-load whose response is an operation summary, not a listable
/// resource. Mirrors <c>LeaveConfigurationController</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Employee Relations Configuration")]
[AuthorizationPolicySet(EmployeeRelationsConfigurationPolicies.Read, EmployeeRelationsConfigurationPolicies.Manage)]
public sealed class EmployeeRelationsConfigurationController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost("companies/{companyId:guid}/employee-relations/load-template")]
    [ProducesResponseType<EmployeeRelationsTemplateSeedResultResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Load the El Salvador employee-relations configuration template",
        Description = """
            Applies the El Salvador template to the company's employee-relations configuration masters:
            the recognition types, disciplinary-action types (only `SUSPENSION_SIN_GOCE` carries the
            suspension flag) and disciplinary-action causes (all without a deduction concept) of
            Anexo A.2. Idempotent by normalized code: rows the company already has are never overwritten
            (even when edited or inactive) and are reported as skipped; only the missing template rows
            are created. Returns `200 OK` with the created/skipped summary.
            """)]
    public async Task<ActionResult<EmployeeRelationsTemplateSeedResultResponse>> LoadTemplate(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new LoadEmployeeRelationsTemplateCommand(companyId),
            cancellationToken);

        return this.ToActionResult(result);
    }
}
