using System.Reflection;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Preferences.Common;
using CLARIHR.Application.Features.Preferences.Company;
using CLARIHR.Application.Features.Preferences.User;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Preferences;

namespace CLARIHR.Application.UnitTests;

public sealed class UserPreferenceAdministrationTests
{
    [Fact]
    public void UpdateCurrentUserPreferencesCommandValidator_WhenLanguageHasInvalidFormat_ShouldReturnValidationError()
    {
        var validator = new UpdateCurrentUserPreferencesCommandValidator();

        var result = validator.Validate(new UpdateCurrentUserPreferencesCommand("es-sv"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateCurrentUserPreferencesCommand.Language));
    }

    [Fact]
    public async Task GetCurrentUserPreferencesQueryHandler_WhenPreferenceDoesNotExist_ShouldCreateDefaultLanguage()
    {
        var user = SeededUser(5, Guid.NewGuid(), "ana@clarihr.test");
        var currentUserService = new TestCurrentUserService(user.PublicId.ToString());
        var userRepository = new TestUserRepository(user);
        var preferenceRepository = new TestUserPreferenceRepository();
        var unitOfWork = new TestUnitOfWork();

        var handler = new GetCurrentUserPreferencesQueryHandler(
            currentUserService,
            userRepository,
            preferenceRepository,
            unitOfWork);

        var result = await handler.Handle(new GetCurrentUserPreferencesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("en", result.Value.Language);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Equal("en", preferenceRepository.GetByUserId(user.Id)!.Language);
    }

    [Fact]
    public async Task UpdateCurrentUserPreferencesCommandHandler_WhenPreferenceExists_ShouldUpdateLanguage()
    {
        var user = SeededUser(11, Guid.NewGuid(), "carla@clarihr.test");
        var currentUserService = new TestCurrentUserService(user.PublicId.ToString());
        var userRepository = new TestUserRepository(user);
        var preferenceRepository = new TestUserPreferenceRepository();
        preferenceRepository.Add(UserPreference.Create(user.Id, "en"));

        var unitOfWork = new TestUnitOfWork();
        var handler = new UpdateCurrentUserPreferencesCommandHandler(
            currentUserService,
            userRepository,
            preferenceRepository,
            unitOfWork);

        var result = await handler.Handle(new UpdateCurrentUserPreferencesCommand("it"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("it", result.Value.Language);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Equal("it", preferenceRepository.GetByUserId(user.Id)!.Language);
    }

    [Fact]
    public void ReplaceCurrentUserSocialLinksCommandValidator_WhenProviderCodesRepeat_ShouldReturnValidationError()
    {
        var validator = new ReplaceCurrentUserSocialLinksCommandValidator();

        var result = validator.Validate(new ReplaceCurrentUserSocialLinksCommand(
        [
            new UpdateCurrentUserSocialLinkItem("linkedin", "https://linkedin.com/in/ana"),
            new UpdateCurrentUserSocialLinkItem("LINKEDIN", "https://linkedin.com/in/ana-2")
        ]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == "Provider codes must be unique.");
    }

    [Fact]
    public async Task ReplaceCurrentUserSocialLinksCommandHandler_WhenRequestIsValid_ShouldReplaceLinks()
    {
        var user = SeededUser(17, Guid.NewGuid(), "diana@clarihr.test");
        var currentUserService = new TestCurrentUserService(user.PublicId.ToString());
        var userRepository = new TestUserRepository(user);
        var preferenceRepository = new TestUserPreferenceRepository(UserPreference.Create(user.Id, "en"));
        var unitOfWork = new TestUnitOfWork();
        var handler = new ReplaceCurrentUserSocialLinksCommandHandler(
            currentUserService,
            userRepository,
            preferenceRepository,
            unitOfWork);

        var result = await handler.Handle(
            new ReplaceCurrentUserSocialLinksCommand(
            [
                new UpdateCurrentUserSocialLinkItem("linkedin", "https://linkedin.com/in/diana"),
                new UpdateCurrentUserSocialLinkItem("github", "https://github.com/diana")
            ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.SocialLinks.Count);
        Assert.Collection(
            result.Value.SocialLinks,
            link =>
            {
                Assert.Equal("LINKEDIN", link.ProviderCode);
                Assert.Equal("https://linkedin.com/in/diana", link.Url);
            },
            link =>
            {
                Assert.Equal("GITHUB", link.ProviderCode);
                Assert.Equal("https://github.com/diana", link.Url);
            });
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    private static User SeededUser(long id, Guid publicId, string email)
    {
        var user = User.RegisterLocal("Ana", "Mendoza", email, "hash", "SV", "tests");
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

public sealed class CompanyPreferenceAdministrationTests
{
    [Fact]
    public async Task GetCompanyPreferencesQueryHandler_WhenAuthorizationFails_ShouldReturnFailure()
    {
        var authorizationService = new TestCompanyPreferenceAuthorizationService(
            readResult: Result.Failure(PreferenceErrors.CompanyForbidden),
            manageResult: Result.Success());

        var repository = new TestCompanyPreferenceRepository();
        var handler = new GetCompanyPreferencesQueryHandler(authorizationService, repository);

        var result = await handler.Handle(new GetCompanyPreferencesQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PreferenceErrors.CompanyForbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task UpdateCompanyPreferencesCommandHandler_WhenConcurrencyTokenDiffers_ShouldReturnConflict()
    {
        var tenantId = Guid.NewGuid();
        var preference = CompanyPreference.Create("USD", "UTC");
        preference.SetTenantId(tenantId);

        var authorizationService = new TestCompanyPreferenceAuthorizationService(Result.Success(), Result.Success());
        var repository = new TestCompanyPreferenceRepository(preference);
        var unitOfWork = new TestUnitOfWork();
        var handler = new UpdateCompanyPreferencesCommandHandler(authorizationService, repository, unitOfWork);

        var result = await handler.Handle(
            new UpdateCompanyPreferencesCommand(tenantId, "EUR", "Europe/Madrid", Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PreferenceErrors.ConcurrencyConflict.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateCompanyPreferencesCommandHandler_WhenRequestIsValid_ShouldUpdatePreference()
    {
        var tenantId = Guid.NewGuid();
        var preference = CompanyPreference.Create("USD", "UTC");
        preference.SetTenantId(tenantId);
        var expectedToken = preference.ConcurrencyToken;

        var authorizationService = new TestCompanyPreferenceAuthorizationService(Result.Success(), Result.Success());
        var repository = new TestCompanyPreferenceRepository(preference);
        var unitOfWork = new TestUnitOfWork();
        var handler = new UpdateCompanyPreferencesCommandHandler(authorizationService, repository, unitOfWork);

        var result = await handler.Handle(
            new UpdateCompanyPreferencesCommand(tenantId, "EUR", "Europe/Madrid", expectedToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("EUR", result.Value.CurrencyCode);
        Assert.Equal("Europe/Madrid", result.Value.TimeZone);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task PatchCompanyPreferencesCommandHandler_WhenConcurrencyTokenDiffers_ShouldReturnConflict()
    {
        var tenantId = Guid.NewGuid();
        var preference = CompanyPreference.Create("USD", "UTC");
        preference.SetTenantId(tenantId);

        var authorizationService = new TestCompanyPreferenceAuthorizationService(Result.Success(), Result.Success());
        var repository = new TestCompanyPreferenceRepository(preference);
        var unitOfWork = new TestUnitOfWork();
        var handler = new PatchCompanyPreferencesCommandHandler(authorizationService, repository, unitOfWork);

        var result = await handler.Handle(
            new PatchCompanyPreferencesCommand(
                tenantId,
                Guid.NewGuid(),
                [PatchOp("replace", "/currencyCode", "EUR")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PreferenceErrors.ConcurrencyConflict.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task PatchCompanyPreferencesCommandHandler_WhenRequestIsValid_ShouldApplyPatch()
    {
        var tenantId = Guid.NewGuid();
        var preference = CompanyPreference.Create("USD", "UTC");
        preference.SetTenantId(tenantId);
        var expectedToken = preference.ConcurrencyToken;

        var authorizationService = new TestCompanyPreferenceAuthorizationService(Result.Success(), Result.Success());
        var repository = new TestCompanyPreferenceRepository(preference);
        var unitOfWork = new TestUnitOfWork();
        var handler = new PatchCompanyPreferencesCommandHandler(authorizationService, repository, unitOfWork);

        var result = await handler.Handle(
            new PatchCompanyPreferencesCommand(
                tenantId,
                expectedToken,
                [
                    PatchOp("replace", "/currencyCode", "EUR"),
                    PatchOp("replace", "/timeZone", "Europe/Madrid")
                ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("EUR", result.Value.CurrencyCode);
        Assert.Equal("Europe/Madrid", result.Value.TimeZone);
        Assert.NotEqual(expectedToken, result.Value.ConcurrencyToken);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task PatchCompanyPreferencesCommandHandler_WhenPathIsNotPatchable_ShouldReturnValidationFailure()
    {
        var tenantId = Guid.NewGuid();
        var preference = CompanyPreference.Create("USD", "UTC");
        preference.SetTenantId(tenantId);
        var expectedToken = preference.ConcurrencyToken;

        var authorizationService = new TestCompanyPreferenceAuthorizationService(Result.Success(), Result.Success());
        var repository = new TestCompanyPreferenceRepository(preference);
        var unitOfWork = new TestUnitOfWork();
        var handler = new PatchCompanyPreferencesCommandHandler(authorizationService, repository, unitOfWork);

        var result = await handler.Handle(
            new PatchCompanyPreferencesCommand(
                tenantId,
                expectedToken,
                [PatchOp("replace", "/concurrencyToken", Guid.NewGuid().ToString())]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static CompanyPreferencePatchOperation PatchOp(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : System.Text.Json.JsonSerializer.SerializeToElement(value));
}

file sealed class TestCurrentUserService(string? userId) : ICurrentUserService
{
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(userId);

    public string? UserId => userId;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
}

file sealed class TestUserRepository(params User[] users) : IUserRepository
{
    private readonly Dictionary<Guid, User> _users = users.ToDictionary(user => user.PublicId);

    public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.SingleOrDefault(user => user.Id == userId));

    public Task<User?> GetByPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken) =>
        Task.FromResult(_users.GetValueOrDefault(userPublicId));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = User.NormalizeEmail(email);
        return Task.FromResult(_users.Values.SingleOrDefault(user => user.NormalizedEmail == normalizedEmail));
    }

    public Task<User?> GetByExternalProviderAsync(AuthProvider authProvider, string providerUserId, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.SingleOrDefault(user => user.AuthProvider == authProvider && user.ProviderUserId == providerUserId));

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _users[user.PublicId] = user;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

file sealed class TestUserPreferenceRepository(params UserPreference[] seeded) : IUserPreferenceRepository
{
    private readonly Dictionary<long, UserPreference> _items = seeded.ToDictionary(preference => preference.UserId);

    public void Add(UserPreference preference)
    {
        _items[preference.UserId] = preference;
    }

    public Task<UserPreference?> GetByUserIdAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(GetByUserId(userId));

    public Task<string?> ResolveLanguageAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(GetByUserId(userId)?.Language);

    public UserPreference? GetByUserId(long userId) => _items.GetValueOrDefault(userId);
}

file sealed class TestCompanyPreferenceRepository(params CompanyPreference[] seeded) : ICompanyPreferenceRepository
{
    private readonly Dictionary<Guid, CompanyPreference> _items = seeded.ToDictionary(preference => preference.TenantId);

    public void Add(CompanyPreference preference)
    {
        _items[preference.TenantId] = preference;
    }

    public Task<CompanyPreference?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.GetValueOrDefault(tenantId));
}

file sealed class TestCompanyPreferenceAuthorizationService(Result readResult, Result manageResult) : ICompanyPreferenceAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(readResult);

    public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(manageResult);

    public Error TenantMismatch(CLARIHR.Application.Features.IdentityAccess.Common.RbacPermissionAction action) =>
        PreferenceErrors.TenantMismatch(action);
}

file sealed class TestUnitOfWork : IUnitOfWork
{
    public int SaveChangesCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new TestUnitOfWorkTransaction());
}

file sealed class TestUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
