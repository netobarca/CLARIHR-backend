using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.JobProfileCatalogTypes.Common;

public static partial class JobProfileCatalogTypeValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidCode(string value) =>
        CodeRegex().IsMatch(value.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.\-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();
}

public static class JobProfileCatalogTypeErrors
{
    public static readonly Error NotFound = new(
        "JOB_PROFILE_CATALOG_TYPE_NOT_FOUND",
        "The requested Job Profile catalog type could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "JOB_PROFILE_CATALOG_TYPE_CODE_CONFLICT",
        "Another Job Profile catalog type already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);
}
