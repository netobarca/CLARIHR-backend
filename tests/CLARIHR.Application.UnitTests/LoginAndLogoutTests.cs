using System.Reflection;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.Login;
using CLARIHR.Application.Features.Auth.Logout;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;

namespace CLARIHR.Application.UnitTests;

public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenEmailIsInvalid_ShouldReturnValidationError()
    {
        var result = _validator.Validate(new LoginCommand("invalid-email", "StrongPass123!"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LoginCommand.Email));
    }
}

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCredentialsAreValid_ShouldReturnAuthResponse()
    {
        var repository = new TestUserRepository();
        var user = User.RegisterLocal(
            "Ana",
            "Mendoza",
            "ana@clarihr.test",
            "hashed::StrongPass123!",
            "SV",
            "tests");
        repository.Seed(user);

        var handler = new LoginCommandHandler(
            repository,
            new TestPasswordHasher(),
            new SuccessfulTokenService());

        var result = await handler.Handle(
            new LoginCommand("ana@clarihr.test", "StrongPass123!"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("jwt-access-token", result.Value.AccessToken);
        Assert.Equal("refresh-token", result.Value.RefreshToken);
        Assert.Equal("ana@clarihr.test", result.Value.User.Email);
    }

    [Fact]
    public async Task Handle_WhenPasswordIsInvalid_ShouldReturnUnauthorized()
    {
        var repository = new TestUserRepository();
        var user = User.RegisterLocal(
            "Ana",
            "Mendoza",
            "ana@clarihr.test",
            "hashed::StrongPass123!",
            "SV",
            "tests");
        repository.Seed(user);

        var handler = new LoginCommandHandler(
            repository,
            new TestPasswordHasher(),
            new SuccessfulTokenService());

        var result = await handler.Handle(
            new LoginCommand("ana@clarihr.test", "WrongPass123!"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenUserIsExternal_ShouldReturnUnauthorized()
    {
        var repository = new TestUserRepository();
        var user = User.RegisterExternal(
            "Ana",
            "Mendoza",
            "ana.external@clarihr.test",
            AuthProvider.Google,
            "google-user-id",
            "SV",
            "tests");
        repository.Seed(user);

        var handler = new LoginCommandHandler(
            repository,
            new TestPasswordHasher(),
            new SuccessfulTokenService());

        var result = await handler.Handle(
            new LoginCommand("ana.external@clarihr.test", "StrongPass123!"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenEmailIsUnknown_ShouldStillRunPasswordVerificationToEqualizeTiming()
    {
        // AU-5: login must perform a password verification even when the email maps to no account, so the
        // response time does not reveal which emails are active local accounts (timing-based enumeration).
        var hasher = new CountingPasswordHasher();
        var handler = new LoginCommandHandler(
            new TestUserRepository(),
            hasher,
            new SuccessfulTokenService());

        var result = await handler.Handle(
            new LoginCommand("unknown@clarihr.test", "StrongPass123!"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, result.Error.Code);
        Assert.True(
            hasher.VerifyCount >= 1,
            "Login must verify a password even for unknown emails (timing equalization, AU-5).");
    }
}

public sealed class LogoutCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCurrentUserIdIsInvalid_ShouldReturnUnauthorized()
    {
        var handler = new LogoutCommandHandler(
            new TestCurrentUserService("not-a-guid"),
            new TestUserRepository(),
            new TestRefreshTokenRepository(),
            new FixedDateTimeProvider(DateTime.Parse("2026-03-07T10:00:00Z").ToUniversalTime()));

        var result = await handler.Handle(new LogoutCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal(AuthErrors.InvalidCurrentUser.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserExists_ShouldRevokeActiveRefreshTokens()
    {
        var repository = new TestUserRepository();
        var user = User.RegisterLocal(
            "Ana",
            "Mendoza",
            "ana@clarihr.test",
            "hashed::StrongPass123!",
            "SV",
            "tests");
        repository.Seed(user);

        var refreshTokens = new TestRefreshTokenRepository();
        var now = DateTime.Parse("2026-03-07T10:00:00Z").ToUniversalTime();
        var handler = new LogoutCommandHandler(
            new TestCurrentUserService(user.PublicId.ToString()),
            repository,
            refreshTokens,
            new FixedDateTimeProvider(now));

        var result = await handler.Handle(new LogoutCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, refreshTokens.RevokedUserId);
        Assert.Equal("logout", refreshTokens.RevokedReason);
        Assert.Equal(now, refreshTokens.RevokedUtc);
        Assert.True(refreshTokens.SaveChangesCalled);
    }
}

internal sealed class TestUserRepository : IUserRepository
{
    private static readonly MethodInfo EntityIdSetter = typeof(Entity)
        .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
        .GetSetMethod(nonPublic: true)!;

    private readonly List<User> _users = [];
    private long _nextId = 1;

    public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(_users.SingleOrDefault(user => user.Id == userId));

    public Task<User?> GetByPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken) =>
        Task.FromResult(_users.SingleOrDefault(user => user.PublicId == userPublicId));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = User.NormalizeEmail(email);
        return Task.FromResult(_users.SingleOrDefault(user => user.NormalizedEmail == normalizedEmail));
    }

    public Task<User?> GetByExternalProviderAsync(
        AuthProvider authProvider,
        string providerUserId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_users.SingleOrDefault(user =>
            user.AuthProvider == authProvider &&
            user.ProviderUserId == providerUserId));
    }

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        EnsureId(user);
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Seed(User user)
    {
        EnsureId(user);
        _users.Add(user);
    }

    private void EnsureId(Entity entity)
    {
        if (entity.Id != 0)
        {
            return;
        }

        EntityIdSetter.Invoke(entity, [_nextId++]);
    }
}

internal sealed class TestPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed::{password}";

    public bool Verify(string password, string passwordHash) => passwordHash == $"hashed::{password}";
}

internal sealed class CountingPasswordHasher : IPasswordHasher
{
    public int VerifyCount { get; private set; }

    public string Hash(string password) => $"hashed::{password}";

    public bool Verify(string password, string passwordHash)
    {
        VerifyCount++;
        return passwordHash == $"hashed::{password}";
    }
}

internal sealed class SuccessfulTokenService : ITokenService
{
    public Task<Result<AuthTokenResult>> GenerateAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(Result<AuthTokenResult>.Success(new AuthTokenResult(
            "jwt-access-token",
            RefreshToken: "refresh-token",
            ExpiresIn: 900)));

    public Task<Result<AuthTokenResult>> GenerateForTenantAsync(User user, Guid tenantId, CancellationToken cancellationToken) =>
        GenerateAsync(user, cancellationToken);

    public Task<Result<AuthTokenResult>> GeneratePlatformAsync(User user, CancellationToken cancellationToken) =>
        GenerateAsync(user, cancellationToken);

    public Task<Result<RefreshTokenExchangeResult>> RefreshAsync(
        string refreshToken,
        AuthClientType clientType,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result<RefreshTokenExchangeResult>.Failure(AuthErrors.RefreshTokenInvalid));
}

internal sealed class TestCurrentUserService(string? userId) : ICurrentUserService
{
    public bool IsAuthenticated => true;
    public string? UserId { get; } = userId;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
}

internal sealed class TestRefreshTokenRepository : IRefreshTokenRepository
{
    public long? RevokedUserId { get; private set; }

    public DateTime? RevokedUtc { get; private set; }

    public string? RevokedReason { get; private set; }

    public bool SaveChangesCalled { get; private set; }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult<RefreshToken?>(null);

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RevokeFamilyAsync(Guid familyId, DateTime revokedUtc, string reason, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task RevokeUserTokensAsync(
        long userId,
        AuthClientType clientType,
        DateTime revokedUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        RevokedUserId = userId;
        RevokedUtc = revokedUtc;
        RevokedReason = reason;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCalled = true;
        return Task.CompletedTask;
    }
}

internal sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; } = utcNow;
}
