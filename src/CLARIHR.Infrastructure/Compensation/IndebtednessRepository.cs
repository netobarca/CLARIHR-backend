using CLARIHR.Application.Abstractions.Compensation;
using CLARIHR.Application.Features.Compensation;
using CLARIHR.Domain.Compensation;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Compensation;

internal sealed class IndebtednessRepository(ApplicationDbContext dbContext) : IIndebtednessRepository
{
    public async Task<IReadOnlyCollection<IndebtednessLimitResponse>> GetLimitsAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await dbContext.IndebtednessLimits
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .OrderBy(item => item.RecurringDeductionTypeCode)
            .Select(item => new IndebtednessLimitResponse(
                item.PublicId,
                item.RecurringDeductionTypeCode,
                item.MaxPercent,
                item.IsActive))
            .ToArrayAsync(cancellationToken);

    public async Task ReplaceLimitsAsync(
        Guid tenantId,
        IReadOnlyCollection<IndebtednessLimit> limits,
        CancellationToken cancellationToken)
    {
        // Hard delete, not a logical one: without production data there is nothing to preserve, and keeping
        // tombstones would only fight the filtered-unique index for no benefit.
        var existing = await dbContext.IndebtednessLimits
            .Where(item => item.TenantId == tenantId)
            .ToArrayAsync(cancellationToken);

        dbContext.IndebtednessLimits.RemoveRange(existing);

        foreach (var limit in limits)
        {
            dbContext.IndebtednessLimits.Add(limit);
        }
    }
}
