namespace CLARIHR.Application.Common.Errors;

public static class ErrorCatalog
{
    public static readonly Error Unauthorized = new(
        "auth.unauthorized",
        "Authentication is required to access this resource.",
        ErrorType.Unauthorized);

    public static readonly Error Forbidden = new(
        "auth.forbidden",
        "You do not have permission to access this resource.",
        ErrorType.Forbidden);

    public static readonly Error NotFound = new(
        "common.not_found",
        "The requested resource was not found.",
        ErrorType.NotFound);

    public static readonly Error Conflict = new(
        "common.conflict",
        "The request conflicts with the current state of the resource.",
        ErrorType.Conflict);

    public static readonly Error TooManyRequests = new(
        "common.too_many_requests",
        "The request rate exceeded the allowed threshold.",
        ErrorType.TooManyRequests);

    public static readonly Error Unexpected = new(
        "common.unexpected",
        "An unexpected error occurred.",
        ErrorType.Unexpected);

    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(
            "common.validation",
            "One or more validation errors occurred.",
            ErrorType.Validation,
            errors);
}
