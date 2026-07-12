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
/// Company-wide one-time-deduction operations (REQ-009): the pending work list and the apply-period batch (PR-4);
/// the bandeja + exports arrive in PR-5. Intentionally NOT annotated with [AuthorizationPolicySet] (the convention
/// would assign a Manage policy to the POST that is really a READ); the reads enforce
/// <c>ViewOneTimeDeductions</c> per handler and the batch enforces <c>ManageOneTimeDeductions</c> per handler.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class OneTimeDeductionsReportingController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/one-time-deductions/pending/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionPendingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the one-time deductions still waiting to be charged",
        Description = """
            The payroll operator's work list: every `AUTORIZADO` deduction of the company that has not been charged
            yet, optionally narrowed by payroll type, payroll period or employee. A deduction whose target period
            has already CLOSED is flagged `isOverdue` — it should have been charged and was not. HR-only
            (`ViewOneTimeDeductions`).
            """)]
    public async Task<ActionResult<OneTimeDeductionPendingResponse>> QueryOneTimeDeductionPending(
        Guid companyId,
        [FromBody] QueryOneTimeDeductionPendingRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryOneTimeDeductionPendingQuery(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.EmployeeId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/one-time-deductions/apply-period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionApplyPeriodResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Charge the pending one-time deductions of a payroll period",
        Description = """
            Charges, ATOMICALLY, every `AUTORIZADO` deduction of the company for the given `payrollTypeCode`
            (optionally narrowed to a payroll period). `excludedDeductionPublicIds` postpones deductions — they
            stay pending for the next run. Any conflict rolls the WHOLE batch back (422); there are no partial
            successes. HR-only (`ManageOneTimeDeductions`).
            """)]
    public async Task<ActionResult<OneTimeDeductionApplyPeriodResult>> ApplyOneTimeDeductionPeriod(
        Guid companyId,
        [FromBody] ApplyOneTimeDeductionPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyOneTimeDeductionPeriodCommand(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.ExcludedDeductionPublicIds ?? []),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/one-time-deductions/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide one-time-deduction bandeja",
        Description = """
            Returns a paginated, filterable list of the company's one-off deductions (by employee, status, concept,
            payroll type and date range) with their value, requester and payroll destination, plus per-status
            counts and the totals per currency. Every status is listed by default; the counts and totals always
            cover EVERY status. HR-only (`ViewOneTimeDeductions`).
            """)]
    public async Task<ActionResult<OneTimeDeductionBandejaResponse>> QueryOneTimeDeductions(
        Guid companyId,
        [FromBody] QueryOneTimeDeductionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryOneTimeDeductionsQuery(
                companyId,
                request.EmployeeId,
                request.StatusCode,
                request.ConceptTypeCode,
                request.PayrollTypeCode,
                request.DeductionFrom,
                request.DeductionTo,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/one-time-deductions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the one-time-deduction bandeja to Excel/CSV/JSON",
        Description = "Exports the filtered one-off deductions (same filters as the bandeja). HR-only. Synchronous download capped at the configured row limit (`413` if exceeded).")]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] string? conceptTypeCode = null,
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] DateOnly? deductionFrom = null,
        [FromQuery] DateOnly? deductionTo = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportOneTimeDeductionsQuery(
                companyId, employeeId, statusCode, conceptTypeCode, payrollTypeCode,
                deductionFrom, deductionTo, reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<DescuentoEventualExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "one-time-deductions",
            "OneTimeDeductions",
            AuditEntityTypes.PersonnelFile,
            "ONE_TIME_DEDUCTIONS",
            "Exported one-time deductions report.",
            new { employeeId, statusCode, conceptTypeCode, payrollTypeCode, deductionFrom, deductionTo },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/one-time-deductions/payroll-input/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the payroll input of the charged one-time deductions",
        Description = """
            The handoff to the external payroll system: one row per CHARGED deduction whose applied date falls in
            the MANDATORY range (`startDate` + `endDate`; a missing bound → `422`
            `ONE_TIME_DEDUCTION_PAYROLL_INPUT_RANGE_REQUIRED`). REVERTED applications are excluded, so the export
            reflects exactly what was actually charged. HR-only.
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
            new ExportOneTimeDeductionPayrollInputQuery(
                companyId, payrollTypeCode, startDate, endDate, reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<InsumoPlanillaDescuentoEventualExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "one-time-deduction-payroll-input",
            "OneTimeDeductionPayrollInput",
            AuditEntityTypes.PersonnelFile,
            "ONE_TIME_DEDUCTION_PAYROLL_INPUT",
            "Exported one-time-deduction payroll input.",
            new { payrollTypeCode, startDate, endDate },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}
