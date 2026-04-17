using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Domain.Preferences;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Preferences;

internal sealed class CompanyPreferenceRepository(ApplicationDbContext dbContext) : ICompanyPreferenceRepository
{
    public void Add(CompanyPreference preference) => dbContext.CompanyPreferences.Add(preference);

    public Task<CompanyPreference?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.CompanyPreferences.SingleOrDefaultAsync(preference => preference.TenantId == tenantId, cancellationToken);
}
