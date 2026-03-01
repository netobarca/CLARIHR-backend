using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.IdentityAccess.Common;

public static class AuthorizationErrors
{
    public static readonly Error Unauthenticated = new(
        "UNAUTHENTICATED",
        "Authentication is required to access this resource.",
        ErrorType.Unauthorized);

    public static Error Denied(string resourceKey, RbacPermissionAction action, string? endpoint = null) =>
        new(
            "RBAC_DENIED",
            "You do not have permission to perform this action.",
            ErrorType.Forbidden,
            Details:
            [
                new ErrorDetail(
                    ResourceKey: resourceKey,
                    Action: action.ToString(),
                    Endpoint: endpoint)
            ]);

    public static Error TenantMismatch(string resourceKey, RbacPermissionAction action, string? endpoint = null) =>
        new(
            "TENANT_MISMATCH",
            "The requested resource does not belong to the current tenant.",
            ErrorType.Forbidden,
            Details:
            [
                new ErrorDetail(
                    ResourceKey: resourceKey,
                    Action: action.ToString(),
                    Endpoint: endpoint)
            ]);

    public static Error FieldEditForbidden(
        string resourceKey,
        RbacPermissionAction action,
        IEnumerable<string> fieldKeys,
        string? endpoint = null) =>
        new(
            "FIELD_EDIT_FORBIDDEN",
            "One or more submitted fields cannot be modified by the current user.",
            ErrorType.Forbidden,
            Details: fieldKeys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(fieldKey => new ErrorDetail(
                    ResourceKey: resourceKey,
                    Action: action.ToString(),
                    FieldKey: fieldKey,
                    Endpoint: endpoint))
                .ToArray());
}
