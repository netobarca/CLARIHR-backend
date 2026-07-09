using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Company-wide "otras transacciones de personal" bandejas, exports and the disciplinary payroll input (REQ-003
/// §3.9). Intentionally NOT annotated with [AuthorizationPolicySet]: the convention would assign the Manage
/// policy to the POST queries (READs), producing false 403s for view-only users; authorization is enforced per
/// handler via <c>EnsureCanViewRecognitionsAsync</c> / <c>EnsureCanViewDisciplinaryActionsAsync</c> (precedent:
/// settlements / incapacities / compensatory-time reporting). The recognitions/disciplinary bandejas EXCLUDE the
/// ANULADA records by default (opt in with <c>includeAnnulled</c> or an explicit <c>statusCode</c>); the payroll
/// input carries only APLICADA effects of a MANDATORY range (revoked records never travel — RN-14/RN-15).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class EmployeeRelationsReportingController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    // ── Recognitions ──────────────────────────────────────────────────────────────────────────────

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/recognitions/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecognitionBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide recognitions bandeja",
        Description = """
            Returns a paginated, filterable list of the company's recognitions (by employee, type code, status and
            event-date range) with per-status counts. The StatusCounts cover every status; the items EXCLUDE the
            ANULADA records by default (opt in with `includeAnnulled` or an explicit `statusCode`). HR-only
            (`ViewRecognitions`).
            """)]
    public async Task<ActionResult<RecognitionBandejaResponse>> QueryRecognitions(
        Guid companyId,
        [FromBody] QueryRecognitionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryRecognitionsQuery(
                companyId,
                request.EmployeeId,
                request.RecognitionTypeCode,
                request.StatusCode,
                request.FromDate,
                request.ToDate,
                request.IncludeAnnulled ?? false,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/recognitions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the recognitions bandeja to Excel/CSV/JSON",
        Description = """
            Exports the filtered recognitions (same filters as the bandeja) to `xlsx`, `csv` or `json` with the
            type, event date, detail, informational amount/currency, status and decision/registration audit. The
            ANULADA records are EXCLUDED by default (opt in with `includeAnnulled` or an explicit `statusCode`).
            HR-only. Synchronous download capped at the configured row limit (413 if exceeded).
            """)]
    public async Task<IActionResult> ExportRecognitions(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? recognitionTypeCode = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] bool includeAnnulled = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportRecognitionsQuery(
                companyId,
                employeeId,
                recognitionTypeCode,
                statusCode,
                fromDate,
                toDate,
                includeAnnulled,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<ReconocimientoExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "recognitions",
            "Recognitions",
            AuditEntityTypes.PersonnelFile,
            "RECOGNITIONS",
            "Exported recognitions report.",
            new { employeeId, recognitionTypeCode, statusCode, fromDate, toDate, includeAnnulled },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    // ── Disciplinary actions ──────────────────────────────────────────────────────────────────────

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/disciplinary-actions/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<DisciplinaryActionBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide disciplinary-actions bandeja",
        Description = """
            Returns a paginated, filterable list of the company's disciplinary actions (by employee, type code,
            cause code, status and incident-date range) with the deduction/suspension blocks and per-status
            counts. The StatusCounts cover every status; the items EXCLUDE the ANULADA records by default (opt in
            with `includeAnnulled` or an explicit `statusCode`). HR-only (`ViewDisciplinaryActions`).
            """)]
    public async Task<ActionResult<DisciplinaryActionBandejaResponse>> QueryDisciplinaryActions(
        Guid companyId,
        [FromBody] QueryDisciplinaryActionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryDisciplinaryActionsQuery(
                companyId,
                request.EmployeeId,
                request.DisciplinaryActionTypeCode,
                request.CauseCode,
                request.StatusCode,
                request.FromDate,
                request.ToDate,
                request.IncludeAnnulled ?? false,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/disciplinary-actions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the disciplinary-actions bandeja to Excel/CSV/JSON",
        Description = """
            Exports the filtered disciplinary actions (same filters as the bandeja) to `xlsx`, `csv` or `json`
            with the cause, incident date, facts, deduction block (amount + snapshotted concept), suspension range
            + days, status and decision/registration audit. The ANULADA records are EXCLUDED by default (opt in
            with `includeAnnulled` or an explicit `statusCode`). HR-only. Synchronous download capped at the
            configured row limit (413 if exceeded).
            """)]
    public async Task<IActionResult> ExportDisciplinaryActions(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? disciplinaryActionTypeCode = null,
        [FromQuery] string? causeCode = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] bool includeAnnulled = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportDisciplinaryActionsQuery(
                companyId,
                employeeId,
                disciplinaryActionTypeCode,
                causeCode,
                statusCode,
                fromDate,
                toDate,
                includeAnnulled,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<AmonestacionExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "disciplinary-actions",
            "DisciplinaryActions",
            AuditEntityTypes.PersonnelFile,
            "DISCIPLINARY_ACTIONS",
            "Exported disciplinary actions report.",
            new { employeeId, disciplinaryActionTypeCode, causeCode, statusCode, fromDate, toDate, includeAnnulled },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    // ── Payroll input (RF-012) ──────────────────────────────────────────────────────────────────────

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/disciplinary-actions/payroll-input/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the disciplinary payroll input to Excel/CSV/JSON",
        Description = """
            Exports the payroll input (RF-012) for a MANDATORY date range: only the APLICADA disciplinary actions
            whose incident date falls in the range with an effect, one row per effect — `DESCUENTO` (concept +
            amount + currency) or `SUSPENSION_SIN_GOCE` (suspension range + days). Revoked (ANULADA) records never
            travel (RN-14/RN-15). A missing or incoherent range is rejected with 422
            `PERSONNEL_TRANSACTION_RANGE_REQUIRED`. Requires `ViewDisciplinaryActions`. Synchronous download
            capped at the configured row limit (413 if exceeded).
            """)]
    public async Task<IActionResult> ExportPayrollInput(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportPayrollInputQuery(
                companyId,
                startDate,
                endDate,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<InsumoPlanillaExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "disciplinary-actions-payroll-input",
            "DisciplinaryActionsPayrollInput",
            AuditEntityTypes.PersonnelFile,
            "DISCIPLINARY_ACTIONS_PAYROLL_INPUT",
            "Exported disciplinary payroll input report.",
            new { startDate, endDate },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}
