using CLARIHR.Application.Abstractions.Overtime;
using CLARIHR.Domain.Overtime;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Overtime;

/// <summary>
/// Seeds the 4 legal overtime types for a company (Art. 168/169/171/175 CT: HED 2.00 / HEN 2.50 /
/// HEDF 4.00 / HENF 5.00). Runs once, at provisioning.
///
/// Justification types are NOT seeded: why a company authorises overtime is company specific, the catalog
/// supports no DELETE (only activate/inactivate), and a seeded guess would be permanent noise. The company
/// creates its own through <c>POST /companies/{companyPublicId}/overtime-justification-types</c>.
///
/// Guarded existence checks (keyed on the tenant's <c>NormalizedCode</c>) keep it idempotent; an existing row
/// is skipped even when edited or inactivated, so tenant edits are never overwritten.
/// </summary>
internal sealed class OvertimeTemplateSeeder(
    ApplicationDbContext dbContext,
    AmbientTenantContext ambientTenantContext) : IOvertimeTemplateSeeder
{
    public async Task<OvertimeTemplateSeedResult> ApplyTemplateAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Push the ambient tenant so the fail-closed global query filter scopes the idempotency guards below
        // to this tenant (the provisioning hook runs without an HTTP tenant claim).
        using var tenantScope = ambientTenantContext.Push(tenantId);

        var (typesCreated, typesSkipped) = await ApplyOvertimeTypesAsync(tenantId, cancellationToken);

        if (typesCreated > 0)
        {
            _ = await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new OvertimeTemplateSeedResult(typesCreated, typesSkipped);
    }

    private async Task<(int Created, int Skipped)> ApplyOvertimeTypesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existingCodes = (await dbContext.Set<OvertimeType>()
                .AsNoTracking()
                .Where(type => type.TenantId == tenantId)
                .Select(type => type.NormalizedCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;
        var skipped = 0;

        foreach (var template in OvertimeTypeTemplates)
        {
            if (existingCodes.Contains(template.Code))
            {
                skipped++;
                continue;
            }

            var type = OvertimeType.Create(
                template.Code,
                template.Name,
                template.DefaultFactor,
                template.PayrollEffectDescription,
                template.SortOrder);
            type.SetTenantId(tenantId);
            dbContext.Set<OvertimeType>().Add(type);
            created++;
        }

        return (created, skipped);
    }


    // ---------------------------------------------------------------------------------------
    // Template data — El Salvador (ratified Anexo A.2). Codes are already normalized (upper). The factors
    // are REFERENCE values (editable per company; confirm with the accountant before load-template in prod).
    // ---------------------------------------------------------------------------------------

    private static readonly OvertimeTypeTemplate[] OvertimeTypeTemplates =
    [
        new("HED", "Hora extra diurna", 2.00m, "Recargo del 100% sobre la hora ordinaria diurna (Art. 169 CT).", 10),
        new("HEN", "Hora extra nocturna", 2.50m, "Hora nocturna con recargo de horas extra (Art. 168/169 CT).", 20),
        new("HEDF", "Hora extra diurna en día de descanso/asueto", 4.00m, "Día de descanso o asueto trabajado, jornada diurna (Art. 171/175 CT).", 30),
        new("HENF", "Hora extra nocturna en día de descanso/asueto", 5.00m, "Día de descanso o asueto trabajado, jornada nocturna (Art. 171/175 CT).", 40),
    ];


    private sealed record OvertimeTypeTemplate(string Code, string Name, decimal DefaultFactor, string? PayrollEffectDescription, int SortOrder);

}
