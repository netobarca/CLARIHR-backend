using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.SalaryTabulator;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.JobProfiles;

internal sealed class JobProfileCompensationRepository(ApplicationDbContext dbContext) : IJobProfileCompensationRepository
{
    public void Add(JobProfileCompensation compensation) => dbContext.JobProfileCompensations.Add(compensation);

    public void Remove(JobProfileCompensation compensation) => dbContext.JobProfileCompensations.Remove(compensation);

    public Task<JobProfileCompensation?> GetByPublicIdAsync(
        Guid jobProfilePublicId,
        Guid compensationPublicId,
        CancellationToken cancellationToken) =>
        (from compensation in dbContext.JobProfileCompensations
         join profile in dbContext.JobProfiles on compensation.JobProfileId equals profile.Id
         where profile.PublicId == jobProfilePublicId &&
               compensation.PublicId == compensationPublicId
         select compensation)
        .SingleOrDefaultAsync(cancellationToken);

    public Task<JobProfileCompensation?> GetByProfileIdAsync(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken) =>
        (from compensation in dbContext.JobProfileCompensations
         join profile in dbContext.JobProfiles on compensation.JobProfileId equals profile.Id
         where profile.PublicId == jobProfilePublicId
         select compensation)
        .SingleOrDefaultAsync(cancellationToken);

    public Task<JobProfileCompensationItemResponse?> GetResponseAsync(
        Guid jobProfilePublicId,
        Guid compensationPublicId,
        CancellationToken cancellationToken) =>
        (from compensation in dbContext.JobProfileCompensations.AsNoTracking()
         join profile in dbContext.JobProfiles.AsNoTracking() on compensation.JobProfileId equals profile.Id
         join line in dbContext.SalaryTabulatorLines.AsNoTracking() on compensation.SalaryTabulatorLineId equals line.Id
         where profile.PublicId == jobProfilePublicId && compensation.PublicId == compensationPublicId
         select new JobProfileCompensationItemResponse(
             compensation.PublicId,
             line.PublicId,
             line.SalaryClassCode,
             line.SalaryScaleCode,
             line.CurrencyCode,
             line.BaseAmount,
             line.MinAmount,
             line.MaxAmount,
             line.EffectiveFromUtc,
             line.EffectiveToUtc,
             compensation.Notes,
             compensation.ConcurrencyToken,
             compensation.CreatedUtc,
             compensation.ModifiedUtc))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<JobProfileCompensationItemResponse>?> GetResponsesByProfileIdAsync(
        Guid jobProfilePublicId,
        int maxItems,
        CancellationToken cancellationToken)
    {
        var profileExists = await dbContext.JobProfiles
            .AsNoTracking()
            .AnyAsync(profile => profile.PublicId == jobProfilePublicId, cancellationToken);
        if (!profileExists)
        {
            return null;
        }

        return await (from compensation in dbContext.JobProfileCompensations.AsNoTracking()
                      join profile in dbContext.JobProfiles.AsNoTracking() on compensation.JobProfileId equals profile.Id
                      join line in dbContext.SalaryTabulatorLines.AsNoTracking() on compensation.SalaryTabulatorLineId equals line.Id
                      where profile.PublicId == jobProfilePublicId
                      select new JobProfileCompensationItemResponse(
                          compensation.PublicId,
                          line.PublicId,
                          line.SalaryClassCode,
                          line.SalaryScaleCode,
                          line.CurrencyCode,
                          line.BaseAmount,
                          line.MinAmount,
                          line.MaxAmount,
                          line.EffectiveFromUtc,
                          line.EffectiveToUtc,
                          compensation.Notes,
                          compensation.ConcurrencyToken,
                          compensation.CreatedUtc,
                          compensation.ModifiedUtc))
            .Take(maxItems)
            .ToArrayAsync(cancellationToken);
    }

    public Task<bool> ProfileHasCompensationAsync(long jobProfileInternalId, CancellationToken cancellationToken) =>
        dbContext.JobProfileCompensations
            .AsNoTracking()
            .AnyAsync(compensation => compensation.JobProfileId == jobProfileInternalId, cancellationToken);

    public Task<long?> ResolveJobProfileInternalIdAsync(Guid tenantId, Guid jobProfilePublicId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.PublicId == jobProfilePublicId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<SalaryTabulatorLine?> ResolveSalaryTabulatorLineAsync(Guid tenantId, Guid salaryTabulatorLinePublicId, CancellationToken cancellationToken) =>
        dbContext.SalaryTabulatorLines
            .AsNoTracking()
            .SingleOrDefaultAsync(
                line => line.TenantId == tenantId && line.PublicId == salaryTabulatorLinePublicId,
                cancellationToken);
}
