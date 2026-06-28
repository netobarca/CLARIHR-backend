using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

internal sealed class CompanyCertificateSettingsRepository(ApplicationDbContext dbContext) : ICompanyCertificateSettingsRepository
{
    public void Add(CompanyCertificateSettings settings) => dbContext.CompanyCertificateSettings.Add(settings);

    public Task<CompanyCertificateSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.CompanyCertificateSettings.SingleOrDefaultAsync(settings => settings.TenantId == tenantId, cancellationToken);
}
