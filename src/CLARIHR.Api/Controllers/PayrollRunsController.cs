using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Payroll-run generation (REQ-012 §3.4 — PR-5: generate + pre-flight; the review/decision surface
/// arrives in PR-6 and the bandeja/reporting in PR-7). The precise HR gate lives in the handlers
/// (EnsureCanManagePayrollRunsAsync); the marker pair is the declarative defense-in-depth.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Payroll Runs")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewPayrollRuns, PersonnelFilePolicies.ManagePayrollRuns)]
public sealed class PayrollRunsController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost("companies/{companyId:guid}/payroll-runs")]
    [ProducesResponseType<PayrollRunResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Generate a payroll run",
        Description = """
            Generates the payroll run of a Nómina × period (REQ-012): resolves the population (active
            plazas whose payroll type matches the Nómina; RETIRADO profiles and employees with an issued
            settlement in the period stay out), runs the pure engine (base by frequency, pool incomes,
            valued overtime, registro deductions, ISSS/AFP/Renta, employer charges, minimum-income
            guarantee) and persists the run with its lines and totals; the eligible pool records are
            applied with origin `MOTOR` in the same transaction. Optional `employeeIds` restricts the
            population. The Nómina × period admits ONE active run — a second submit yields
            `409 PAYROLL_RUN_ALREADY_ACTIVE` (annul the active run to regenerate the slot); a pool record
            that changes mid-generation rolls everything back with `409 PAYROLL_RUN_POOL_CONFLICT`.
            Lagged not-worked-time/disciplinary inputs (REQ-014) join automatically with the
            `PAYROLL_WARNING_CARRYOVER_INPUT` warning.
            """)]
    public async Task<ActionResult<PayrollRunResponse>> Generate(
        Guid companyId,
        [FromBody] GeneratePayrollRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new GeneratePayrollRunCommand(
                companyId,
                request.PayrollDefinitionPublicId,
                request.PayrollPeriodPublicId,
                request.EmployeeIds),
            cancellationToken);

        // No GET endpoint exists until PR-6 — the created run travels complete in the body; Location is
        // deferred with it (201 with route would 500 on link generation).
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/payroll-runs/preflight")]
    [ProducesResponseType<PayrollRunPreflightResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Pre-flight a payroll run",
        Description = """
            Resolves the SAME inputs a generation would use — population, per-module input counts,
            projected totals and the stable warnings (missing Renta table, missing base salary, REQ-014
            lagged carryover inventory) — WITHOUT writing anything. This is the operator's preview and the
            adoption tool: the first pre-flight of a Nómina lists the historical TNT/disciplinary lags so
            anything already paid by the external payroll can be excluded after generating.
            """)]
    public async Task<ActionResult<PayrollRunPreflightResponse>> Preflight(
        Guid companyId,
        [FromBody] GeneratePayrollRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PreflightPayrollRunCommand(
                companyId,
                request.PayrollDefinitionPublicId,
                request.PayrollPeriodPublicId,
                request.EmployeeIds),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record GeneratePayrollRunRequest(
        Guid PayrollDefinitionPublicId,
        Guid PayrollPeriodPublicId,
        IReadOnlyCollection<Guid>? EmployeeIds = null);
}
