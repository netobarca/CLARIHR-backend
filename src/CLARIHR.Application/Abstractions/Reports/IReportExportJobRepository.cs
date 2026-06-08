using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Application.Abstractions.Reports;

public interface IReportExportJobRepository
{
    void Add(ReportExportJob job);

    Task<ReportExportJob?> GetByPublicIdAsync(Guid jobId, CancellationToken cancellationToken);

    // REX-A: the list is filtered to the resource keys the caller may read (resolved by the handler),
    // so a user cannot see export-job metadata for resources they have no read permission on.
    Task<PagedResponse<ReportExportJobResponse>> SearchAsync(
        Guid tenantId,
        ReportExportJobStatus? status,
        IReadOnlyCollection<string> allowedResourceKeys,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ReportExportJob>> GetClaimableAsync(
        DateTime utcNow,
        int maxCount,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ReportExportJob>> GetExpiredSucceededAsync(
        DateTime utcNow,
        int maxCount,
        CancellationToken cancellationToken);
}
