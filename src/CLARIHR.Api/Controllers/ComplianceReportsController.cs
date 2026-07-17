using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// The payroll legal-compliance reports — F-14, Planilla Única (REQ-016 RF-001/RF-002). Trans-run by
/// design (a calendar month can span more than one `CERRADA` run — RN-03), unlike the per-run reports of
/// <see cref="PayrollRunsReportingController"/>. Deliberately NOT annotated with [AuthorizationPolicySet]:
/// every handler gates individually on the dedicated <c>ViewComplianceReports</c> permission (P-10), not
/// <c>ViewPayrollRuns</c> — these reports concentrate NIT/DUI/NUP ISSS/AFP account of the whole payroll.
/// </summary>
[ApiController]
[Authorize]
[Tags("Compliance Reports")]
public sealed class ComplianceReportsController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/compliance-reports/income-tax-withholding/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the F-14 report (monthly income-tax withholding)",
        Description = """
            REQ-016 RF-001 — one row per employee, consolidated across every `CERRADA` payroll run whose
            period falls in the given calendar month (a quincenal/semanal Nómina can contribute more than
            one run to the same month — RN-03/P-01/P-09). An employee without a registered NIT still
            appears, with `Advertencias` set instead of blocking the report (RN-06). Empty month → empty
            file, not an error. **Layout**: flat tabular detail for now — the real F-14 official-template
            mapping (P-02 of the analysis) is not yet available; this endpoint will switch to it once the
            business supplies the template.
            """)]
    public async Task<IActionResult> ExportIncomeTaxWithholding(
        Guid companyId,
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string format = "xlsx",
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportIncomeTaxWithholdingReportQuery(companyId, year, month),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<F14ExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            $"f14-{year:D4}-{month:D2}",
            "IncomeTaxWithholding",
            AuditEntityTypes.PayrollRun,
            "F14_INCOME_TAX_WITHHOLDING",
            $"Exported the F-14 report for {year:D4}-{month:D2}.",
            new { year, month },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/compliance-reports/social-security-contributions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the Planilla Única report (monthly ISSS + AFP contributions)",
        Description = """
            REQ-016 RF-002 — one row per employee, consolidated across every `CERRADA` payroll run whose
            period falls in the given calendar month (same consolidation as F-14 — RN-03/P-01/P-09). NUP
            ISSS and AFP account number are obligatorios (P-04); a row missing either still appears, with
            `Advertencias` set (RN-06) — once Gate B (RF-007) is active for the tenant this should not
            happen in practice. **Layout**: flat tabular detail for now, same caveat as F-14 (P-02).
            """)]
    public async Task<IActionResult> ExportSocialSecurityContributions(
        Guid companyId,
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string format = "xlsx",
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportSocialSecurityContributionReportQuery(companyId, year, month),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PlanillaUnicaExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            $"planilla-unica-{year:D4}-{month:D2}",
            "SocialSecurityContributions",
            AuditEntityTypes.PayrollRun,
            "PLANILLA_UNICA_SOCIAL_SECURITY_CONTRIBUTIONS",
            $"Exported the Planilla Única report for {year:D4}-{month:D2}.",
            new { year, month },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}
