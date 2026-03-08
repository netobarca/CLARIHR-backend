using System.Reflection;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.Provisioning;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.LegalRepresentatives;

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
            Password: "weakpass",
            CompanyName: null,
            Country: "SV",
            Source: "landing");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterUserCommand.Password));
    }

    [Fact]
    public void Validate_WhenFirstNameContainsInvalidCharacters_ShouldReturnValidationErrors()
    {
        var command = new RegisterUserCommand(
            FirstName: "Ana123",
            LastName: "Mendoza",
            Email: "ana@clarihr.test",
            Password: "StrongP@ss1",
            CompanyName: null,
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
            Password: "StrongP@ss1",
            CompanyName: null,
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
            Password: "StrongP@ss1",
            CompanyName: null,
            Country: "SV",
            Source: "landing",
            InitialLegalRepresentative: CreateInitialLegalRepresentative());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    private static InitialLegalRepresentativeInput CreateInitialLegalRepresentative() =>
        new(
            "Ana",
            "Mendoza",
            LegalRepresentativeDocumentType.TaxId,
            "0614-290190-102-3",
            "Representante Legal",
            LegalRepresentativeRepresentationType.PrimaryLegalRepresentative,
            "Representación general",
            "Acta de nombramiento",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            null,
            "ana@clarihr.test",
            "+50370000000",
            IsPrimary: true);
}

public sealed class RegisterUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ShouldReturnConflict()
    {
        var repository = new TestUserRepository();
        var dispatcher = new TestProvisioningCommandDispatcher();
        var unitOfWork = new TestUnitOfWork();
        repository.Seed(User.RegisterLocal("Ana", "Mendoza", "ana@clarihr.test", "existing-hash", "SV", "seed"));

        var handler = new RegisterUserCommandHandler(
            repository,
            new TestPasswordHasher(),
            new TestTokenService(),
            dispatcher,
            unitOfWork);

        var result = await handler.Handle(new RegisterUserCommand(
            "Ana",
            "Mendoza",
            "ana@clarihr.test",
            "StrongP@ss1",
            null,
            "SV",
            "landing",
            CreateInitialLegalRepresentative()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UserAlreadyExists.Code, result.Error.Code);
        Assert.True(unitOfWork.Transaction.RollbackCalled);
    }

    [Fact]
    public async Task Handle_WhenUserIsNew_ShouldCreateUserAndReturnTokens()
    {
        var repository = new TestUserRepository();
        var passwordHasher = new TestPasswordHasher();
        var tokenService = new TestTokenService();
        var dispatcher = new TestProvisioningCommandDispatcher();
        var unitOfWork = new TestUnitOfWork();
        var handler = new RegisterUserCommandHandler(repository, passwordHasher, tokenService, dispatcher, unitOfWork);

        var result = await handler.Handle(new RegisterUserCommand(
            "Carla",
            "Lopez",
            "carla@clarihr.test",
            "StrongP@ss1",
            "ClariHR Demo",
            "SV",
            "landing",
            CreateInitialLegalRepresentative()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.AddedUser);
        Assert.Equal("hashed::StrongP@ss1", repository.AddedUser!.PasswordHash);
        Assert.Equal("jwt-access-token", result.Value.AccessToken);
        Assert.Equal("refresh-token", result.Value.RefreshToken);
        Assert.Equal(900, result.Value.ExpiresIn);
        Assert.Equal("carla@clarihr.test", result.Value.User.Email);
        Assert.Equal(AuthProvider.Local, result.Value.User.AuthProvider);
        Assert.Equal("ClariHR Demo", dispatcher.LastCommand!.CompanyName);
        Assert.True(unitOfWork.Transaction.CommitCalled);
    }

    [Fact]
    public async Task Handle_WhenProvisioningFails_ShouldReturnProvisioningErrorAndRollback()
    {
        var repository = new TestUserRepository();
        var dispatcher = new TestProvisioningCommandDispatcher
        {
            NextResult = Result<ProvisionCompanyForUserResult>.Failure(ProvisioningErrors.ProvisioningFailed)
        };
        var unitOfWork = new TestUnitOfWork();

        var handler = new RegisterUserCommandHandler(
            repository,
            new TestPasswordHasher(),
            new TestTokenService(),
            dispatcher,
            unitOfWork);

        var result = await handler.Handle(new RegisterUserCommand(
            "Carla",
            "Lopez",
            "carla@clarihr.test",
            "StrongP@ss1",
            null,
            "SV",
            "landing",
            CreateInitialLegalRepresentative()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ProvisioningErrors.ProvisioningFailed.Code, result.Error.Code);
        Assert.True(unitOfWork.Transaction.RollbackCalled);
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

    private sealed class TestTokenService : ITokenService
    {
        public Task<Result<AuthTokenResult>> GenerateAsync(User user, CancellationToken cancellationToken) =>
            Task.FromResult(Result<AuthTokenResult>.Success(new AuthTokenResult(
                "jwt-access-token",
                RefreshToken: "refresh-token",
                ExpiresIn: 900)));

        public Task<Result<AuthTokenResult>> GenerateForTenantAsync(User user, Guid tenantId, CancellationToken cancellationToken) =>
            GenerateAsync(user, cancellationToken);

        public Task<Result<RefreshTokenExchangeResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
            Task.FromResult(Result<RefreshTokenExchangeResult>.Failure(AuthErrors.RefreshTokenInvalid));
    }

    private static InitialLegalRepresentativeInput CreateInitialLegalRepresentative() =>
        new(
            "Carla",
            "Lopez",
            LegalRepresentativeDocumentType.TaxId,
            "0614-000001-001-1",
            "Representante Legal",
            LegalRepresentativeRepresentationType.PrimaryLegalRepresentative,
            "Representación general",
            "Acta de nombramiento",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            null,
            "carla@clarihr.test",
            "+50370000001",
            IsPrimary: true);
}
