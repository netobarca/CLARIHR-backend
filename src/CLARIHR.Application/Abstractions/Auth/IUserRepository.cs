using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Abstractions.Auth;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken);

    Task<User?> GetByPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<User?> GetByExternalProviderAsync(
        AuthProvider authProvider,
        string providerUserId,
        CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
