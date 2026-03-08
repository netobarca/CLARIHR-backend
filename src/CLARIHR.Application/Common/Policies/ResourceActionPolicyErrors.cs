using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Common.Policies;

public static class ResourceActionPolicyErrors
{
    public static readonly Error ActionForbiddenByState = new(
        "RESOURCE_ACTION_FORBIDDEN_BY_STATE",
        "The requested action is not allowed for the current resource state.",
        ErrorType.Conflict);

    public static readonly Error ActionForbiddenByType = new(
        "RESOURCE_ACTION_FORBIDDEN_BY_TYPE",
        "The requested action is not allowed for this resource type.",
        ErrorType.Conflict);

    public static readonly Error DeleteBlockedByDependencies = new(
        "RESOURCE_DELETE_BLOCKED_BY_DEPENDENCIES",
        "The resource cannot be deleted because it has active dependencies.",
        ErrorType.Conflict);
}
