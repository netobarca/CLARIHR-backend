using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// The employee's OWN payroll history (REQ-015 RF-005 — its P-03 ajustada: «ambos pueden ver»). The
/// class-level set maps reads to <c>ViewPayrollRuns</c>, which is deliberately AUTHN-ONLY at the policy
/// level: the REAL access decision is the SELF-OR-VIEW gate in the handler (the corporate grant OR the
/// personnel file LINKED to the caller — molde medical-claims/D-13); states are FIXED to
/// `CERRADA`/`AUTORIZADA` and no new permission exists.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewPayrollRuns, PersonnelFilePolicies.ManagePayrollRuns)]
public sealed class PersonnelFilePayrollHistoryController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpGet("api/v1/personnel-files/{publicId:guid}/payroll-history")]
    [Produces("application/json")]
    [ProducesResponseType<PayrollRunEmployeeHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Get an employee's own payroll history",
        Description = """
            The self-service payment history (REQ-015 RF-005): one row per `CERRADA`/`AUTORIZADA` run
            where the employee has INCLUDED lines, with their income/deduction/net sums, newest first.
            States are FIXED — drafts (`GENERADA`) and annulled runs NEVER appear on this surface, and
            there is no payslip PDF here (F2). Access: the corporate `ViewPayrollRuns` grant OR the
            personnel file linked to the caller; other files answer `404`.
            """)]
    public async Task<ActionResult<PayrollRunEmployeeHistoryResponse>> GetHistory(
        Guid publicId,
        [FromQuery] int? year = null,
        [FromQuery] int? pageNumber = null,
        [FromQuery, Range(1, PersonnelFileValidationRules.MaxPageSize)] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetMyPayrollHistoryQuery(publicId, year, pageNumber ?? 1, pageSize ?? 25),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpGet("api/v1/personnel-files/{publicId:guid}/payroll-history/{payrollRunPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PayrollRunEmployeeLinesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Get the employee's own lines of one payroll run",
        Description = """
            The self-service drill (REQ-015 RF-005): the employee's lines of ONE `CERRADA`/`AUTORIZADA`
            run — concept, class, units, base, calculated/override/final amount, inclusion flag and the
            line→source traceability — plus their income/deduction/net sums. A `GENERADA` or annulled run
            answers `404` on this surface (fixed states).
            """)]
    public async Task<ActionResult<PayrollRunEmployeeLinesResponse>> GetRunLines(
        Guid publicId,
        Guid payrollRunPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetMyPayrollHistoryRunLinesQuery(publicId, payrollRunPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }
}
