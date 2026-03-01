using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.Audit.Common;

public static class AuditErrors
{
    public static readonly Error LogNotFound = new(
        "AUDIT_LOG_NOT_FOUND",
        "The requested audit log entry was not found.",
        ErrorType.NotFound);
}
