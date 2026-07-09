using CLARIHR.Application.Abstractions.Overtime;
using CLARIHR.Domain.Overtime;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Overtime;

/// <summary>
/// El Salvador overtime configuration template (Anexo A.2: 4 overtime types with the reference factors
/// HED 2.00 / HEN 2.50 / HEDF 4.00 / HENF 5.00 — editable per company — and 6 justification types).
/// Mirrors <c>EmployeeRelationsTemplateSeeder</c>: guarded existence checks (keyed on the tenant's
/// <c>NormalizedCode</c>) make it idempotent so it is safe to run on every provisioning and on every
/// <c>load-template</c> call. An existing row is skipped even when it was edited or inactivated, so the
/// seeder never overwrites tenant edits — it only creates the missing template rows.
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
        // to this tenant in every call path (the provisioning hook runs without an HTTP tenant claim).
        // Mirrors EmployeeRelationsTemplateSeeder.ApplyTemplateAsync.
        using var tenantScope = ambientTenantContext.Push(tenantId);

        var (typesCreated, typesSkipped) = await ApplyOvertimeTypesAsync(tenantId, cancellationToken);
        var (justificationsCreated, justificationsSkipped) =
            await ApplyOvertimeJustificationTypesAsync(tenantId, cancellationToken);

        if (typesCreated + justificationsCreated > 0)
        {
            _ = await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new OvertimeTemplateSeedResult(
            typesCreated,
            justificationsCreated,
            typesSkipped,
            justificationsSkipped);
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

    private async Task<(int Created, int Skipped)> ApplyOvertimeJustificationTypesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existingCodes = (await dbContext.Set<OvertimeJustificationType>()
                .AsNoTracking()
                .Where(type => type.TenantId == tenantId)
                .Select(type => type.NormalizedCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;
        var skipped = 0;

        foreach (var template in OvertimeJustificationTypeTemplates)
        {
            if (existingCodes.Contains(template.Code))
            {
                skipped++;
                continue;
            }

            var type = OvertimeJustificationType.Create(
                template.Code,
                template.Name,
                template.Description,
                template.SortOrder);
            type.SetTenantId(tenantId);
            dbContext.Set<OvertimeJustificationType>().Add(type);
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

    private static readonly OvertimeJustificationTemplate[] OvertimeJustificationTypeTemplates =
    [
        new("PICO_PRODUCCION", "Pico de producción / mayor demanda", null, 10),
        new("CIERRE_CONTABLE", "Cierre contable o de periodo", null, 20),
        new("PROYECTO_ESPECIAL", "Proyecto o entrega especial", null, 30),
        new("EMERGENCIA", "Emergencia o contingencia operativa", null, 40),
        new("MANTENIMIENTO", "Mantenimiento o soporte fuera de jornada", null, 50),
        new("OTRO", "Otra", null, 60),
    ];

    private sealed record OvertimeTypeTemplate(string Code, string Name, decimal DefaultFactor, string? PayrollEffectDescription, int SortOrder);

    private sealed record OvertimeJustificationTemplate(string Code, string Name, string? Description, int SortOrder);
}
