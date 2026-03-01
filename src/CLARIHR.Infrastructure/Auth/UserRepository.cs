using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Domain.Auth;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class UserRepository(ApplicationDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.AuthUsers.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<User?> GetByPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken) =>
        dbContext.AuthUsers.SingleOrDefaultAsync(user => user.PublicId == userPublicId, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = User.NormalizeEmail(email);

        return dbContext.AuthUsers
            .SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public Task<User?> GetByExternalProviderAsync(
        AuthProvider authProvider,
        string providerUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.AuthUsers
            .SingleOrDefaultAsync(
                user => user.AuthProvider == authProvider && user.ProviderUserId == providerUserId,
                cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _ = await dbContext.AuthUsers.AddAsync(user, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
