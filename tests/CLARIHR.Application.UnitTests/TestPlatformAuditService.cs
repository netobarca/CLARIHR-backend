using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Features.Audit.Common;

namespace CLARIHR.Application.UnitTests;

internal sealed class TestPlatformAuditService : IPlatformAuditService
{
    public List<AuditLogEntry> Entries { get; } = [];

    public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public bool Recorded(string eventType) =>
        Entries.Exists(entry => entry.EventType == eventType);
}
