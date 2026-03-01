namespace CLARIHR.Application.Abstractions.Auditing;

public interface IAuditSanitizer
{
    string? SanitizeToJson(object? payload);
}
