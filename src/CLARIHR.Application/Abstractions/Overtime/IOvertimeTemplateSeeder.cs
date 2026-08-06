namespace CLARIHR.Application.Abstractions.Overtime;

/// <summary>
/// Outcome of one <see cref="IOvertimeTemplateSeeder.ApplyTemplateAsync"/> run: how many of the legal
/// overtime types were created versus skipped (a row is skipped when the tenant already has one with the
/// template's normalized code — even when that row was edited or inactivated, so tenant edits are never
/// overwritten).
/// </summary>
public sealed record OvertimeTemplateSeedResult(int OvertimeTypesCreated, int OvertimeTypesSkipped);

/// <summary>
/// Seeds the 4 overtime types whose factors the law fixes (HED 2.00 / HEN 2.50 / HEDF 4.00 / HENF 5.00 —
/// Art. 168/169/171/175 CT) into a newly provisioned company. Invoked only by the company-provisioning hook:
/// there is no public template-load endpoint, because everything else in the overtime configuration is a
/// company decision and these catalogs cannot be deleted once created.
/// </summary>
public interface IOvertimeTemplateSeeder
{
    Task<OvertimeTemplateSeedResult> ApplyTemplateAsync(Guid tenantId, CancellationToken cancellationToken);
}
