namespace CLARIHR.Application.Features.Audit.Common;

public sealed record AuditLogEntry(
    string EventType,
    string EntityType,
    Guid? EntityId,
    string? EntityKey,
    string Action,
    string Summary,
    object? Before = null,
    object? After = null,
    object? Diff = null);
