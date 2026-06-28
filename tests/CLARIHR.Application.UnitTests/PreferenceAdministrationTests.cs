using System.Reflection;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
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

        var result = validator.Validate(new UpdateCurrentUserPreferencesCommand("es-sv", Guid.NewGuid()));

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
        var preference = UserPreference.Create(user.Id, "en");
        var originalToken = preference.ConcurrencyToken;
        preferenceRepository.Add(preference);

        var unitOfWork = new TestUnitOfWork();
        var handler = new UpdateCurrentUserPreferencesCommandHandler(
            currentUserService,
            userRepository,
            preferenceRepository,
            unitOfWork);

        var result = await handler.Handle(new UpdateCurrentUserPreferencesCommand("it", originalToken), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("it", result.Value.Language);
        Assert.NotEqual(originalToken, result.Value.ConcurrencyToken);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Equal("it", preferenceRepository.GetByUserId(user.Id)!.Language);
    }

    [Fact]
    public async Task UpdateCurrentUserPreferencesCommandHandler_WhenConcurrencyTokenDiffers_ShouldReturnConflict()
    {
        var user = SeededUser(12, Guid.NewGuid(), "elena@clarihr.test");
        var currentUserService = new TestCurrentUserService(user.PublicId.ToString());
        var userRepository = new TestUserRepository(user);
        var preferenceRepository = new TestUserPreferenceRepository(UserPreference.Create(user.Id, "en"));
        var unitOfWork = new TestUnitOfWork();
        var handler = new UpdateCurrentUserPreferencesCommandHandler(
            currentUserService,
            userRepository,
            preferenceRepository,
            unitOfWork);

        var result = await handler.Handle(new UpdateCurrentUserPreferencesCommand("it", Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PreferenceErrors.ConcurrencyConflict.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateCurrentUserPreferencesCommandHandler_WhenPreferenceDoesNotExist_ShouldAutoProvisionIgnoringToken()
    {
        var user = SeededUser(13, Guid.NewGuid(), "fabio@clarihr.test");
        var currentUserService = new TestCurrentUserService(user.PublicId.ToString());
        var userRepository = new TestUserRepository(user);
        var preferenceRepository = new TestUserPreferenceRepository();
        var unitOfWork = new TestUnitOfWork();
        var handler = new UpdateCurrentUserPreferencesCommandHandler(
            currentUserService,
            userRepository,
            preferenceRepository,
            unitOfWork);

        // No preference yet → first write provisions it; the supplied (necessarily unknown) token is ignored.
        var result = await handler.Handle(new UpdateCurrentUserPreferencesCommand("pt", Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("pt", result.Value.Language);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public void ReplaceCurrentUserSocialLinksCommandValidator_WhenProviderCodesRepeat_ShouldReturnValidationError()
    {
        var validator = new ReplaceCurrentUserSocialLinksCommandValidator();

        var result = validator.Validate(new ReplaceCurrentUserSocialLinksCommand(
        [
            new UpdateCurrentUserSocialLinkItem("linkedin", "https://linkedin.com/in/ana"),
            new UpdateCurrentUserSocialLinkItem("LINKEDIN", "https://linkedin.com/in/ana-2")
        ], Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == "Provider codes must be unique.");
    }

    [Fact]
    public async Task ReplaceCurrentUserSocialLinksCommandHandler_WhenRequestIsValid_ShouldReplaceLinks()
    {
        var user = SeededUser(17, Guid.NewGuid(), "diana@clarihr.test");
        var currentUserService = new TestCurrentUserService(user.PublicId.ToString());
        var userRepository = new TestUserRepository(user);
        var preference = UserPreference.Create(user.Id, "en");
        var originalToken = preference.ConcurrencyToken;
        var preferenceRepository = new TestUserPreferenceRepository(preference);
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
            ], originalToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.SocialLinks.Count);
        Assert.NotEqual(originalToken, result.Value.ConcurrencyToken);
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

    [Fact]
    public async Task ReplaceCurrentUserSocialLinksCommandHandler_WhenConcurrencyTokenDiffers_ShouldReturnConflict()
    {
        var user = SeededUser(18, Guid.NewGuid(), "elsa@clarihr.test");
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
                new UpdateCurrentUserSocialLinkItem("github", "https://github.com/elsa")
            ], Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PreferenceErrors.ConcurrencyConflict.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task PatchCurrentUserPreferencesCommandHandler_WhenConcurrencyTokenDiffers_ShouldReturnConflict()
    {
        var user = SeededUser(21, Guid.NewGuid(), "gael@clarihr.test");
        var currentUserService = new TestCurrentUserService(user.PublicId.ToString());
        var userRepository = new TestUserRepository(user);
        var preferenceRepository = new TestUserPreferenceRepository(UserPreference.Create(user.Id, "en"));
        var unitOfWork = new TestUnitOfWork();
        var handler = new PatchCurrentUserPreferencesCommandHandler(
            currentUserService,
            userRepository,
            preferenceRepository,
            unitOfWork);

        var result = await handler.Handle(
            new PatchCurrentUserPreferencesCommand(Guid.NewGuid(), [PatchOp("replace", "/language", "es")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PreferenceErrors.ConcurrencyConflict.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task PatchCurrentUserPreferencesCommandHandler_WhenRequestIsValid_ShouldApplyPatchAndBumpToken()
    {
        var user = SeededUser(22, Guid.NewGuid(), "hugo@clarihr.test");
        var currentUserService = new TestCurrentUserService(user.PublicId.ToString());
        var userRepository = new TestUserRepository(user);
        var preference = UserPreference.Create(user.Id, "en");
        var originalToken = preference.ConcurrencyToken;
        var preferenceRepository = new TestUserPreferenceRepository(preference);
        var unitOfWork = new TestUnitOfWork();
        var handler = new PatchCurrentUserPreferencesCommandHandler(
            currentUserService,
            userRepository,
            preferenceRepository,
            unitOfWork);

        var result = await handler.Handle(
            new PatchCurrentUserPreferencesCommand(originalToken, [PatchOp("replace", "/language", "fr")]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("fr", result.Value.Language);
        Assert.NotEqual(originalToken, result.Value.ConcurrencyToken);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task PatchCurrentUserPreferencesCommandHandler_WhenPathIsNotPatchable_ShouldReturnValidationFailure()
    {
        var user = SeededUser(23, Guid.NewGuid(), "iris@clarihr.test");
        var currentUserService = new TestCurrentUserService(user.PublicId.ToString());
        var userRepository = new TestUserRepository(user);
        var preference = UserPreference.Create(user.Id, "en");
        var preferenceRepository = new TestUserPreferenceRepository(preference);
        var unitOfWork = new TestUnitOfWork();
        var handler = new PatchCurrentUserPreferencesCommandHandler(
            currentUserService,
            userRepository,
            preferenceRepository,
            unitOfWork);

        var result = await handler.Handle(
            new PatchCurrentUserPreferencesCommand(
                preference.ConcurrencyToken,
                [PatchOp("add", "/socialLinks", "x")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task GetCurrentUserPreferencesQueryHandler_WhenAutoProvisionRacesUniqueConflict_ShouldReReadAndSucceed()
    {
        // UP-A: a concurrent first access provisioned the singleton first; this insert trips the unique
        // (user_id) index. The GET must re-read the winning row and return it (200), never a 500.
        var user = SeededUser(31, Guid.NewGuid(), "noa@clarihr.test");
        var currentUserService = new TestCurrentUserService(user.PublicId.ToString());
        var userRepository = new TestUserRepository(user);
        var winner = UserPreference.Create(user.Id, "fr");
        var preferenceRepository = new RaceUserPreferenceRepository(winner);
        var unitOfWork = new ThrowingUnitOfWork(UserPreferenceConstraintViolations.UserUniqueConstraintName);

        var handler = new GetCurrentUserPreferencesQueryHandler(
            currentUserService,
            userRepository,
            preferenceRepository,
            unitOfWork);

        var result = await handler.Handle(new GetCurrentUserPreferencesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("fr", result.Value.Language); // the winning row, re-read after the conflict
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateCurrentUserPreferencesCommandHandler_WhenAutoProvisionRacesUniqueConflict_ShouldReturnConflict()
    {
        // UP-A: first-write provision lost the race; surface a retryable 409 instead of a 500.
        var user = SeededUser(32, Guid.NewGuid(), "omar@clarihr.test");
        var handler = new UpdateCurrentUserPreferencesCommandHandler(
            new TestCurrentUserService(user.PublicId.ToString()),
            new TestUserRepository(user),
            new TestUserPreferenceRepository(),
            new ThrowingUnitOfWork(UserPreferenceConstraintViolations.UserUniqueConstraintName));

        var result = await handler.Handle(new UpdateCurrentUserPreferencesCommand("pt", Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PreferenceErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task ReplaceCurrentUserSocialLinksCommandHandler_WhenAutoProvisionRacesUniqueConflict_ShouldReturnConflict()
    {
        var user = SeededUser(33, Guid.NewGuid(), "petra@clarihr.test");
        var handler = new ReplaceCurrentUserSocialLinksCommandHandler(
            new TestCurrentUserService(user.PublicId.ToString()),
            new TestUserRepository(user),
            new TestUserPreferenceRepository(),
            new ThrowingUnitOfWork(UserPreferenceConstraintViolations.UserUniqueConstraintName));

        var result = await handler.Handle(
            new ReplaceCurrentUserSocialLinksCommand(
                [new UpdateCurrentUserSocialLinkItem("github", "https://github.com/petra")],
                Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PreferenceErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task PatchCurrentUserPreferencesCommandHandler_WhenAutoProvisionRacesUniqueConflict_ShouldReturnConflict()
    {
        var user = SeededUser(34, Guid.NewGuid(), "quim@clarihr.test");
        var handler = new PatchCurrentUserPreferencesCommandHandler(
            new TestCurrentUserService(user.PublicId.ToString()),
            new TestUserRepository(user),
            new TestUserPreferenceRepository(),
            new ThrowingUnitOfWork(UserPreferenceConstraintViolations.UserUniqueConstraintName));

        var result = await handler.Handle(
            new PatchCurrentUserPreferencesCommand(Guid.NewGuid(), [PatchOp("replace", "/language", "es")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PreferenceErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    private static UserPreferencePatchOperation PatchOp(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : System.Text.Json.JsonSerializer.SerializeToElement(value));

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
        var auditService = new TestAuditService();
        var unitOfWork = new TestUnitOfWork();
        var handler = new UpdateCompanyPreferencesCommandHandler(authorizationService, repository, auditService, unitOfWork);

        var result = await handler.Handle(
            new UpdateCompanyPreferencesCommand(tenantId, "EUR", "Europe/Madrid", null, null, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PreferenceErrors.ConcurrencyConflict.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
        Assert.Empty(auditService.TenantEntries); // a rejected write is never audited
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
        var auditService = new TestAuditService();
        var unitOfWork = new TestUnitOfWork();
        var handler = new UpdateCompanyPreferencesCommandHandler(authorizationService, repository, auditService, unitOfWork);

        var result = await handler.Handle(
            new UpdateCompanyPreferencesCommand(tenantId, "EUR", "Europe/Madrid", null, null, null, expectedToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("EUR", result.Value.CurrencyCode);
        Assert.Equal("Europe/Madrid", result.Value.TimeZone);
        Assert.Equal(2, unitOfWork.SaveChangesCalls); // CP-C: 1 for the mutation + 1 for the audit row

        // CP-C: the change is audited under the company tenant with a before/after snapshot.
        var audited = Assert.Single(auditService.TenantEntries);
        Assert.Equal(tenantId, audited.TenantId);
        Assert.Equal(AuditEventTypes.CompanyPreferencesUpdated, audited.Entry.EventType);
        Assert.Equal(AuditEntityTypes.CompanyPreference, audited.Entry.EntityType);
        Assert.Equal(preference.PublicId, audited.Entry.EntityId);
        Assert.Equal(AuditActions.Update, audited.Entry.Action);
        var auditedBefore = Assert.IsType<CompanyPreferenceResponse>(audited.Entry.Before);
        Assert.Equal("USD", auditedBefore.CurrencyCode);
        var auditedAfter = Assert.IsType<CompanyPreferenceResponse>(audited.Entry.After);
        Assert.Equal("EUR", auditedAfter.CurrencyCode);
    }

    [Fact]
    public async Task PatchCompanyPreferencesCommandHandler_WhenConcurrencyTokenDiffers_ShouldReturnConflict()
    {
        var tenantId = Guid.NewGuid();
        var preference = CompanyPreference.Create("USD", "UTC");
        preference.SetTenantId(tenantId);

        var authorizationService = new TestCompanyPreferenceAuthorizationService(Result.Success(), Result.Success());
        var repository = new TestCompanyPreferenceRepository(preference);
        var auditService = new TestAuditService();
        var unitOfWork = new TestUnitOfWork();
        var handler = new PatchCompanyPreferencesCommandHandler(authorizationService, repository, auditService, unitOfWork);

        var result = await handler.Handle(
            new PatchCompanyPreferencesCommand(
                tenantId,
                Guid.NewGuid(),
                [PatchOp("replace", "/currencyCode", "EUR")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PreferenceErrors.ConcurrencyConflict.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
        Assert.Empty(auditService.TenantEntries); // a rejected patch is never audited
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
        var auditService = new TestAuditService();
        var unitOfWork = new TestUnitOfWork();
        var handler = new PatchCompanyPreferencesCommandHandler(authorizationService, repository, auditService, unitOfWork);

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
        Assert.Equal(2, unitOfWork.SaveChangesCalls); // CP-C: 1 for the mutation + 1 for the audit row

        // CP-C: the patch is audited under the company tenant.
        var audited = Assert.Single(auditService.TenantEntries);
        Assert.Equal(tenantId, audited.TenantId);
        Assert.Equal(AuditEventTypes.CompanyPreferencesUpdated, audited.Entry.EventType);
        Assert.Equal(AuditEntityTypes.CompanyPreference, audited.Entry.EntityType);
        Assert.Equal(preference.PublicId, audited.Entry.EntityId);
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
        var auditService = new TestAuditService();
        var unitOfWork = new TestUnitOfWork();
        var handler = new PatchCompanyPreferencesCommandHandler(authorizationService, repository, auditService, unitOfWork);

        var result = await handler.Handle(
            new PatchCompanyPreferencesCommand(
                tenantId,
                expectedToken,
                [PatchOp("replace", "/concurrencyToken", Guid.NewGuid().ToString())]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
        Assert.Empty(auditService.TenantEntries); // a rejected patch is never audited
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

// UP-A: simulates the auto-provision insert losing the race to a concurrent first access — SaveChanges
// surfaces the (user_id) unique-index violation the way CLARIHR.Infrastructure.Persistence.UnitOfWork does.
file sealed class ThrowingUnitOfWork(string constraintName) : IUnitOfWork
{
    public int SaveChangesCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        throw new UniqueConstraintViolationException(constraintName, new InvalidOperationException("simulated 23505"));
    }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new TestUnitOfWorkTransaction());
}

// UP-A: not provisioned on the first read (→ provision branch), then the winning concurrent request's row
// is visible on the re-read that follows the unique-conflict catch. Add is a no-op (our insert lost).
file sealed class RaceUserPreferenceRepository(UserPreference winner) : IUserPreferenceRepository
{
    private int _getCalls;

    public void Add(UserPreference preference)
    {
    }

    public Task<UserPreference?> GetByUserIdAsync(long userId, CancellationToken cancellationToken)
    {
        _getCalls++;
        return Task.FromResult<UserPreference?>(_getCalls == 1 ? null : winner);
    }

    public Task<string?> ResolveLanguageAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(winner.Language);
}
