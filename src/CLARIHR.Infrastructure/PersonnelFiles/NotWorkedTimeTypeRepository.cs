using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Domain.Leave;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

internal sealed class NotWorkedTimeTypeRepository(ApplicationDbContext dbContext) : INotWorkedTimeTypeRepository
{
    public async Task<IReadOnlyCollection<NotWorkedTimeType>> GetAsync(
        Guid tenantId,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.NotWorkedTimeTypes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

        if (isActive is { } active)
        {
            query = query.Where(item => item.IsActive == active);
        }

        return await query.OrderBy(item => item.NormalizedName).ToArrayAsync(cancellationToken);
    }

    public Task<NotWorkedTimeType?> GetEntityAsync(
        Guid tenantId,
        Guid publicId,
        CancellationToken cancellationToken) =>
        dbContext.NotWorkedTimeTypes
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.PublicId == publicId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingPublicId,
        CancellationToken cancellationToken) =>
        dbContext.NotWorkedTimeTypes
            .AsNoTracking()
            .AnyAsync(
                item => item.TenantId == tenantId
                    && item.NormalizedCode == normalizedCode
                    && (excludingPublicId == null || item.PublicId != excludingPublicId),
                cancellationToken);

    public void Add(NotWorkedTimeType entity) => dbContext.NotWorkedTimeTypes.Add(entity);
}
