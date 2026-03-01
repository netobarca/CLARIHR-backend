using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.IdentityAccess.Common;

public static class RbacErrors
{
    public static readonly Error Denied = new(
        "RBAC_DENIED",
        "You do not have permission to perform this action.",
        ErrorType.Forbidden);
}
