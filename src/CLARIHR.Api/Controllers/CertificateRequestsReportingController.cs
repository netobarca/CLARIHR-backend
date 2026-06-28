using CLARIHR.Api.Common;
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
/// Company-wide certificate-request bandeja and export (D-08) — the consultation that names the requirement. This
/// is HR-only (the employee uses the per-file list). Intentionally NOT annotated with [AuthorizationPolicySet]:
/// the convention would assign the Manage policy to the POST query (a READ), producing false 403s for view-only
/// users; authorization is enforced per handler via <c>EnsureCanViewCertificateRequestsAsync</c>.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class CertificateRequestsReportingController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/certificate-requests/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<CertificateRequestBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide certificate-request bandeja",
        Description = """
            Returns a paginated, filterable list of all certificate requests of the company (by type, status,
            purpose, employee, request-date range and free text), plus per-status counts. HR-only
            (`ViewCertificateRequests`). The employee uses the per-file list instead.
            """)]
    public async Task<ActionResult<CertificateRequestBandejaResponse>> QueryCertificateRequests(
        Guid companyId,
        [FromBody] QueryCertificateRequestsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryCertificateRequestsQuery(
                companyId,
                request.TypeCode,
                request.StatusCode,
                request.PurposeCode,
                request.EmployeeId,
                request.FromUtc,
                request.ToUtc,
                request.Search,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/certificate-requests/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the certificate-request bandeja to Excel/CSV",
        Description = """
            Exports the filtered certificate-request list (same filters as the bandeja) to `xlsx`, `csv` or `json`.
            HR-only. Synchronous download capped at the configured row limit (`413` if exceeded).
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] string? typeCode = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] string? purposeCode = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportCertificateRequestsQuery(
                companyId,
                typeCode,
                statusCode,
                purposeCode,
                employeeId,
                fromUtc,
                toUtc,
                search,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<CertificateRequestExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "certificate-requests",
            "CertificateRequests",
            AuditEntityTypes.PersonnelFile,
            "CERTIFICATE_REQUESTS",
            "Exported certificate requests report.",
            new
            {
                typeCode,
                statusCode,
                purposeCode,
                employeeId,
                fromUtc,
                toUtc,
                q = search
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}
