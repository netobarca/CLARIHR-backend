namespace CLARIHR.Application.Common.Errors;

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    IReadOnlyCollection<ErrorDetail>? Details = null,
    IReadOnlyList<object?>? MessageArguments = null,
    // Structured, machine-readable payload the client needs to ACT on the error (e.g. the indebtedness breakdown
    // behind an INDEBTEDNESS_LIMIT_EXCEEDED, which drives the confirmation dialog). It cannot ride in the message:
    // the localizer REPLACES `detail` with the catalogued text. These land as ROOT members of the ProblemDetails
    // (ProblemDetails.Extensions is [JsonExtensionData]); the reserved keys code/traceId/details are never shadowed.
    IReadOnlyDictionary<string, object?>? Extensions = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
}
