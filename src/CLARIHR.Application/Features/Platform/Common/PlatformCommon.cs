using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.Platform.Common;

public static class PlatformAccessErrors
{
    public static readonly Error Forbidden = new(
        "PLATFORM_ACCESS_FORBIDDEN",
        "You do not have permission to access platform backoffice administration.",
        ErrorType.Forbidden);
}

public static class PlatformOperatorErrors
{
    public static readonly Error UserNotFound = new(
        "PLATFORM_OPERATOR_USER_NOT_FOUND",
        "The requested user could not be found.",
        ErrorType.NotFound);

    public static readonly Error UserMustBeActiveLocal = new(
        "PLATFORM_OPERATOR_USER_MUST_BE_ACTIVE_LOCAL",
        "Platform operators must be linked to an active local user.",
        ErrorType.Validation);

    public static readonly Error AlreadyExists = new(
        "PLATFORM_OPERATOR_ALREADY_EXISTS",
        "The user is already linked to a platform operator.",
        ErrorType.Conflict);
}
