namespace CLARIHR.Application.Abstractions.Payroll;

/// <summary>Summary of one template application (created vs skipped — idempotent by normalized code).</summary>
public sealed record WorkScheduleTemplateSeedResult(int WorkSchedulesCreated, int WorkSchedulesSkipped);

/// <summary>
/// Seeds the El Salvador work-schedule template (REQ-012 §1.3 — the LEGAL week: 44 h, golden 11).
/// Idempotent by the tenant's <c>NormalizedCode</c>: existing rows are skipped, never overwritten
/// (mirrors <c>IOvertimeTemplateSeeder</c>). Runs on provisioning and on
/// <c>POST …/payroll-configuration/load-template</c>.
/// </summary>
public interface IWorkScheduleTemplateSeeder
{
    Task<WorkScheduleTemplateSeedResult> ApplyTemplateAsync(Guid tenantId, CancellationToken cancellationToken);
}
