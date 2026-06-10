using System.Reflection;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.PasswordReset;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;

namespace CLARIHR.Application.UnitTests;

public sealed class PasswordResetAdministrationTests
{
    [Fact]
    public async Task RequestPasswordResetCommandHandler_WhenUserIsEligible_ShouldIssueTokenAndSendEmail()
    {
        var user = CreateUser(7, Guid.NewGuid(), "reset@clarihr.test", UserStatus.Active, AuthProvider.Local);
        var userRepository = new TestPasswordResetUserRepository(user);
        var tokenRepository = new TestPasswordResetTokenRepository();
        var emailService = new TestAuthEmailService();
        var auditService = new TestAuditService();
        var handler = new RequestPasswordResetCommandHandler(
            userRepository,
            tokenRepository,
            new TestPasswordResetTokenHasher(),
            new TestPasswordResetTokenGenerator(),
            emailService,
            new FixedDateTimeProvider(new DateTime(2026, 4, 23, 12, 0, 0, DateTimeKind.Utc)),
            new TestPasswordResetLinkBuilder(),
            new TestPasswordResetPolicyProvider(),
            auditService,
            new TestUnitOfWork());

        var result = await handler.Handle(new RequestPasswordResetCommand(user.Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(tokenRepository.Items);
        Assert.Single(emailService.Messages);
        Assert.Single(auditService.Entries);
        Assert.Equal("https://frontend/reset-password?token=raw-reset-token", emailService.Messages[0].ResetLink);
    }

    [Fact]
    public async Task RedeemPasswordResetCommandHandler_WhenTokenIsValid_ShouldUpdatePasswordAndRevokeSessions()
    {
        var user = CreateUser(9, Guid.NewGuid(), "redeem@clarihr.test", UserStatus.Active, AuthProvider.Local);
        var token = PasswordResetToken.Issue(user.Id, "HASH::raw-reset-token", new DateTime(2026, 4, 23, 13, 0, 0, DateTimeKind.Utc));
        var userRepository = new TestPasswordResetUserRepository(user);
        var tokenRepository = new TestPasswordResetTokenRepository(user, token);
        var refreshTokenRepository = new TestRefreshTokenRepository();
        var handler = new RedeemPasswordResetCommandHandler(
            userRepository,
            tokenRepository,
            new TestPasswordResetTokenHasher(),
            new TestPasswordHasher(),
            refreshTokenRepository,
            new FixedDateTimeProvider(new DateTime(2026, 4, 23, 12, 30, 0, DateTimeKind.Utc)),
            new TestAuditService(),
            new TestUnitOfWork());

        var result = await handler.Handle(
            new RedeemPasswordResetCommand("raw-reset-token", "StrongerPass123!"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("HASH::StrongerPass123!", user.PasswordHash);
        Assert.True(token.IsUsed);
        Assert.Equal(2, refreshTokenRepository.RevokedUsers.Count);
        Assert.All(refreshTokenRepository.RevokedUsers, revokedUserId => Assert.Equal(user.Id, revokedUserId));
    }

    [Fact]
    public async Task ValidatePasswordResetTokenQueryHandler_WhenTokenIsMissing_ShouldReturnUnauthorized()
    {
        var handler = new ValidatePasswordResetTokenQueryHandler(
            new TestPasswordResetTokenRepository(),
            new TestPasswordResetTokenHasher(),
            new FixedDateTimeProvider(new DateTime(2026, 4, 23, 12, 0, 0, DateTimeKind.Utc)));

        var result = await handler.Handle(new ValidatePasswordResetTokenQuery("missing"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.PasswordResetTokenInvalid.Code, result.Error.Code);
    }

    private static User CreateUser(long id, Guid publicId, string email, UserStatus status, AuthProvider authProvider)
    {
        var user = authProvider == AuthProvider.Local
            ? User.RegisterLocal("Alicia", "Reset", email, "HASH::old", "SV", "tests")
            : User.RegisterExternal("Alicia", "Reset", email, authProvider, "provider-user", "SV", "tests");

        if (status == UserStatus.Inactive)
        {
            user.Deactivate();
        }

        SetEntityId(user, id);
        SetPublicId(user, publicId);
        return user;
    }

    private static void SetEntityId(Entity entity, long id)
    {
        var setter = typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!;

        setter.Invoke(entity, [id]);
    }

    private static void SetPublicId(Entity entity, Guid publicId)
    {
        var setter = typeof(Entity)
            .GetProperty(nameof(Entity.PublicId), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!;

        setter.Invoke(entity, [publicId]);
    }
}

file sealed class TestPasswordResetUserRepository(params User[] users) : IUserRepository
{
    private readonly Dictionary<long, User> _usersById = users.ToDictionary(user => user.Id);
    private readonly Dictionary<Guid, User> _usersByPublicId = users.ToDictionary(user => user.PublicId);

    public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(_usersById.GetValueOrDefault(userId));

    public Task<User?> GetByPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken) =>
        Task.FromResult(_usersByPublicId.GetValueOrDefault(userPublicId));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = User.NormalizeEmail(email);
        return Task.FromResult(_usersById.Values.SingleOrDefault(user => user.NormalizedEmail == normalizedEmail));
    }

    public Task<User?> GetByExternalProviderAsync(AuthProvider authProvider, string providerUserId, CancellationToken cancellationToken) =>
        Task.FromResult(_usersById.Values.SingleOrDefault(user => user.AuthProvider == authProvider && user.ProviderUserId == providerUserId));

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _usersById[user.Id] = user;
        _usersByPublicId[user.PublicId] = user;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

file sealed class TestPasswordResetTokenRepository(User? seededUser = null, PasswordResetToken? seededToken = null) : IPasswordResetTokenRepository
{
    public List<PasswordResetToken> Items { get; } = seededToken is null ? [] : [seededToken];

    public void Add(PasswordResetToken token) => Items.Add(token);

    public Task<PasswordResetTokenResolution?> GetActiveByHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (seededUser is null)
        {
            return Task.FromResult<PasswordResetTokenResolution?>(null);
        }

        return Task.FromResult<PasswordResetTokenResolution?>(
            Items
                .Where(token => token.TokenHash == tokenHash && token.IsActive(utcNow))
                .Select(token => new PasswordResetTokenResolution(
                    token,
                    seededUser.PublicId,
                    seededUser.Email,
                    seededUser.FirstName,
                    seededUser.LastName))
                .SingleOrDefault());
    }

    public Task RevokeActiveTokensAsync(long userId, DateTime revokedUtc, CancellationToken cancellationToken)
    {
        foreach (var token in Items.Where(token => token.UserId == userId && !token.IsUsed && token.RevokedUtc is null))
        {
            token.Revoke(revokedUtc);
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasRecentRequestAsync(long userId, DateTime sinceUtc, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}

file sealed class TestPasswordResetTokenHasher : IPasswordResetTokenHasher
{
    public string Hash(string token) => $"HASH::{token}";
}

file sealed class TestPasswordResetTokenGenerator : IPasswordResetTokenGenerator
{
    public string Generate() => "raw-reset-token";
}

file sealed class TestAuthEmailService : IAuthEmailService
{
    public List<PasswordResetEmailMessage> Messages { get; } = [];

    public Task SendPasswordResetAsync(PasswordResetEmailMessage message, CancellationToken cancellationToken)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(EmailVerificationEmailMessage message, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

file sealed class TestPasswordResetLinkBuilder : IPasswordResetLinkBuilder
{
    public string Build(string token) => $"https://frontend/reset-password?token={token}";
}

file sealed class TestPasswordResetPolicyProvider : IPasswordResetPolicyProvider
{
    public int GetTokenLifetimeMinutes() => 15;

    public int GetCooldownMinutes() => 2;
}

file sealed class TestPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"HASH::{password}";

    public bool Verify(string password, string passwordHash) => Hash(password) == passwordHash;
}

file sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow => utcNow;
}

file sealed class TestRefreshTokenRepository : IRefreshTokenRepository
{
    public List<long> RevokedUsers { get; } = [];

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task RevokeFamilyAsync(Guid familyId, DateTime revokedUtc, string reason, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task RevokeUserTokensAsync(
        long userId,
        AuthClientType clientType,
        DateTime revokedUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        RevokedUsers.Add(userId);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

file sealed class TestAuditService : IAuditService
{
    public List<AuditLogEntry> Entries { get; } = [];

    public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}

file sealed class TestUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new TestUnitOfWorkTransaction());
}

file sealed class TestUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
