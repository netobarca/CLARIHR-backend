using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Absences;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// The company-wide not-worked-time bandeja and its exports (REQ-011). Deliberately NOT annotated with
/// [AuthorizationPolicySet]: the convention would treat the query's POST as a write, and it is a read. Everything
/// gates per handler on <c>ViewNotWorkedTimes</c>.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class NotWorkedTimesReportingController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/not-worked-times/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<NotWorkedTimeBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide not-worked-time bandeja",
        Description = """
            Paginated and filterable (employee, status, type, date range), with per-status counts and the totals per
            currency. **The counts and totals always span EVERY status** — they are the numbers of the tabs, so do
            not recompute them from `items`.
            """)]
    public async Task<ActionResult<NotWorkedTimeBandejaResponse>> Query(
        Guid companyId,
        [FromBody] QueryNotWorkedTimesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryNotWorkedTimesQuery(
                companyId,
                request.EmployeeId,
                request.StatusCode,
                request.TypeCode,
                request.From,
                request.To,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/not-worked-times/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(Summary = "Export the not-worked-time bandeja to Excel/CSV/JSON")]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] string? typeCode = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportNotWorkedTimesQuery(
                companyId, employeeId, statusCode, typeCode, from, to, reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(
                Result<IReadOnlyCollection<TiempoNoTrabajadoExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "not-worked-times",
            "NotWorkedTimes",
            AuditEntityTypes.PersonnelFile,
            "NOT_WORKED_TIMES",
            "Exported not-worked-time report.",
            new { employeeId, statusCode, typeCode, from, to },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/not-worked-times/payroll-input/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the payroll input of the not-worked time",
        Description = """
            One row per REGISTERED record with a discount, over the MANDATORY range (`startDate` + `endDate`; a
            missing bound → `422 NOT_WORKED_TIME_PAYROLL_INPUT_RANGE_REQUIRED`). **Annulled records are excluded** —
            the payroll must never discount money the company gave back — and so are the paid absences (they have no
            discount: they are documentation, not payroll input).
            """)]
    public async Task<IActionResult> ExportPayrollInput(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportNotWorkedTimePayrollInputQuery(
                companyId, startDate, endDate, reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(
                Result<IReadOnlyCollection<InsumoPlanillaTiempoNoTrabajadoExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "not-worked-time-payroll-input",
            "NotWorkedTimePayrollInput",
            AuditEntityTypes.PersonnelFile,
            "NOT_WORKED_TIME_PAYROLL_INPUT",
            "Exported not-worked-time payroll input.",
            new { startDate, endDate },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}

public sealed record QueryNotWorkedTimesRequest(
    Guid? EmployeeId,
    string? StatusCode,
    string? TypeCode,
    DateOnly? From,
    DateOnly? To,
    int? PageNumber,
    int? PageSize);
