using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Platform;

public sealed class PlatformAuditLog : AuditableEntity
{
    private PlatformAuditLog()
    {
    }

    private PlatformAuditLog(
        Guid actorUserId,
        string? actorEmail,
        string eventType,
        string entityType,
        Guid? entityId,
        string? entityKey,
        string action,
        string summary,
        string? beforeJson,
        string? afterJson,
        string? diffJson,
        string? ipAddress,
        string? userAgent)
    {
        ActorUserId = actorUserId;
        ActorEmail = CleanOptional(actorEmail, 320);
        EventType = CleanRequired(eventType, nameof(eventType), 100);
        EntityType = CleanRequired(entityType, nameof(entityType), 50);
        EntityId = entityId;
        EntityKey = CleanOptional(entityKey, 150);
        Action = CleanRequired(action, nameof(action), 50);
        Summary = CleanRequired(summary, nameof(summary), 500);
        BeforeJson = CleanOptional(beforeJson);
        AfterJson = CleanOptional(afterJson);
        DiffJson = CleanOptional(diffJson);
        IpAddress = CleanOptional(ipAddress, 45);
        UserAgent = CleanOptional(userAgent, 1000);
    }

    public Guid ActorUserId { get; private set; }

    public string? ActorEmail { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    public string? EntityKey { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public string? BeforeJson { get; private set; }

    public string? AfterJson { get; private set; }

    public string? DiffJson { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public static PlatformAuditLog Create(
        Guid actorUserId,
        string? actorEmail,
        string eventType,
        string entityType,
        Guid? entityId,
        string? entityKey,
        string action,
        string summary,
        string? beforeJson,
        string? afterJson,
        string? diffJson,
        string? ipAddress,
        string? userAgent) =>
        new(
            actorUserId,
            actorEmail,
            eventType,
            entityType,
            entityId,
            entityKey,
            action,
            summary,
            beforeJson,
            afterJson,
            diffJson,
            ipAddress,
            userAgent);

    private static string CleanRequired(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", paramName);
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : throw new ArgumentOutOfRangeException(paramName, $"Value cannot exceed {maxLength} characters.");
    }

    private static string? CleanOptional(string? value, int maxLength = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : throw new ArgumentOutOfRangeException(nameof(value), $"Value cannot exceed {maxLength} characters.");
    }
}
