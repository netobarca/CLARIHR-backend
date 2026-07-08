using CLARIHR.Application.Abstractions.EmployeeRelations;
using CLARIHR.Domain.EmployeeRelations;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.EmployeeRelations;

/// <summary>
/// El Salvador employee-relations configuration template (Anexo A.2: recognition types,
/// disciplinary-action types, disciplinary-action causes — REQ-003 aclaración №8). Mirrors
/// <c>LeaveTemplateSeeder</c>: guarded existence checks (keyed on the tenant's <c>NormalizedCode</c>)
/// make it idempotent so it is safe to run on every provisioning and on every <c>load-template</c>
/// call. An existing row is skipped even when it was edited or inactivated, so the seeder never
/// overwrites tenant edits — it only creates the missing template rows. All causes ship WITHOUT a
/// deduction concept (REQ-003 P-14 "no hay multas").
/// </summary>
internal sealed class EmployeeRelationsTemplateSeeder(
    ApplicationDbContext dbContext,
    AmbientTenantContext ambientTenantContext) : IEmployeeRelationsTemplateSeeder
{
    public async Task<EmployeeRelationsTemplateSeedResult> ApplyTemplateAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Push the ambient tenant so the fail-closed global query filter scopes the idempotency guards
        // below to this tenant in every call path (the provisioning hook runs without an HTTP tenant
        // claim). Mirrors LeaveTemplateSeeder.ApplyTemplateAsync.
        using var tenantScope = ambientTenantContext.Push(tenantId);

        var (recognitionTypesCreated, recognitionTypesSkipped) =
            await ApplyRecognitionTypesAsync(tenantId, cancellationToken);
        var (disciplinaryTypesCreated, disciplinaryTypesSkipped) =
            await ApplyDisciplinaryActionTypesAsync(tenantId, cancellationToken);
        var (disciplinaryCausesCreated, disciplinaryCausesSkipped) =
            await ApplyDisciplinaryActionCausesAsync(tenantId, cancellationToken);

        if (recognitionTypesCreated + disciplinaryTypesCreated + disciplinaryCausesCreated > 0)
        {
            _ = await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new EmployeeRelationsTemplateSeedResult(
            recognitionTypesCreated,
            disciplinaryTypesCreated,
            disciplinaryCausesCreated,
            recognitionTypesSkipped,
            disciplinaryTypesSkipped,
            disciplinaryCausesSkipped);
    }

    private async Task<(int Created, int Skipped)> ApplyRecognitionTypesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existingCodes = (await dbContext.Set<RecognitionType>()
                .AsNoTracking()
                .Where(type => type.TenantId == tenantId)
                .Select(type => type.NormalizedCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;
        var skipped = 0;

        foreach (var template in RecognitionTypeTemplates)
        {
            if (existingCodes.Contains(template.Code))
            {
                skipped++;
                continue;
            }

            var type = RecognitionType.Create(template.Code, template.Name, template.SortOrder);
            type.SetTenantId(tenantId);
            dbContext.Set<RecognitionType>().Add(type);
            created++;
        }

        return (created, skipped);
    }

    private async Task<(int Created, int Skipped)> ApplyDisciplinaryActionTypesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existingCodes = (await dbContext.Set<DisciplinaryActionType>()
                .AsNoTracking()
                .Where(type => type.TenantId == tenantId)
                .Select(type => type.NormalizedCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;
        var skipped = 0;

        foreach (var template in DisciplinaryActionTypeTemplates)
        {
            if (existingCodes.Contains(template.Code))
            {
                skipped++;
                continue;
            }

            var type = DisciplinaryActionType.Create(
                template.Code,
                template.Name,
                template.AppliesSuspension,
                template.SortOrder);
            type.SetTenantId(tenantId);
            dbContext.Set<DisciplinaryActionType>().Add(type);
            created++;
        }

        return (created, skipped);
    }

    private async Task<(int Created, int Skipped)> ApplyDisciplinaryActionCausesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existingCodes = (await dbContext.Set<DisciplinaryActionCause>()
                .AsNoTracking()
                .Where(cause => cause.TenantId == tenantId)
                .Select(cause => cause.NormalizedCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;
        var skipped = 0;

        foreach (var template in DisciplinaryActionCauseTemplates)
        {
            if (existingCodes.Contains(template.Code))
            {
                skipped++;
                continue;
            }

            // P-14 "no hay multas": every cause ships without a deduction concept.
            var cause = DisciplinaryActionCause.Create(
                template.Code,
                template.Name,
                deductionConceptTypeCode: null,
                template.SortOrder);
            cause.SetTenantId(tenantId);
            dbContext.Set<DisciplinaryActionCause>().Add(cause);
            created++;
        }

        return (created, skipped);
    }

    // ---------------------------------------------------------------------------------------
    // Template data — El Salvador (ratified Anexo A.2). Codes are already normalized (upper).
    // ---------------------------------------------------------------------------------------

    private static readonly MasterTemplate[] RecognitionTypeTemplates =
    [
        new("FELICITACION_ESCRITA", "Felicitación escrita", 10),
        new("DESEMPENO_SOBRESALIENTE", "Desempeño sobresaliente", 20),
        new("PRODUCTIVIDAD", "Logro de metas / mayor producción", 30),
        new("ANTIGUEDAD", "Reconocimiento por años de servicio", 40),
        new("OTRO", "Otro", 50),
    ];

    private static readonly DisciplinaryTypeTemplate[] DisciplinaryActionTypeTemplates =
    [
        new("VERBAL", "Amonestación verbal (con constancia escrita)", AppliesSuspension: false, 10),
        new("ESCRITA", "Amonestación escrita", AppliesSuspension: false, 20),
        new("SUSPENSION_SIN_GOCE", "Suspensión de labores sin goce de sueldo", AppliesSuspension: true, 30),
        new("OTRO", "Otra", AppliesSuspension: false, 40),
    ];

    private static readonly MasterTemplate[] DisciplinaryActionCauseTemplates =
    [
        new("INASISTENCIA_INJUSTIFICADA", "Inasistencia injustificada", 10),
        new("LLEGADAS_TARDIAS", "Llegadas tardías reiteradas", 20),
        new("INCUMPLIMIENTO_FUNCIONES", "Incumplimiento de funciones", 30),
        new("CONDUCTA_INDEBIDA", "Conducta indebida", 40),
        new("DANO_BIENES", "Daño a bienes de la empresa", 50),
        new("OTRO", "Otra", 60),
    ];

    private sealed record MasterTemplate(string Code, string Name, int SortOrder);

    private sealed record DisciplinaryTypeTemplate(string Code, string Name, bool AppliesSuspension, int SortOrder);
}
