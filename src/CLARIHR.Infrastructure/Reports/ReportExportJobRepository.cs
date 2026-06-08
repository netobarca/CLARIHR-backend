using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Reports;

internal sealed class ReportExportJobRepository(ApplicationDbContext dbContext) : IReportExportJobRepository
{
    public void Add(ReportExportJob job) => dbContext.ReportExportJobs.Add(job);

    public Task<ReportExportJob?> GetByPublicIdAsync(Guid jobId, CancellationToken cancellationToken) =>
        dbContext.ReportExportJobs.SingleOrDefaultAsync(job => job.PublicId == jobId, cancellationToken);

    public async Task<PagedResponse<ReportExportJobResponse>> SearchAsync(
        Guid tenantId,
        ReportExportJobStatus? status,
        IReadOnlyCollection<string> allowedResourceKeys,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ReportExportJobs
            .AsNoTracking()
            .Where(job => job.TenantId == tenantId)
            // REX-A: only jobs whose resource the caller may read (the handler passes the resolved set).
            .Where(job => allowedResourceKeys.Contains(job.ResourceKey));

        if (status.HasValue)
        {
            query = query.Where(job => job.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(job => job.QueuedUtc)
            .ThenByDescending(job => job.PublicId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(job => new ReportExportJobResponse(
                job.PublicId,
                job.ResourceKey,
                job.Format,
                job.Status,
                job.QueuedUtc,
                job.StartedUtc,
                job.CompletedUtc,
                job.ExpiresUtc,
                job.Attempts,
                job.RowCount,
                job.ArtifactFileName,
                job.ArtifactSizeBytes,
                job.LastErrorCode,
                job.LastErrorMessage,
                job.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<ReportExportJobResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IReadOnlyCollection<ReportExportJob>> GetClaimableAsync(
        DateTime utcNow,
        int maxCount,
        CancellationToken cancellationToken) =>
        await dbContext.ReportExportJobs
            // Intentional tenant filter bypass: background worker must claim queued jobs across tenants without exposing job data to users.
            .IgnoreQueryFilters()
            .Where(job =>
                job.Status == ReportExportJobStatus.Queued ||
                (job.Status == ReportExportJobStatus.Running &&
                 job.LeaseUntilUtc.HasValue &&
                 job.LeaseUntilUtc.Value <= utcNow))
            .OrderBy(job => job.QueuedUtc)
            .Take(maxCount)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ReportExportJob>> GetExpiredSucceededAsync(
        DateTime utcNow,
        int maxCount,
        CancellationToken cancellationToken) =>
        await dbContext.ReportExportJobs
            // Intentional tenant filter bypass: background retention cleanup scans succeeded jobs across tenants only to expire artifacts.
            .IgnoreQueryFilters()
            .Where(job =>
                job.Status == ReportExportJobStatus.Succeeded &&
                job.ExpiresUtc.HasValue &&
                job.ExpiresUtc.Value <= utcNow)
            .OrderBy(job => job.ExpiresUtc)
            .Take(maxCount)
            .ToArrayAsync(cancellationToken);
}
