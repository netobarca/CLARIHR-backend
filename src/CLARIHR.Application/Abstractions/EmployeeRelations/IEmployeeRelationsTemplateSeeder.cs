namespace CLARIHR.Application.Abstractions.EmployeeRelations;

/// <summary>
/// Outcome of one <see cref="IEmployeeRelationsTemplateSeeder.ApplyTemplateAsync"/> run: how many
/// template rows were created versus skipped (a row is skipped when the tenant already has a master
/// with the template's normalized code — even when that row was edited or inactivated, so tenant
/// edits are never overwritten).
/// </summary>
public sealed record EmployeeRelationsTemplateSeedResult(
    int RecognitionTypesCreated,
    int DisciplinaryActionTypesCreated,
    int DisciplinaryActionCausesCreated,
    int RecognitionTypesSkipped,
    int DisciplinaryActionTypesSkipped,
    int DisciplinaryActionCausesSkipped);

/// <summary>
/// Applies the El Salvador employee-relations configuration template to one tenant: the recognition
/// types, disciplinary-action types (only SUSPENSION_SIN_GOCE carries the suspension flag) and
/// disciplinary-action causes (seeded WITHOUT a deduction concept — REQ-003 P-14) of Anexo A.2.
/// Idempotent by normalized code (creates missing rows, never overwrites edits). Invoked (a) by the
/// company-provisioning hook for new tenants and (b) by the admin
/// <c>POST …/employee-relations/load-template</c> endpoint for existing tenants. Mirrors
/// <c>ILeaveTemplateSeeder</c>.
/// </summary>
public interface IEmployeeRelationsTemplateSeeder
{
    Task<EmployeeRelationsTemplateSeedResult> ApplyTemplateAsync(Guid tenantId, CancellationToken cancellationToken);
}
