using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.IdentityAccess.Common;

public static class FieldPermissionErrors
{
    public static readonly Error ResourceAccessRequired = new(
        "iam.field_permissions.resource_access_required",
        "The target role must have screen access before field permissions can be configured.",
        ErrorType.Conflict);
}
