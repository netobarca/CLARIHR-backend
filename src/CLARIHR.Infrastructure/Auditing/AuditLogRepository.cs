using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Domain.Auditing;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Auditing;

internal sealed class AuditLogRepository(ApplicationDbContext dbContext) : IAuditLogRepository
{
    public void Add(AuditLog auditLog) => dbContext.Set<AuditLog>().Add(auditLog);

    public async Task<PagedResponse<AuditLog>> GetLogsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? actorUserId,
        Guid? entityId,
        string? entityType,
        string? eventType,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<AuditLog>().AsNoTracking();

        if (fromUtc.HasValue)
        {
            query = query.Where(log => log.CreatedUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(log => log.CreatedUtc <= toUtc.Value);
        }

        if (actorUserId.HasValue)
        {
            query = query.Where(log => log.ActorUserId == actorUserId.Value);
        }

        if (entityId.HasValue)
        {
            query = query.Where(log => log.EntityId == entityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(log => log.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(log => log.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(log =>
                (log.ActorEmail != null && log.ActorEmail.ToUpper().Contains(normalizedSearch)) ||
                log.Summary.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.CreatedUtc)
            .ThenByDescending(log => log.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<AuditLog>(items, pageNumber, pageSize, totalCount);
    }

    public Task<AuditLog?> GetByPublicIdAsync(Guid auditLogId, CancellationToken cancellationToken) =>
        dbContext.Set<AuditLog>()
            .AsNoTracking()
            .SingleOrDefaultAsync(log => log.PublicId == auditLogId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid auditLogId, CancellationToken cancellationToken) =>
        dbContext.Set<AuditLog>()
            .IgnoreQueryFilters()
            .AnyAsync(log => log.PublicId == auditLogId, cancellationToken);
}
