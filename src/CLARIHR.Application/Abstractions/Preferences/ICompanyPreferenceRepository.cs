using CLARIHR.Domain.Preferences;

namespace CLARIHR.Application.Abstractions.Preferences;

public interface ICompanyPreferenceRepository
{
    void Add(CompanyPreference preference);

    Task<CompanyPreference?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken);
}
