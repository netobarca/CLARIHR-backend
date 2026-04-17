using CLARIHR.Domain.Preferences;

namespace CLARIHR.Application.Abstractions.Preferences;

public interface IUserPreferenceRepository
{
    void Add(UserPreference preference);

    Task<UserPreference?> GetByUserIdAsync(long userId, CancellationToken cancellationToken);

    Task<string?> ResolveLanguageAsync(long userId, CancellationToken cancellationToken);
}
