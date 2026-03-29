namespace CLARIHR.Application.Features.System.GetApiStatus;

public sealed record ApiStatusResponse(
    string ApplicationName,
    DateTime UtcNow,
    Guid? TenantId,
    string? UserPublicId,
    bool IsAuthenticated);
