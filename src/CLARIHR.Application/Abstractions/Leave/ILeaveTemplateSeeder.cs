namespace CLARIHR.Application.Abstractions.Leave;

/// <summary>
/// Outcome of one <see cref="ILeaveTemplateSeeder.ApplyTemplateAsync"/> run: how many template
/// rows were created versus skipped (a row is skipped when the tenant already has an incapacity
/// risk/type with the template's normalized code, or a holiday on the template's date — even when
/// that row was edited or inactivated, so tenant edits are never overwritten).
/// </summary>
public sealed record LeaveTemplateSeedResult(
    int RisksCreated,
    int RiskParametersCreated,
    int TypesCreated,
    int HolidaysCreated,
    int RisksSkipped,
    int TypesSkipped,
    int HolidaysSkipped);

/// <summary>
/// Applies the El Salvador leave-configuration template to one tenant: the incapacity risks of
/// Anexo A.2 (with their subsidy tranches), the minimum incapacity types (D-08, including
/// LACTANCIA) and — only when <c>holidayYear</c> travels — the national holidays of Anexo A.3
/// (Art. 190 CT) for that year. Idempotent by code/date (creates missing rows, never overwrites
/// edits). Invoked (a) by the company-provisioning hook for new tenants and (b) by the admin
/// <c>POST …/leave-configuration/load-template</c> endpoint for existing tenants.
/// </summary>
public interface ILeaveTemplateSeeder
{
    Task<LeaveTemplateSeedResult> ApplyTemplateAsync(Guid tenantId, int? holidayYear, CancellationToken cancellationToken);
}
