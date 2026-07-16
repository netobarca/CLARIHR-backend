using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Domain.Payroll;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Payroll;

/// <summary>
/// El Salvador work-schedule template (REQ-012 §1.3 — the LEGAL week, golden 11):
/// <c>JORNADA_ORDINARIA</c> Mon-Fri 08:00-17:00 with a 12:00-13:00 meal (8 h/day) + Saturday 08:00-12:00
/// (4 h) = 44 h. Mirrors <c>OvertimeTemplateSeeder</c>: guarded existence checks (keyed on the tenant's
/// <c>NormalizedCode</c>) make it idempotent so it is safe to run on every provisioning and on every
/// <c>load-template</c> call. An existing row is skipped even when it was edited or inactivated, so the
/// seeder never overwrites tenant edits — it only creates the missing template rows.
/// </summary>
internal sealed class WorkScheduleTemplateSeeder(
    ApplicationDbContext dbContext,
    AmbientTenantContext ambientTenantContext) : IWorkScheduleTemplateSeeder
{
    public async Task<WorkScheduleTemplateSeedResult> ApplyTemplateAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Push the ambient tenant so the fail-closed global query filter scopes the idempotency guard below
        // to this tenant in every call path (the provisioning hook runs without an HTTP tenant claim).
        using var tenantScope = ambientTenantContext.Push(tenantId);

        var existingCodes = (await dbContext.Set<WorkSchedule>()
                .AsNoTracking()
                .Where(schedule => schedule.TenantId == tenantId)
                .Select(schedule => schedule.NormalizedCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;
        var skipped = 0;

        foreach (var template in Templates)
        {
            if (existingCodes.Contains(template.Code))
            {
                skipped++;
                continue;
            }

            var schedule = WorkSchedule.Create(
                template.Code,
                template.Name,
                template.ScheduleLabel,
                template.AttendanceDateAnchor,
                template.ScheduleClass,
                totalWeeklyHours: null,
                template.Days);
            schedule.SetTenantId(tenantId);
            dbContext.Set<WorkSchedule>().Add(schedule);
            created++;
        }

        if (created > 0)
        {
            _ = await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new WorkScheduleTemplateSeedResult(created, skipped);
    }

    // ---------------------------------------------------------------------------------------
    // Template data — El Salvador LEGAL week (44 h, golden 11 of the signed A.3): Mon-Fri 8 h net
    // (08:00-17:00 minus the 12:00-13:00 meal) + Saturday 4 h (08:00-12:00). TotalWeeklyHours is derived
    // from the days on purpose so the template can never drift from its own rows.
    // ---------------------------------------------------------------------------------------

    private static readonly WorkScheduleTemplate[] Templates =
    [
        new(
            "JORNADA_ORDINARIA",
            "Jornada ordinaria (semana legal 44 h)",
            "L-V 08:00-17:00 (comida 12:00-13:00) · sáb 08:00-12:00",
            WorkScheduleAnchors.Entrada,
            WorkScheduleClasses.Ordinaria,
            [
                new WorkScheduleDayInput(1, new TimeOnly(8, 0), new TimeOnly(17, 0), new TimeOnly(12, 0), new TimeOnly(13, 0)),
                new WorkScheduleDayInput(2, new TimeOnly(8, 0), new TimeOnly(17, 0), new TimeOnly(12, 0), new TimeOnly(13, 0)),
                new WorkScheduleDayInput(3, new TimeOnly(8, 0), new TimeOnly(17, 0), new TimeOnly(12, 0), new TimeOnly(13, 0)),
                new WorkScheduleDayInput(4, new TimeOnly(8, 0), new TimeOnly(17, 0), new TimeOnly(12, 0), new TimeOnly(13, 0)),
                new WorkScheduleDayInput(5, new TimeOnly(8, 0), new TimeOnly(17, 0), new TimeOnly(12, 0), new TimeOnly(13, 0)),
                new WorkScheduleDayInput(6, new TimeOnly(8, 0), new TimeOnly(12, 0), null, null),
            ]),
    ];

    private sealed record WorkScheduleTemplate(
        string Code,
        string Name,
        string ScheduleLabel,
        string AttendanceDateAnchor,
        string ScheduleClass,
        WorkScheduleDayInput[] Days);
}
