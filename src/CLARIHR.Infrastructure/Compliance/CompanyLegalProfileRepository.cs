using CLARIHR.Application.Abstractions.Compliance;
using CLARIHR.Domain.Compliance;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Compliance;

internal sealed class CompanyLegalProfileRepository(ApplicationDbContext dbContext) : ICompanyLegalProfileRepository
{
    public void Add(CompanyLegalProfile profile) => dbContext.CompanyLegalProfiles.Add(profile);

    public Task<CompanyLegalProfile?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.CompanyLegalProfiles.SingleOrDefaultAsync(profile => profile.TenantId == tenantId, cancellationToken);
}
