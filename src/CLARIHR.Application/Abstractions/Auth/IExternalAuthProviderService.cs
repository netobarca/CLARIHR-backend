using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Abstractions.Auth;

public interface IExternalAuthProviderService
{
    Task<Result<ExternalAuthValidationResult>> ValidateAsync(
        AuthProvider provider,
        string idToken,
        CancellationToken cancellationToken);
}
