using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Domain.Preferences;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Preferences;

internal sealed class UserPreferenceRepository(ApplicationDbContext dbContext) : IUserPreferenceRepository
{
    public void Add(UserPreference preference) => dbContext.UserPreferences.Add(preference);

    public Task<UserPreference?> GetByUserIdAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.UserPreferences.SingleOrDefaultAsync(preference => preference.UserId == userId, cancellationToken);

    public Task<string?> ResolveLanguageAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.UserPreferences
            .AsNoTracking()
            .Where(preference => preference.UserId == userId)
            .Select(preference => preference.Language)
            .SingleOrDefaultAsync(cancellationToken);
}
