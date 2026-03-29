using System.Reflection;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.External;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;

namespace CLARIHR.Application.UnitTests;

public sealed class RegisterExternalUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTokenIsInvalid_ShouldReturnUnauthorized()
    {
        var unitOfWork = new TestUnitOfWork();
        var companyTypeCatalogSeedService = new TestCompanyTypeCatalogSeedService();
        var handler = new RegisterExternalUserCommandHandler(
            new TestUserRepository(),
            new FailingExternalAuthProviderService(AuthErrors.ExternalTokenInvalid),
            new TestTokenService(),
            companyTypeCatalogSeedService,
            unitOfWork);

        var result = await handler.Handle(new RegisterExternalUserCommand(
            AuthProvider.Google,
            "invalid-token",
            "SV",
            "landing"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal(AuthErrors.ExternalTokenInvalid.Code, result.Error.Code);
        Assert.Empty(companyTypeCatalogSeedService.SeededOwnerUserIds);
    }

    [Fact]
    public async Task Handle_WhenValidatedTokenHasNoEmail_ShouldReturnUnprocessableEntity()
    {
        var unitOfWork = new TestUnitOfWork();
        var companyTypeCatalogSeedService = new TestCompanyTypeCatalogSeedService();
        var handler = new RegisterExternalUserCommandHandler(
            new TestUserRepository(),
            new SuccessfulExternalAuthProviderService(new ExternalAuthValidationResult(
                Email: null,
                FirstName: "Ana",
                LastName: "Mendoza",
                ProviderUserId: "google-123",
                Provider: AuthProvider.Google,
                CanAutoLinkByEmail: false)),
            new TestTokenService(),
            companyTypeCatalogSeedService,
            unitOfWork);

        var result = await handler.Handle(new RegisterExternalUserCommand(
            AuthProvider.Google,
            "mock-token",
            "SV",
            "landing"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.UnprocessableEntity, result.Error.Type);
        Assert.Equal(AuthErrors.ExternalEmailMissing.Code, result.Error.Code);
        Assert.Empty(companyTypeCatalogSeedService.SeededOwnerUserIds);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldCreateUserAndReturnCreatedResult()
    {
        var repository = new TestUserRepository();
        var unitOfWork = new TestUnitOfWork();
        var companyTypeCatalogSeedService = new TestCompanyTypeCatalogSeedService();
        var handler = new RegisterExternalUserCommandHandler(
            repository,
            new SuccessfulExternalAuthProviderService(new ExternalAuthValidationResult(
                Email: "ana@clarihr.test",
                FirstName: "Ana",
                LastName: "Mendoza",
                ProviderUserId: "google-123",
                Provider: AuthProvider.Google,
                CanAutoLinkByEmail: true)),
            new TestTokenService(),
            companyTypeCatalogSeedService,
            unitOfWork);

        var result = await handler.Handle(new RegisterExternalUserCommand(
            AuthProvider.Google,
            "mock-token",
            "SV",
            "landing"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.WasCreated);
        Assert.NotNull(repository.AddedUser);
        Assert.Null(repository.AddedUser!.PasswordHash);
        Assert.Equal("google-123", repository.AddedUser.ProviderUserId);
        Assert.Equal(AuthProvider.Google, repository.AddedUser.AuthProvider);
        Assert.Equal("refresh-token", result.Value.Response.RefreshToken);
        Assert.Equal([repository.AddedUser.PublicId], companyTypeCatalogSeedService.SeededOwnerUserIds);
        Assert.True(unitOfWork.Transaction.CommitCalled);
    }

    [Fact]
    public async Task Handle_WhenUserExists_ShouldReturnOkAndLinkProviderIfMissing()
    {
        var repository = new TestUserRepository();
        var unitOfWork = new TestUnitOfWork();
        var companyTypeCatalogSeedService = new TestCompanyTypeCatalogSeedService();
        repository.Seed(User.RegisterLocal(
            "Carla",
            "Lopez",
            "carla@clarihr.test",
            "hashed-password",
            "SV",
            "seed"));

        var handler = new RegisterExternalUserCommandHandler(
            repository,
            new SuccessfulExternalAuthProviderService(new ExternalAuthValidationResult(
                Email: "carla@clarihr.test",
                FirstName: "Carla",
                LastName: "Lopez",
                ProviderUserId: "google-456",
                Provider: AuthProvider.Google,
                CanAutoLinkByEmail: true)),
            new TestTokenService(),
            companyTypeCatalogSeedService,
            unitOfWork);

        var result = await handler.Handle(new RegisterExternalUserCommand(
            AuthProvider.Google,
            "mock-token",
            "SV",
            "landing"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.WasCreated);
        Assert.NotNull(repository.LastSavedUser);
        Assert.Equal("google-456", repository.LastSavedUser!.ProviderUserId);
        Assert.Equal(AuthProvider.Google, repository.LastSavedUser.AuthProvider);
        Assert.Equal("refresh-token", result.Value.Response.RefreshToken);
        Assert.Empty(companyTypeCatalogSeedService.SeededOwnerUserIds);
        Assert.True(unitOfWork.Transaction.CommitCalled);
    }

    [Fact]
    public async Task Handle_WhenExistingUserCannotBeLinkedSafelyByEmail_ShouldReturnConflict()
    {
        var repository = new TestUserRepository();
        var unitOfWork = new TestUnitOfWork();
        var companyTypeCatalogSeedService = new TestCompanyTypeCatalogSeedService();
        repository.Seed(User.RegisterLocal(
            "Luisa",
            "Martinez",
            "luisa@example.com",
            "hashed-password",
            "SV",
            "seed"));

        var handler = new RegisterExternalUserCommandHandler(
            repository,
            new SuccessfulExternalAuthProviderService(new ExternalAuthValidationResult(
                Email: "luisa@example.com",
                FirstName: "Luisa",
                LastName: "Martinez",
                ProviderUserId: "google-789",
                Provider: AuthProvider.Google,
                CanAutoLinkByEmail: false)),
            new TestTokenService(),
            companyTypeCatalogSeedService,
            unitOfWork);

        var result = await handler.Handle(new RegisterExternalUserCommand(
            AuthProvider.Google,
            "mock-token",
            "SV",
            "landing"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(AuthErrors.ExternalEmailLinkNotAllowed.Code, result.Error.Code);
        Assert.True(unitOfWork.Transaction.RollbackCalled);
        Assert.Empty(companyTypeCatalogSeedService.SeededOwnerUserIds);
    }

    [Fact]
    public async Task Handle_WhenTokenGenerationFails_ShouldReturnTokenErrorAndRollback()
    {
        var repository = new TestUserRepository();
        var unitOfWork = new TestUnitOfWork();
        var companyTypeCatalogSeedService = new TestCompanyTypeCatalogSeedService();
        var tokenService = new FailingTestTokenService(AuthErrors.TokenConfigurationInvalid);

        var handler = new RegisterExternalUserCommandHandler(
            repository,
            new SuccessfulExternalAuthProviderService(new ExternalAuthValidationResult(
                Email: "ana@clarihr.test",
                FirstName: "Ana",
                LastName: "Mendoza",
                ProviderUserId: "google-123",
                Provider: AuthProvider.Google,
                CanAutoLinkByEmail: true)),
            tokenService,
            companyTypeCatalogSeedService,
            unitOfWork);

        var result = await handler.Handle(new RegisterExternalUserCommand(
            AuthProvider.Google,
            "mock-token",
            "SV",
            "landing"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.TokenConfigurationInvalid.Code, result.Error.Code);
        Assert.True(unitOfWork.Transaction.RollbackCalled);
        Assert.Single(companyTypeCatalogSeedService.SeededOwnerUserIds);
    }

    private sealed class TestCompanyTypeCatalogSeedService : ICompanyTypeCatalogSeedService
    {
        public List<Guid> SeededOwnerUserIds { get; } = [];

        public Task EnsureSeededAsync(Guid ownerUserPublicId, CancellationToken cancellationToken)
        {
            SeededOwnerUserIds.Add(ownerUserPublicId);
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

        public User? LastSavedUser { get; private set; }

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

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            LastSavedUser = _users.LastOrDefault();
            return Task.CompletedTask;
        }

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

    private sealed class SuccessfulExternalAuthProviderService(
        ExternalAuthValidationResult validationResult) : IExternalAuthProviderService
    {
        public Task<Result<ExternalAuthValidationResult>> ValidateAsync(
            AuthProvider provider,
            string idToken,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<ExternalAuthValidationResult>.Success(validationResult));
    }

    private sealed class FailingExternalAuthProviderService(Error error) : IExternalAuthProviderService
    {
        public Task<Result<ExternalAuthValidationResult>> ValidateAsync(
            AuthProvider provider,
            string idToken,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<ExternalAuthValidationResult>.Failure(error));
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

    private sealed class FailingTestTokenService(Error error) : ITokenService
    {
        public Task<Result<AuthTokenResult>> GenerateAsync(User user, CancellationToken cancellationToken) =>
            Task.FromResult(Result<AuthTokenResult>.Failure(error));

        public Task<Result<AuthTokenResult>> GenerateForTenantAsync(User user, Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(Result<AuthTokenResult>.Failure(error));

        public Task<Result<RefreshTokenExchangeResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
            Task.FromResult(Result<RefreshTokenExchangeResult>.Failure(AuthErrors.RefreshTokenInvalid));
    }
}
