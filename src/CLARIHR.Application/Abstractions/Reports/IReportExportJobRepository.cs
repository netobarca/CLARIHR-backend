using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Application.Abstractions.Reports;

public interface IReportExportJobRepository
{
    void Add(ReportExportJob job);

    Task<ReportExportJob?> GetByPublicIdAsync(Guid jobId, CancellationToken cancellationToken);

    Task<PagedResponse<ReportExportJobResponse>> SearchAsync(
        Guid tenantId,
        ReportExportJobStatus? status,
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
