namespace CLARIHR.Application.Common.Errors;

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    IReadOnlyCollection<ErrorDetail>? Details = null,
    IReadOnlyList<object?>? MessageArguments = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
}
