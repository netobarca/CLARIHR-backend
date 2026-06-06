using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Abstractions.Auth;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken);

    Task<User?> GetByPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken);

    /// <summary>
    /// Batch variant of <see cref="GetByPublicIdAsync"/>. The default falls back to per-id lookups
    /// so existing implementations keep working; the EF repository overrides it with a single
    /// batched query (used to de-N+1 the position-slot role cascade).
    /// </summary>
    async Task<IReadOnlyList<User>> GetByPublicIdsAsync(
        IReadOnlyCollection<Guid> userPublicIds,
        CancellationToken cancellationToken)
    {
        var users = new List<User>(userPublicIds.Count);
        foreach (var userPublicId in userPublicIds)
        {
            var user = await GetByPublicIdAsync(userPublicId, cancellationToken);
            if (user is not null)
            {
                users.Add(user);
            }
        }

        return users;
    }

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<User?> GetByExternalProviderAsync(
        AuthProvider authProvider,
        string providerUserId,
        CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
