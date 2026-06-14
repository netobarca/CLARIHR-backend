using CLARIHR.Application.Common.Policies;

namespace CLARIHR.Application.Abstractions.Policies;

/// <summary>
/// Computes the <see cref="AllowedActionsResponse"/> for a resource from the current
/// user's JWT permission claims (no DB access) plus the already-loaded response DTO's
/// state. Returns <c>null</c> (fail-closed) when the resource key is not registered.
/// Advertised capabilities are a subset of what the server enforces.
/// </summary>
public interface IAllowedActionsResolver
{
    AllowedActionsResponse? Resolve(string resourceKey, object? dto);
}
