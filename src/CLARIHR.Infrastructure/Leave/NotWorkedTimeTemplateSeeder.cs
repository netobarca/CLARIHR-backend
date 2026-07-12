using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Domain.Leave;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Leave;

/// <summary>
/// The El Salvador not-worked-time TYPE template (REQ-011 D-18). Molded on <see cref="LeaveTemplateSeeder"/>:
/// idempotent by <c>NormalizedCode</c>, so an existing row is SKIPPED — never overwritten — even when the company
/// edited or inactivated it. That is what makes it safe to run on every provisioning and on every load-template.
/// </summary>
internal sealed class NotWorkedTimeTemplateSeeder(
    ApplicationDbContext dbContext,
    AmbientTenantContext ambientTenantContext) : INotWorkedTimeTemplateSeeder
{
    /// <summary>The four F1 types of the levantamiento.</summary>
    private static readonly NotWorkedTimeTypeTemplate[] Templates =
    [
        // Unpaid absence: nothing counts (the employee simply was not there) and the week costs them their paid
        // day of rest too — that is the "séptimo" (P-18).
        new("AUSENCIA_SIN_GOCE", "Ausencia sin goce de sueldo",
            AppliesToPermission: true, UsesWorkSchedule: false,
            CountsHoliday: false, CountsSaturday: false, CountsRestDay: false, CountsSeventhDayPenalty: true,
            DiscountPercent: 100m, DeductionConceptTypeCode: "AUSENCIA_SIN_GOCE", IncomeConceptTypeCode: null),

        // Paid absence: recorded for the record, but the money is NOT touched (0% ⇒ no deduction concept needed).
        new("AUSENCIA_CON_GOCE", "Ausencia con goce de sueldo",
            AppliesToPermission: true, UsesWorkSchedule: false,
            CountsHoliday: true, CountsSaturday: true, CountsRestDay: true, CountsSeventhDayPenalty: false,
            DiscountPercent: 0m, DeductionConceptTypeCode: null, IncomeConceptTypeCode: null),

        new("SUSPENSION_CON_DESCUENTO", "Suspensión con descuento",
            AppliesToPermission: false, UsesWorkSchedule: false,
            CountsHoliday: false, CountsSaturday: false, CountsRestDay: false, CountsSeventhDayPenalty: true,
            DiscountPercent: 100m, DeductionConceptTypeCode: "SUSPENSION_SIN_GOCE", IncomeConceptTypeCode: null),

        // Captured in HOURS: two late hours are a quarter of a day, not a day (§2 regla 5).
        new("LLEGADA_TARDIA", "Llegada tardía",
            AppliesToPermission: false, UsesWorkSchedule: true,
            CountsHoliday: false, CountsSaturday: false, CountsRestDay: false, CountsSeventhDayPenalty: false,
            DiscountPercent: 100m, DeductionConceptTypeCode: "LLEGADA_TARDIA", IncomeConceptTypeCode: null),
    ];

    public async Task<NotWorkedTimeTemplateSeedResult> ApplyTemplateAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Push the ambient tenant: the provisioning hook runs with no HTTP tenant claim.
        using var tenantScope = ambientTenantContext.Push(tenantId);

        var existingCodes = await dbContext.NotWorkedTimeTypes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.NormalizedCode)
            .ToListAsync(cancellationToken);

        var existing = existingCodes.ToHashSet(StringComparer.Ordinal);

        var created = 0;
        var skipped = 0;

        foreach (var template in Templates)
        {
            if (existing.Contains(template.Code))
            {
                skipped++;
                continue;
            }

            var type = NotWorkedTimeType.Create(
                template.Code,
                template.Name,
                template.AppliesToPermission,
                template.UsesWorkSchedule,
                template.CountsHoliday,
                template.CountsSaturday,
                template.CountsRestDay,
                template.CountsSeventhDayPenalty,
                template.DiscountPercent,
                template.DeductionConceptTypeCode,
                template.IncomeConceptTypeCode);
            type.SetTenantId(tenantId);

            dbContext.NotWorkedTimeTypes.Add(type);
            created++;
        }

        if (created > 0)
        {
            _ = await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new NotWorkedTimeTemplateSeedResult(created, skipped);
    }

    private sealed record NotWorkedTimeTypeTemplate(
        string Code,
        string Name,
        bool AppliesToPermission,
        bool UsesWorkSchedule,
        bool CountsHoliday,
        bool CountsSaturday,
        bool CountsRestDay,
        bool CountsSeventhDayPenalty,
        decimal DiscountPercent,
        string? DeductionConceptTypeCode,
        string? IncomeConceptTypeCode);
}
