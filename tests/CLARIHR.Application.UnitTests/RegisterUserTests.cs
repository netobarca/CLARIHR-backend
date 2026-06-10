using System.Reflection;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Preferences;

namespace CLARIHR.Application.UnitTests;

public sealed class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenPasswordDoesNotMeetPolicy_ShouldReturnValidationErrors()
    {
        var command = new RegisterUserCommand(
            FirstName: "Ana",
            LastName: "Mendoza",
            Email: "ana@clarihr.test",
            Password: "Pass2019!",
            Country: "SV",
            Source: "landing");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterUserCommand.Password));
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(RegisterUserCommand.Password) &&
                     error.ErrorMessage.Contains("at least 12 characters", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenPasswordContainsPersonalInfo_ShouldReturnValidationErrors()
    {
        var command = new RegisterUserCommand(
            FirstName: "Ana",
            LastName: "Mendoza",
            Email: "ana@clarihr.test",
            Password: "AnaSecure!45",
            Country: "SV",
            Source: "landing");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(RegisterUserCommand.Password) &&
                     error.ErrorMessage == "Password cannot contain your name or email.");
    }

    [Fact]
    public void Validate_WhenFirstNameContainsInvalidCharacters_ShouldReturnValidationErrors()
    {
        var command = new RegisterUserCommand(
            FirstName: "Ana123",
            LastName: "Mendoza",
            Email: "ana@clarihr.test",
            Password: "StrongP@ss1!",
            Country: "SV",
            Source: "landing");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterUserCommand.FirstName));
    }

    [Fact]
    public void Validate_WhenSourceContainsInvalidCharacters_ShouldReturnValidationErrors()
    {
        var command = new RegisterUserCommand(
            FirstName: "Ana",
            LastName: "Mendoza",
            Email: "ana@clarihr.test",
            Password: "StrongP@ss1!",
            Country: "SV",
            Source: "landing\npage");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterUserCommand.Source));
    }

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldNotReturnErrors()
    {
        var command = new RegisterUserCommand(
            FirstName: "Ana",
            LastName: "Mendoza",
            Email: "ana@clarihr.test",
            Password: "StrongP@ss1!",
            Country: "SV",
            Source: "landing");

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}

public sealed class RegisterUserCommandHandlerTests
{
    private static readonly DateTime FixedNow = DateTime.Parse("2026-06-10T10:00:00Z").ToUniversalTime();

    [Fact]
    public async Task Handle_WhenUserIsNew_ShouldCreatePendingUserAndSendVerificationWithoutTokens()
    {
        var repository = new TestUserRepository();
        var tokenRepository = new TestEmailVerificationTokenRepository();
        var emailService = new TestAuthEmailService();
        var unitOfWork = new TestUnitOfWork();
        var handler = CreateHandler(repository, tokenRepository, emailService, unitOfWork);

        var result = await handler.Handle(new RegisterUserCommand(
            "Carla",
            "Lopez",
            "carla@clarihr.test",
            "StrongP@ss1!",
            "SV",
            "landing"), CancellationToken.None);

        // AU-1: registration no longer mints a session — it creates a NON-usable account pending email
        // verification and emails a single-use link. The result carries no tokens.
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.NotNull(repository.AddedUser);
        Assert.Equal(UserStatus.PendingEmailVerification, repository.AddedUser!.Status);
        Assert.Equal("hashed::StrongP@ss1!", repository.AddedUser.PasswordHash);
        Assert.Single(tokenRepository.Added);
        Assert.Single(emailService.VerificationMessages);
        Assert.Equal("carla@clarihr.test", emailService.VerificationMessages[0].ToEmail);
        Assert.True(unitOfWork.Transaction.CommitCalled);
    }

    [Fact]
    public async Task Handle_WhenEmailExistsActive_ShouldReturnUniformSuccessWithoutVerification()
    {
        var repository = new TestUserRepository();
        repository.Seed(User.RegisterLocal("Ana", "Mendoza", "ana@clarihr.test", "existing-hash", "SV", "seed"));
        var tokenRepository = new TestEmailVerificationTokenRepository();
        var emailService = new TestAuthEmailService();
        var unitOfWork = new TestUnitOfWork();
        var handler = CreateHandler(repository, tokenRepository, emailService, unitOfWork);

        var result = await handler.Handle(new RegisterUserCommand(
            "Ana",
            "Mendoza",
            "ana@clarihr.test",
            "StrongP@ss1!",
            "SV",
            "landing"), CancellationToken.None);

        // Uniform success (anti-enumeration) and no mutation when the email already maps to an active account.
        Assert.True(result.IsSuccess);
        Assert.Null(repository.AddedUser);
        Assert.Empty(tokenRepository.Added);
        Assert.Empty(emailService.VerificationMessages);
    }

    [Fact]
    public async Task Handle_WhenEmailExistsPendingVerification_ShouldReissueAndResend()
    {
        var repository = new TestUserRepository();
        repository.Seed(User.RegisterLocalPendingVerification("Ana", "Mendoza", "ana@clarihr.test", "existing-hash", "SV", "seed"));
        var tokenRepository = new TestEmailVerificationTokenRepository();
        var emailService = new TestAuthEmailService();
        var unitOfWork = new TestUnitOfWork();
        var handler = CreateHandler(repository, tokenRepository, emailService, unitOfWork);

        var result = await handler.Handle(new RegisterUserCommand(
            "Ana",
            "Mendoza",
            "ana@clarihr.test",
            "StrongP@ss1!",
            "SV",
            "landing"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(repository.AddedUser);
        Assert.Single(tokenRepository.Added);
        Assert.Single(emailService.VerificationMessages);
    }

    [Fact]
    public async Task Handle_WhenPendingButWithinCooldown_ShouldNotResend()
    {
        var repository = new TestUserRepository();
        repository.Seed(User.RegisterLocalPendingVerification("Ana", "Mendoza", "ana@clarihr.test", "existing-hash", "SV", "seed"));
        var tokenRepository = new TestEmailVerificationTokenRepository { HasRecentRequest = true };
        var emailService = new TestAuthEmailService();
        var unitOfWork = new TestUnitOfWork();
        var handler = CreateHandler(repository, tokenRepository, emailService, unitOfWork);

        var result = await handler.Handle(new RegisterUserCommand(
            "Ana",
            "Mendoza",
            "ana@clarihr.test",
            "StrongP@ss1!",
            "SV",
            "landing"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(emailService.VerificationMessages);
    }

    private static RegisterUserCommandHandler CreateHandler(
        TestUserRepository repository,
        TestEmailVerificationTokenRepository tokenRepository,
        TestAuthEmailService emailService,
        TestUnitOfWork unitOfWork) =>
        new(
            repository,
            new TestUserPreferenceRepository(),
            new TestPasswordHasher(),
            tokenRepository,
            new TestEmailVerificationTokenHasher(),
            new TestEmailVerificationTokenGenerator(),
            emailService,
            new TestEmailVerificationLinkBuilder(),
            new TestEmailVerificationPolicyProvider(),
            new FixedDateTimeProvider(FixedNow),
            unitOfWork);

    private sealed class TestEmailVerificationTokenRepository : IEmailVerificationTokenRepository
    {
        public List<EmailVerificationToken> Added { get; } = [];

        public bool HasRecentRequest { get; init; }

        public void Add(EmailVerificationToken token) => Added.Add(token);

        public Task<EmailVerificationTokenResolution?> GetActiveByHashAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken) =>
            Task.FromResult<EmailVerificationTokenResolution?>(null);

        public Task RevokeActiveTokensAsync(long userId, DateTime revokedUtc, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<bool> HasRecentRequestAsync(long userId, DateTime sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult(HasRecentRequest);
    }

    private sealed class TestEmailVerificationTokenHasher : IEmailVerificationTokenHasher
    {
        public string Hash(string token) => $"vhash::{token}";
    }

    private sealed class TestEmailVerificationTokenGenerator : IEmailVerificationTokenGenerator
    {
        public string Generate() => "raw-verification-token";
    }

    private sealed class TestEmailVerificationLinkBuilder : IEmailVerificationLinkBuilder
    {
        public string Build(string token) => $"https://frontend/verify-email?token={token}";
    }

    private sealed class TestEmailVerificationPolicyProvider : IEmailVerificationPolicyProvider
    {
        public int GetTokenLifetimeMinutes() => 60;

        public int GetCooldownMinutes() => 2;
    }

    private sealed class TestAuthEmailService : IAuthEmailService
    {
        public List<EmailVerificationEmailMessage> VerificationMessages { get; } = [];

        public Task SendPasswordResetAsync(PasswordResetEmailMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SendEmailVerificationAsync(EmailVerificationEmailMessage message, CancellationToken cancellationToken)
        {
            VerificationMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class TestUserRepository : IUserRepository
    {
        private static readonly MethodInfo EntityIdSetter = typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!;

        private readonly List<User> _users = [];
        private long _nextId = 1;

        public User? AddedUser { get; private set; }

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
            AddedUser = user;
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

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed::{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == $"hashed::{password}";
    }

    private sealed class TestUserPreferenceRepository : IUserPreferenceRepository
    {
        public void Add(UserPreference preference)
        {
        }

        public Task<UserPreference?> GetByUserIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserPreference?>(null);

        public Task<string?> ResolveLanguageAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }
}
