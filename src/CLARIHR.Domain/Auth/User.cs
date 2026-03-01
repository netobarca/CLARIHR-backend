using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Auth;

public sealed class User : AuditableEntity
{
    private User()
    {
    }

    private User(
        Guid publicId,
        string firstName,
        string lastName,
        string email,
        string? passwordHash,
        AuthProvider authProvider,
        string? providerUserId,
        string? country,
        string? source,
        UserStatus status)
    {
        var cleanedPasswordHash = AuthNormalization.CleanOptional(passwordHash);
        if (authProvider == AuthProvider.Local &&
            status == UserStatus.Active &&
            string.IsNullOrWhiteSpace(cleanedPasswordHash))
        {
            throw new ArgumentException("Password hash cannot be empty for active local users.", nameof(passwordHash));
        }

        if (authProvider != AuthProvider.Local && string.IsNullOrWhiteSpace(providerUserId))
        {
            throw new ArgumentException("Provider user id cannot be empty for external users.", nameof(providerUserId));
        }

        var normalizedEmail = AuthNormalization.NormalizeEmail(email);

        PublicId = publicId;
        FirstName = AuthNormalization.Clean(firstName, nameof(firstName));
        LastName = AuthNormalization.Clean(lastName, nameof(lastName));
        Email = normalizedEmail;
        NormalizedEmail = normalizedEmail;
        PasswordHash = cleanedPasswordHash;
        AuthProvider = authProvider;
        ProviderUserId = AuthNormalization.CleanOptional(providerUserId);
        Country = AuthNormalization.CleanOptional(country);
        Source = AuthNormalization.CleanOptional(source);
        Status = status;
    }

    public Guid PublicId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string? PasswordHash { get; private set; }

    public AuthProvider AuthProvider { get; private set; }

    public string? ProviderUserId { get; private set; }

    public string? Country { get; private set; }

    public string? Source { get; private set; }

    public UserStatus Status { get; private set; }

    public static User RegisterLocal(
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        string? country,
        string? source) =>
        new(
            Guid.NewGuid(),
            firstName,
            lastName,
            email,
            passwordHash,
            AuthProvider.Local,
            providerUserId: null,
            country,
            source,
            UserStatus.Active);

    public static User RegisterExternal(
        string firstName,
        string lastName,
        string email,
        AuthProvider authProvider,
        string providerUserId,
        string? country,
        string? source) =>
        new(
            Guid.NewGuid(),
            firstName,
            lastName,
            email,
            passwordHash: null,
            authProvider,
            providerUserId,
            country,
            source,
            UserStatus.Active);

    public static User InviteLocal(
        string firstName,
        string lastName,
        string email,
        string? country,
        string? source) =>
        new(
            Guid.NewGuid(),
            firstName,
            lastName,
            email,
            passwordHash: null,
            AuthProvider.Local,
            providerUserId: null,
            country,
            source,
            UserStatus.PendingActivation);

    public bool IsLinkedTo(AuthProvider authProvider, string providerUserId) =>
        AuthProvider == authProvider &&
        string.Equals(ProviderUserId, AuthNormalization.Clean(providerUserId, nameof(providerUserId)), StringComparison.Ordinal);

    public void EnsureExternalProviderLink(AuthProvider authProvider, string providerUserId)
    {
        if (authProvider == AuthProvider.Local)
        {
            throw new ArgumentException("External provider is required.", nameof(authProvider));
        }

        var cleanedProviderUserId = AuthNormalization.Clean(providerUserId, nameof(providerUserId));
        if (ProviderUserId is null)
        {
            AuthProvider = authProvider;
            ProviderUserId = cleanedProviderUserId;
            return;
        }

        if (IsLinkedTo(authProvider, cleanedProviderUserId))
        {
            return;
        }

        throw new InvalidOperationException("User is already linked to a different external provider.");
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = AuthNormalization.Clean(firstName, nameof(firstName));
        LastName = AuthNormalization.Clean(lastName, nameof(lastName));
    }

    public void Deactivate() => Status = UserStatus.Inactive;

    public void Reactivate()
    {
        Status = AuthProvider == AuthProvider.Local && string.IsNullOrWhiteSpace(PasswordHash)
            ? UserStatus.PendingActivation
            : UserStatus.Active;
    }

    public bool CanAuthenticate() =>
        Status == UserStatus.Active &&
        (AuthProvider != AuthProvider.Local || !string.IsNullOrWhiteSpace(PasswordHash));

    public static string NormalizeEmail(string email) => AuthNormalization.NormalizeEmail(email);
}
