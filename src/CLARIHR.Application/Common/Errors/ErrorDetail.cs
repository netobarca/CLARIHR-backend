namespace CLARIHR.Application.Common.Errors;

public sealed record ErrorDetail(
    string? ResourceKey = null,
    string? Action = null,
    string? FieldKey = null,
    string? Endpoint = null);
