using CLARIHR.Application.Features.Audit.Common;

namespace CLARIHR.Application.Abstractions.Auditing;

public interface IAuditService
{
    Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken);

    Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken);
}
