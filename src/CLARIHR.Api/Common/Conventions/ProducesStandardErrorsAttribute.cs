namespace CLARIHR.Api.Common.Conventions;

/// <summary>
/// Declares the standard <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> responses
/// an endpoint (or every action of a controller) can return. The
/// <see cref="ProducesStandardErrorsConvention"/> expands the selected
/// <see cref="StandardErrorSet"/> into individual
/// <see cref="Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute"/> filters
/// at application-model build time, so OpenAPI/Swagger sees the full list.
/// Inline <c>[ProducesResponseType]</c> declarations always win — duplicates are skipped.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ProducesStandardErrorsAttribute(StandardErrorSet errors) : Attribute
{
    public StandardErrorSet Errors { get; } = errors;
}
