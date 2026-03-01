namespace CLARIHR.Application.Abstractions.Companies;

public interface IPlanEntitlementService
{
    Task EnsureFreePlanDefaultsAsync(CancellationToken cancellationToken);

    Task<bool> IsModuleEnabledAsync(Guid companyPublicId, string moduleKey, CancellationToken cancellationToken);
}
