using CLARIHR.Application.Common.Pagination;
using CLARIHR.Domain.Auditing;

namespace CLARIHR.Application.Abstractions.Auditing;

public interface IAuditLogRepository
{
    void Add(AuditLog auditLog);

    Task<PagedResponse<AuditLog>> GetLogsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? actorUserId,
        string? entityType,
        string? eventType,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AuditLog?> GetByPublicIdAsync(Guid auditLogId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid auditLogId, CancellationToken cancellationToken);
}
