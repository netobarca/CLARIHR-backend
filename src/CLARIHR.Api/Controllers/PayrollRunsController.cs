using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Payroll-run generation and review (REQ-012 §3.4/§3.6 — PR-5: generate + pre-flight; PR-6: read,
/// per-line adjustment, selective recalculation, regeneration, closure and annulment; the bandeja/
/// reporting arrives in PR-7). The authorize/return pair lives in <see cref="PayrollRunResolutionController"/>
/// because its writes map to the dedicated AuthorizePayrollRuns grant. The precise HR gate lives in the
/// handlers; the marker pair is the declarative defense-in-depth.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Payroll Runs")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewPayrollRuns, PersonnelFilePolicies.ManagePayrollRuns)]
public sealed class PayrollRunsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
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

        // The PublicContractRouteConvention rewrites `{companyId}` → `{companyPublicId}` and
        // `{payrollRunId}` → `{publicId}` on the GetById route, so the Location values use THOSE keys.
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { companyPublicId = companyId, publicId = value.Id },
            value => value.ConcurrencyToken);
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

    [HttpGet("companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}")]
    [ProducesResponseType<PayrollRunResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Get a payroll run",
        Description = """
            The run header: definition/period snapshots, lifecycle status (`GENERADA` → `AUTORIZADA` →
            `CERRADA`; `ANULADA` pre-closure), persisted totals, regeneration count and the generation's
            stable warnings. The response `ETag` carries the `concurrencyToken` every review/decision
            endpoint requires via `If-Match`.
            """)]
    public async Task<ActionResult<PayrollRunResponse>> GetById(
        Guid companyId,
        Guid payrollRunId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPayrollRunByIdQuery(companyId, payrollRunId),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpGet("companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}/employees/{personnelFilePublicId:guid}")]
    [ProducesResponseType<PayrollRunEmployeeLinesResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Get an employee's lines of a payroll run",
        Description = """
            The per-employee drill of the run (REQ-013 RF-002): every line — income, deduction and employer
            charge — with its concept, units, base, calculated amount, audited override, inclusion flag and
            line→source traceability (`sourceModule` + `sourceReferencePublicId`), plus the employee's
            income/deduction/net totals over the INCLUDED lines.
            """)]
    public async Task<ActionResult<PayrollRunEmployeeLinesResponse>> GetEmployeeLines(
        Guid companyId,
        Guid payrollRunId,
        Guid personnelFilePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPayrollRunEmployeeLinesQuery(companyId, payrollRunId, personnelFilePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}/lines/{linePublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PayrollRunResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Adjust a payroll run line",
        Description = """
            Review adjustment of one line — only while the run is `GENERADA`: an audited `overrideAmount`
            with its MANDATORY `overrideNote` (send `clearOverride: true` to remove it) and/or the
            `isIncluded` flag. EXCLUDING a pool-backed line (pool incomes/deductions) annuls its `MOTOR`
            application in the same transaction — the source record is released and re-appears as a
            candidate (REQ-014 RF-007); re-including re-applies it. Law lines (ISSS/AFP/Renta and employer
            charges) and the overtime aggregate's inclusion admit no adjustment
            (`422 PAYROLL_RUN_LINE_NOT_ADJUSTABLE`). Requires `If-Match` with the run's current
            `concurrencyToken`; totals are recomputed and returned.
            """)]
    public async Task<ActionResult<PayrollRunResponse>> AdjustLine(
        Guid companyId,
        Guid payrollRunId,
        Guid linePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AdjustPayrollRunLineRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AdjustPayrollRunLineCommand(
                companyId,
                payrollRunId,
                linePublicId,
                request.ClearOverride || request.OverrideAmount.HasValue,
                request.ClearOverride ? null : request.OverrideAmount,
                request.OverrideNote,
                request.IsIncluded,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}/recalculation")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PayrollRunResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Recalculate employees of a payroll run",
        Description = """
            Selective recalculation — only while `GENERADA`: reverts the given employees' `MOTOR` pool
            applications, re-derives their lines from the CURRENT inputs and re-applies, keeping every
            other employee untouched. Audited overrides of the recalculated employees are preserved when
            the same concept/source re-emerges. Requires `If-Match` with the run's current
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<PayrollRunResponse>> Recalculate(
        Guid companyId,
        Guid payrollRunId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RecalculatePayrollRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RecalculatePayrollRunCommand(companyId, payrollRunId, request.EmployeeIds, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}/regeneration")]
    [Produces("application/json")]
    [ProducesResponseType<PayrollRunResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Regenerate a payroll run",
        Description = """
            Full regeneration — only while `GENERADA`: every `MOTOR` pool application of the period is
            annulled (the pools end as if the run never existed), the run is rebuilt from the CURRENT
            inputs and the pools re-apply. Review adjustments (overrides/exclusions) are deliberately
            discarded; `regeneratedCount` increments. Requires `If-Match` with the run's current
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<PayrollRunResponse>> Regenerate(
        Guid companyId,
        Guid payrollRunId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RegeneratePayrollRunCommand(companyId, payrollRunId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}/closure")]
    [Produces("application/json")]
    [ProducesResponseType<PayrollRunResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Close a payroll run",
        Description = """
            `AUTORIZADA` → `CERRADA` (terminal — REQ-013 P-01 second step): the payment cycle ends and the
            run's PERIOD closes in the SAME transaction; no further generation, adjustment or annulment is
            possible for either. Requires `If-Match` with the run's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PayrollRunResponse>> Close(
        Guid companyId,
        Guid payrollRunId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ClosePayrollRunCommand(companyId, payrollRunId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PayrollRunResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul a payroll run",
        Description = """
            Pre-closure annulment (`GENERADA`/`AUTORIZADA` → `ANULADA`, terminal) with a MANDATORY reason:
            every `MOTOR` pool application the run made is annulled symmetrically — the source records
            return to their pre-generation state and become candidates again — and the one-active-run slot
            of the Nómina × period is released (a new generation is allowed). A `CERRADA` run cannot be
            annulled. Requires `If-Match` with the run's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PayrollRunResponse>> Annul(
        Guid companyId,
        Guid payrollRunId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulPayrollRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulPayrollRunCommand(companyId, payrollRunId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record GeneratePayrollRunRequest(
        Guid PayrollDefinitionPublicId,
        Guid PayrollPeriodPublicId,
        IReadOnlyCollection<Guid>? EmployeeIds = null);

    public sealed record AdjustPayrollRunLineRequest(
        decimal? OverrideAmount = null,
        string? OverrideNote = null,
        bool? IsIncluded = null,
        bool ClearOverride = false);

    public sealed record RecalculatePayrollRunRequest(IReadOnlyCollection<Guid> EmployeeIds);

    public sealed record AnnulPayrollRunRequest(string Reason);
}
