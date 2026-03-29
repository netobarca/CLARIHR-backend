using CLARIHR.Application.Features.Audit.Common;

namespace CLARIHR.Application.Abstractions.Auditing;

public interface IPlatformAuditService
{
    Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken);
}
