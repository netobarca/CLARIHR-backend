namespace CLARIHR.Application.Abstractions.Leave;

/// <summary>Outcome of one template run: rows created versus skipped. A row is SKIPPED when the tenant already has a
/// type with that normalized code — even if it was edited or inactivated. The seeder never overwrites tenant edits;
/// it only fills in what is missing, which is what makes it safe to run on every provisioning and on every
/// <c>load-template</c> call.</summary>
public sealed record NotWorkedTimeTemplateSeedResult(int TypesCreated, int TypesSkipped);

/// <summary>Applies the not-worked-time TYPE template to one tenant (REQ-011 D-18).</summary>
public interface INotWorkedTimeTemplateSeeder
{
    Task<NotWorkedTimeTemplateSeedResult> ApplyTemplateAsync(Guid tenantId, CancellationToken cancellationToken);
}
