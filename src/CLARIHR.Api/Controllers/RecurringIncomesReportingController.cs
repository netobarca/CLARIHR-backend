using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Company-wide recurring-income operations ("planilla — ingresos cíclicos", REQ-005): the apply-period batch
/// (PR-4), the bandeja + exports, the pending-installments bandeja + export and the payroll-input export (PR-5).
/// Intentionally NOT annotated with [AuthorizationPolicySet] (the convention would assign a policy to every
/// action); the reads enforce <c>ViewRecurringIncomes</c> per handler and the apply-period write enforces the
/// <c>ManageRecurringIncomes</c> grant per handler (precedent: settlements / incapacities reporting).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class RecurringIncomesReportingController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/recurring-incomes/apply-period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeApplyPeriodResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Apply the due recurring-income installments of a payroll period",
        Description = """
            Applies, ATOMICALLY, every due installment of the company's `VIGENTE` recurring incomes of the given
            `payrollTypeCode` up to the period cutoff (RF-007) — including overdue installments carried over from
            previous periods. Provide a `payrollPeriodPublicId` (its end date is the cutoff and its id/label are
            snapshotted onto the installments) or a bare `cutoffDate`. `excludedIncomePublicIds` postpones incomes
            (they stay due for the next run). Any conflict rolls the whole batch back (422). HR-only
            (`ManageRecurringIncomes`). Returns the count of applied installments, finalized incomes and postponed
            incomes.
            """)]
    public async Task<ActionResult<RecurringIncomeApplyPeriodResult>> ApplyPeriod(
        Guid companyId,
        [FromBody] ApplyRecurringIncomePeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyRecurringIncomePeriodCommand(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.CutoffDate,
                request.ExcludedIncomePublicIds ?? []),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/recurring-incomes/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide recurring-income bandeja",
        Description = """
            Returns a paginated, filterable list of the company's recurring incomes (by employee, status,
            recurring-income type, payroll type and registration-date range) with the plan header, plus per-status
            counts. Every status is listed by default — annulled and rejected incomes are included with their
            status; the StatusCounts cover every status. HR-only (`ViewRecurringIncomes`).
            """)]
    public async Task<ActionResult<RecurringIncomeBandejaResponse>> QueryRecurringIncomes(
        Guid companyId,
        [FromBody] QueryRecurringIncomesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryRecurringIncomesQuery(
                companyId,
                request.EmployeeId,
                request.StatusCode,
                request.RecurringIncomeTypeCode,
                request.PayrollTypeCode,
                request.RegisteredFromUtc,
                request.RegisteredToUtc,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/recurring-incomes/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the recurring-income bandeja to Excel/CSV/JSON",
        Description = """
            Exports the filtered recurring incomes (same filters as the bandeja) to `xlsx`, `csv` or `json` with the
            plan header, settlement action, status, currency and the registrar / decider ids. Annulled and rejected
            incomes are included (filter by status to narrow). HR-only. Synchronous download capped at the configured
            row limit (`413` if exceeded).
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] string? recurringIncomeTypeCode = null,
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] DateTime? registeredFromUtc = null,
        [FromQuery] DateTime? registeredToUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportRecurringIncomesQuery(
                companyId,
                employeeId,
                statusCode,
                recurringIncomeTypeCode,
                payrollTypeCode,
                registeredFromUtc,
                registeredToUtc,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<IngresoCiclicoExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "recurring-incomes",
            "RecurringIncomes",
            AuditEntityTypes.PersonnelFile,
            "RECURRING_INCOMES",
            "Exported recurring incomes report.",
            new
            {
                employeeId,
                statusCode,
                recurringIncomeTypeCode,
                payrollTypeCode,
                registeredFromUtc,
                registeredToUtc
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/recurring-incomes/pending-installments/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomePendingInstallmentsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide pending / overdue recurring-income installments",
        Description = """
            Returns a paginated list of the THEORETICAL installments of the company's `VIGENTE` recurring incomes
            whose due date is on/before the cutoff and are NOT yet applied (RF-011) — the F1 approximation of the
            "transactions not applied in payroll" backlog. The cutoff is the `payrollPeriodPublicId` end date, the
            bare `cutoffDate`, or today; `startDate` narrows the lower bound; `payrollTypeCode` / `employeeId` scope
            the scan. Overdue installments (theoretical due < today) are flagged. HR-only (`ViewRecurringIncomes`).
            """)]
    public async Task<ActionResult<RecurringIncomePendingInstallmentsResponse>> QueryPendingInstallments(
        Guid companyId,
        [FromBody] QueryPendingRecurringIncomeInstallmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryPendingRecurringIncomeInstallmentsQuery(
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
    [HttpGet("api/v1/companies/{companyId:guid}/recurring-incomes/pending-installments/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the pending / overdue recurring-income installments to Excel/CSV/JSON",
        Description = """
            Exports the projected pending / overdue installments (same filters as the pending bandeja) to `xlsx`,
            `csv` or `json` with the theoretical due date, amount, currency and the overdue flag. HR-only.
            Synchronous download capped at the configured row limit (`413` if exceeded).
            """)]
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
            new ExportPendingRecurringIncomeInstallmentsQuery(
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
            return this.ToActionResult(Result<IReadOnlyCollection<CuotaPendienteCiclicaExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "recurring-income-pending-installments",
            "PendingInstallments",
            AuditEntityTypes.PersonnelFile,
            "RECURRING_INCOME_PENDING_INSTALLMENTS",
            "Exported recurring-income pending installments report.",
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
    [HttpGet("api/v1/companies/{companyId:guid}/recurring-incomes/payroll-input/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Export the recurring-income payroll input for an external payroll system",
        Description = """
            Exports the APPLIED (`APLICADA`, active) installments of the MANDATORY `startDate`..`endDate` range
            (over the applied date), optionally scoped to a `payrollTypeCode` (RF-012 / §5) — one row per
            installment with the employee, concept, payroll type, imputed period, applied date, number, amount,
            currency and cost center. This is the bridge with the external payroll while the internal engine does
            not exist; it cuadra EXACTLY against the pending installments of the same filter once applied. A missing
            range bound yields `422`. HR-only. Synchronous download capped at the configured row limit (`413`).
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
            new ExportRecurringIncomePayrollInputQuery(
                companyId,
                payrollTypeCode,
                startDate,
                endDate,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<InsumoPlanillaCiclicoExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "recurring-income-payroll-input",
            "PayrollInput",
            AuditEntityTypes.PersonnelFile,
            "RECURRING_INCOME_PAYROLL_INPUT",
            "Exported recurring-income payroll input.",
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
