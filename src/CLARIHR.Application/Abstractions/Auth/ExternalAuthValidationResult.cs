using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Abstractions.Auth;

public sealed record ExternalAuthValidationResult(
    string? Email,
    string FirstName,
    string LastName,
    string ProviderUserId,
    AuthProvider Provider,
    bool CanAutoLinkByEmail);
