namespace CLARIHR.Application.Features.IdentityAccess.Common;

public sealed record RbacPermissionState(
    bool HasAccess,
    bool CanRead,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete);
