using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Tenancy;

internal sealed class TenantLocaleResolver(ApplicationDbContext dbContext) : ITenantLocaleResolver
{
    public Task<string?> ResolveDefaultLocaleAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.Companies
            .AsNoTracking()
            .Where(company => company.PublicId == tenantId)
            .Select(company => company.DefaultLocale)
            .SingleOrDefaultAsync(cancellationToken);
}
