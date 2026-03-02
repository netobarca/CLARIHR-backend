using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Abstractions.Auth;

public interface ITokenService
{
    Task<Result<AuthTokenResult>> GenerateAsync(User user, CancellationToken cancellationToken);

    Task<Result<RefreshTokenExchangeResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task<Result<AuthTokenResult>> GenerateForTenantAsync(User user, Guid tenantId, CancellationToken cancellationToken);
}
