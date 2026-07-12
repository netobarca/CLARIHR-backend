using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Company-wide recurring-deduction operations ("planilla — descuentos cíclicos", REQ-008): the apply-period batch
/// (PR-4) and — added in PR-5 — the bandeja + exports, the pending-charges bandeja and the payroll-input export.
/// Intentionally NOT annotated with [AuthorizationPolicySet] (the convention would assign a policy to every
/// action); the apply-period write enforces the <c>ManageRecurringDeductions</c> grant per handler (precedent: the
/// recurring-income reporting controller).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class RecurringDeductionsReportingController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/recurring-deductions/apply-period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionApplyPeriodResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Apply the due recurring-deduction charges of a payroll period",
        Description = """
            Applies, ATOMICALLY, every due charge of the company's `VIGENTE` credits of the given `payrollTypeCode`
            up to the period cutoff (RF-007) — including overdue charges carried over from previous periods. The
            charges advance by each credit's APPLICATION cadence (a monthly quota charged fortnightly yields two
            half charges) and skip its exception months; a credit whose `effectiveDate` has not been reached is not
            a candidate. Provide a `payrollPeriodPublicId` (its end date is the cutoff and its id/label are
            snapshotted onto the charges) or a bare `cutoffDate`. `excludedDeductionPublicIds` postpones credits
            (they stay due for the next run). Credits whose plan completes in the run are FINALIZED. Any conflict
            rolls the WHOLE batch back (422). HR-only (`ManageRecurringDeductions`).
            """)]
    public async Task<ActionResult<RecurringDeductionApplyPeriodResult>> ApplyRecurringDeductionPeriod(
        Guid companyId,
        [FromBody] ApplyRecurringDeductionPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyRecurringDeductionPeriodCommand(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.CutoffDate,
                request.ExcludedDeductionPublicIds ?? []),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/recurring-deductions/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide recurring-deduction bandeja",
        Description = """
            Returns a paginated, filterable list of the company's credits (by employee, status, deduction type,
            payroll type and effective-date range) with the plan header, the creditor, and the DERIVED
            `totalCharged` ("total cobrado") / `totalOutstanding` ("total no cobrado") per credit. Every status is
            listed by default — annulled and rejected credits are included with their status; the StatusCounts
            cover every status, and the per-currency totals cover the WHOLE filtered set (not just the page).
            HR-only (`ViewRecurringDeductions`).
            """)]
    public async Task<ActionResult<RecurringDeductionBandejaResponse>> QueryRecurringDeductions(
        Guid companyId,
        [FromBody] QueryRecurringDeductionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryRecurringDeductionsQuery(
                companyId,
                request.EmployeeId,
                request.StatusCode,
                request.RecurringDeductionTypeCode,
                request.PayrollTypeCode,
                request.EffectiveFrom,
                request.EffectiveTo,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/recurring-deductions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the recurring-deduction bandeja to Excel/CSV/JSON",
        Description = """
            Exports the filtered credits (same filters as the bandeja) to `xlsx`, `csv` or `json` with the plan
            header, the creditor, the interest parameters and the derived charged / outstanding totals. Annulled
            and rejected credits are included (filter by status to narrow). HR-only. Synchronous download capped at
            the configured row limit (`413` if exceeded).
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] string? recurringDeductionTypeCode = null,
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] DateOnly? effectiveFrom = null,
        [FromQuery] DateOnly? effectiveTo = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportRecurringDeductionsQuery(
                companyId,
                employeeId,
                statusCode,
                recurringDeductionTypeCode,
                payrollTypeCode,
                effectiveFrom,
                effectiveTo,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<DescuentoCiclicoExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "recurring-deductions",
            "RecurringDeductions",
            AuditEntityTypes.PersonnelFile,
            "RECURRING_DEDUCTIONS",
            "Exported recurring deductions report.",
            new
            {
                employeeId,
                statusCode,
                recurringDeductionTypeCode,
                payrollTypeCode,
                effectiveFrom,
                effectiveTo
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/recurring-deductions/pending-installments/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionPendingInstallmentsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the pending / overdue charges of the company's credits",
        Description = """
            Returns the THEORETICAL charges of the company's `VIGENTE` credits that are due on/before the cutoff and
            are not yet applied — including OVERDUE ones carried over from previous periods (`isOverdue`). It is a
            pure derivation (never persisted) and it reuses the very projection the apply-period batch consumes, so
            what you see here is exactly what the batch would apply. The charges advance by each credit's
            APPLICATION cadence and skip its exception months; a credit whose effective date has not been reached
            contributes nothing. HR-only (`ViewRecurringDeductions`).
            """)]
    public async Task<ActionResult<RecurringDeductionPendingInstallmentsResponse>> QueryPendingRecurringDeductionInstallments(
        Guid companyId,
        [FromBody] QueryPendingRecurringDeductionInstallmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryPendingRecurringDeductionInstallmentsQuery(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.CutoffDate,
                request.StartDate,
                request.EmployeeId,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/recurring-deductions/pending-installments/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the pending / overdue charges to Excel/CSV/JSON",
        Description = "Exports the same projection as the pending-charges bandeja (same filters), with the creditor and the capital/interest split. HR-only.")]
    public async Task<IActionResult> ExportPendingInstallments(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] Guid? payrollPeriodPublicId = null,
        [FromQuery] DateOnly? cutoffDate = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] Guid? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportPendingRecurringDeductionInstallmentsQuery(
                companyId,
                payrollTypeCode,
                payrollPeriodPublicId,
                cutoffDate,
                startDate,
                employeeId,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<CuotaPendienteDescuentoExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "recurring-deduction-pending-installments",
            "RecurringDeductionPendingInstallments",
            AuditEntityTypes.PersonnelFile,
            "RECURRING_DEDUCTION_PENDING_INSTALLMENTS",
            "Exported pending recurring-deduction installments report.",
            new
            {
                payrollTypeCode,
                payrollPeriodPublicId,
                cutoffDate,
                startDate,
                employeeId
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/recurring-deductions/payroll-input/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the payroll input of the applied recurring-deduction charges",
        Description = """
            The handoff to the external payroll system: one row per APPLIED charge whose applied date falls in the
            MANDATORY range (`startDate` + `endDate`; a missing bound → `422`
            `RECURRING_DEDUCTION_PAYROLL_INPUT_RANGE_REQUIRED`). Each row carries the CREDITOR (financial
            institution), the credit reference and the capital/interest split, so the operator knows whom to pay.
            EXTRAORDINARY payments are included — the payroll must discount them too. Annulled charges are excluded,
            so this export cuadra exactly against the pending-charges bandeja of the same filter once applied.
            HR-only.
            """)]
    public async Task<IActionResult> ExportPayrollInput(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportRecurringDeductionPayrollInputQuery(
                companyId,
                payrollTypeCode,
                startDate,
                endDate,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<InsumoPlanillaDescuentoExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "recurring-deduction-payroll-input",
            "RecurringDeductionPayrollInput",
            AuditEntityTypes.PersonnelFile,
            "RECURRING_DEDUCTION_PAYROLL_INPUT",
            "Exported recurring-deduction payroll input.",
            new
            {
                payrollTypeCode,
                startDate,
                endDate
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}
