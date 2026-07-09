namespace CLARIHR.Application.Abstractions.Overtime;

/// <summary>
/// Outcome of one <see cref="IOvertimeTemplateSeeder.ApplyTemplateAsync"/> run: how many template rows
/// were created versus skipped (a row is skipped when the tenant already has a master with the template's
/// normalized code — even when that row was edited or inactivated, so tenant edits are never overwritten).
/// </summary>
public sealed record OvertimeTemplateSeedResult(
    int OvertimeTypesCreated,
    int OvertimeJustificationTypesCreated,
    int OvertimeTypesSkipped,
    int OvertimeJustificationTypesSkipped);

/// <summary>
/// Applies the El Salvador overtime configuration template to one tenant: the 4 overtime types (with the
/// reference factors HED 2.00 / HEN 2.50 / HEDF 4.00 / HENF 5.00 — Anexo A.2, editable per company) and the
/// 6 justification types (RF-002/RF-003). Idempotent by normalized code (creates missing rows, never
/// overwrites edits). Invoked (a) by the company-provisioning hook for new tenants and (b) by the admin
/// <c>POST …/overtime-configuration/load-template</c> endpoint for existing tenants. Mirrors
/// <c>IEmployeeRelationsTemplateSeeder</c>.
/// </summary>
public interface IOvertimeTemplateSeeder
{
    Task<OvertimeTemplateSeedResult> ApplyTemplateAsync(Guid tenantId, CancellationToken cancellationToken);
}
