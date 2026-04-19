using System.Reflection;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.Locations.Countries;
using CLARIHR.Application.Features.CommercialPlans;
using CLARIHR.Application.Features.PlatformSubscriptions;
using CLARIHR.Application.Features.Provisioning;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.Preferences;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Application.UnitTests;

public sealed class ProvisionCompanyForUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserIsNew_ShouldCreateCompanyPlanRolesPermissionsAndMembership()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository);
        var legalRepresentativeRepository = new TestLegalRepresentativeRepository();
        var planEntitlementService = new TestPlanEntitlementService();
        var unitOfWork = new TestUnitOfWork();
        var user = CreatePersistedUser("ana@company.com");
        userRepository.Seed(user);

        var handler = CreateHandler(
            userRepository,
            companyRepository,
            subscriptionRepository,
            userCompanyRepository,
            iamRepository,
            legalRepresentativeRepository,
            planEntitlementService,
            unitOfWork);

        var result = await handler.Handle(
            new ProvisionCompanyForUserCommand(user.PublicId, "Acme HR", "SV", CreateInitialLegalRepresentative()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.AlreadyProvisioned);
        Assert.Equal(ProvisioningConstants.FreePlanCode, result.Value.PlanCode);
        Assert.Single(companyRepository.Items);
        Assert.Equal("Acme HR", companyRepository.Items[0].Name);
        Assert.Equal("acme-hr", companyRepository.Items[0].Slug);
        Assert.Equal("SV", companyRepository.Items[0].CountryCode);
        Assert.Single(subscriptionRepository.Items);
        Assert.Equal(ProvisioningConstants.FreePlanCode, subscriptionRepository.Items[0].PlanCode);
        Assert.Equal(SubscriptionStatus.Active, subscriptionRepository.Items[0].Status);
        Assert.Single(legalRepresentativeRepository.Items);
        Assert.Equal(companyRepository.Items[0].PublicId, legalRepresentativeRepository.Items[0].TenantId);
        Assert.True(legalRepresentativeRepository.Items[0].IsPrimary == true);
        Assert.Equal(2, iamRepository.Roles.Count);
        var adminRole = Assert.Single(iamRepository.Roles, role => role.Name == ProvisioningConstants.CompanyAdminRoleName && role.IsSystemRole);
        Assert.Contains(iamRepository.Roles, role => role.Name == ProvisioningConstants.StandardUserRoleName && role.IsSystemRole);
        Assert.Equal(
            ProvisioningConstants.CompanyAdminPermissions.Length + PermissionMatrixCatalog.AllMatrixCodes.Count,
            iamRepository.Permissions.Count);
        Assert.All(
            PermissionMatrixCatalog.AllMatrixCodes,
            code => Assert.Contains(
                iamRepository.Permissions,
                permission => permission.TenantId == companyRepository.Items[0].PublicId &&
                              permission.NormalizedCode == code.ToUpperInvariant()));
        Assert.Contains(
            iamRepository.Permissions,
            permission => permission.TenantId == companyRepository.Items[0].PublicId &&
                          permission.NormalizedCode == "COMPANYUSERS.READ");
        Assert.Contains(
            iamRepository.Permissions,
            permission => permission.TenantId == companyRepository.Items[0].PublicId &&
                          permission.NormalizedCode == "COMPANYUSERS.ADMIN");
        Assert.Contains(
            iamRepository.Permissions,
            permission => permission.TenantId == companyRepository.Items[0].PublicId &&
                          permission.NormalizedCode == "WORKCENTERS.READ");
        Assert.Contains(
            iamRepository.Permissions,
            permission => permission.TenantId == companyRepository.Items[0].PublicId &&
                          permission.NormalizedCode == "WORKCENTERS.ADMIN");
        Assert.Equal(
            iamRepository.Permissions.Select(static permission => permission.Id).OrderBy(static id => id),
            adminRole.PermissionAssignments.Select(static assignment => assignment.PermissionId).OrderBy(static id => id));
        Assert.Single(iamRepository.Users);
        Assert.Equal(user.PublicId, iamRepository.Users[0].LinkedUserPublicId);
        Assert.Single(userCompanyRepository.Items);
        Assert.True(userCompanyRepository.Items[0].IsPrimary);
        Assert.True(unitOfWork.Transaction.CommitCalled);
        Assert.Equal(4, unitOfWork.SaveChangesCalls);
        Assert.Equal(1, planEntitlementService.EnsureCalls);
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyProvisioned_ShouldReturnAlreadyProvisionedWithoutDuplicates()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository);
        var legalRepresentativeRepository = new TestLegalRepresentativeRepository();
        var unitOfWork = new TestUnitOfWork();
        var user = CreatePersistedUser("ana@company.com");
        userRepository.Seed(user);

        var company = Company.Create("Acme HR", "acme-hr", user.PublicId, "SV", 1);
        companyRepository.Add(company);
        userCompanyRepository.Add(UserCompanyMembership.Create(user.Id, company.Id, roleId: 10, isPrimary: true));

        var handler = CreateHandler(
            userRepository,
            companyRepository,
            subscriptionRepository,
            userCompanyRepository,
            iamRepository,
            legalRepresentativeRepository,
            new TestPlanEntitlementService(),
            unitOfWork);

        var result = await handler.Handle(
            new ProvisionCompanyForUserCommand(user.PublicId, "Another Name", "SV"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.AlreadyProvisioned);
        Assert.Single(companyRepository.Items);
        Assert.Empty(subscriptionRepository.Items);
        Assert.Empty(iamRepository.Roles);
        Assert.Empty(iamRepository.Permissions);
        Assert.Empty(iamRepository.Users);
        Assert.True(unitOfWork.Transaction.CommitCalled);
    }

    [Fact]
    public async Task Handle_WhenInitialLegalRepresentativeOmitsPrimaryFlag_ShouldPersistNullPrimaryFlag()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository);
        var legalRepresentativeRepository = new TestLegalRepresentativeRepository();
        var user = CreatePersistedUser("ana@company.com");
        userRepository.Seed(user);

        var handler = CreateHandler(
            userRepository,
            companyRepository,
            subscriptionRepository,
            userCompanyRepository,
            iamRepository,
            legalRepresentativeRepository,
            new TestPlanEntitlementService(),
            new TestUnitOfWork());

        var result = await handler.Handle(
            new ProvisionCompanyForUserCommand(user.PublicId, "Acme HR", "SV", CreateInitialLegalRepresentative(isPrimary: null)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(legalRepresentativeRepository.Items);
        Assert.Null(legalRepresentativeRepository.Items[0].IsPrimary);
    }

    [Fact]
    public async Task Handle_WhenInitialLegalRepresentativeMissingForNewProvisioning_ShouldReturnValidationError()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository);
        var legalRepresentativeRepository = new TestLegalRepresentativeRepository();
        var unitOfWork = new TestUnitOfWork();
        var user = CreatePersistedUser("ana@company.com");
        userRepository.Seed(user);

        var handler = CreateHandler(
            userRepository,
            companyRepository,
            subscriptionRepository,
            userCompanyRepository,
            iamRepository,
            legalRepresentativeRepository,
            new TestPlanEntitlementService(),
            unitOfWork);

        var result = await handler.Handle(
            new ProvisionCompanyForUserCommand(user.PublicId, "Acme HR", "SV"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ProvisioningErrors.InitialLegalRepresentativeRequired.Code, result.Error.Code);
        Assert.True(unitOfWork.Transaction.RollbackCalled);
    }

    [Fact]
    public async Task Handle_WhenCountryExistsInCatalog_ShouldProvisionUsingGenericLocationSeed()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository);
        var legalRepresentativeRepository = new TestLegalRepresentativeRepository();
        var unitOfWork = new TestUnitOfWork();
        var user = CreatePersistedUser("ana@company.com");
        userRepository.Seed(user);

        var handler = CreateHandler(
            userRepository,
            companyRepository,
            subscriptionRepository,
            userCompanyRepository,
            iamRepository,
            legalRepresentativeRepository,
            new TestPlanEntitlementService(),
            unitOfWork);

        var result = await handler.Handle(
            new ProvisionCompanyForUserCommand(user.PublicId, "Acme HR", "GT", CreateInitialLegalRepresentative()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(companyRepository.Items);
        Assert.Equal("GT", companyRepository.Items[0].CountryCode);
        Assert.True(unitOfWork.Transaction.CommitCalled);
    }

    [Fact]
    public async Task Handle_WhenCountryDoesNotExistInCatalog_ShouldReturnValidationError()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository);
        var legalRepresentativeRepository = new TestLegalRepresentativeRepository();
        var unitOfWork = new TestUnitOfWork();
        var user = CreatePersistedUser("ana@company.com");
        userRepository.Seed(user);

        var handler = CreateHandler(
            userRepository,
            companyRepository,
            subscriptionRepository,
            userCompanyRepository,
            iamRepository,
            legalRepresentativeRepository,
            new TestPlanEntitlementService(),
            unitOfWork);

        var result = await handler.Handle(
            new ProvisionCompanyForUserCommand(user.PublicId, "Acme HR", "ZZ", CreateInitialLegalRepresentative()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("provisioning.country_not_found", result.Error.Code);
        Assert.Empty(companyRepository.Items);
        Assert.True(unitOfWork.Transaction.RollbackCalled);
    }

    [Fact]
    public async Task Handle_WhenProvisioningStepThrows_ShouldRollbackAndReturnFailure()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository);
        var legalRepresentativeRepository = new TestLegalRepresentativeRepository();
        var unitOfWork = new TestUnitOfWork();
        var user = CreatePersistedUser("ana@company.com");
        userRepository.Seed(user);

        var handler = CreateHandler(
            userRepository,
            companyRepository,
            subscriptionRepository,
            userCompanyRepository,
            iamRepository,
            legalRepresentativeRepository,
            new ThrowingPlanEntitlementService(),
            unitOfWork);

        var result = await handler.Handle(
            new ProvisionCompanyForUserCommand(user.PublicId, "Acme HR", "SV", CreateInitialLegalRepresentative()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ProvisioningErrors.ProvisioningFailed.Code, result.Error.Code);
        Assert.True(unitOfWork.Transaction.RollbackCalled);
    }

    [Fact]
    public async Task Handle_WhenSlugAlreadyExists_ShouldCreateIncrementedSlug()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = new TestCompanyRepository();
        var subscriptionRepository = new TestCompanySubscriptionRepository(companyRepository);
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository);
        var legalRepresentativeRepository = new TestLegalRepresentativeRepository();
        var user = CreatePersistedUser("ana@company.com");
        userRepository.Seed(user);
        companyRepository.Add(Company.Create("Acme HR", "acme-hr", user.PublicId, "SV", 1));

        var handler = CreateHandler(
            userRepository,
            companyRepository,
            subscriptionRepository,
            userCompanyRepository,
            iamRepository,
            legalRepresentativeRepository,
            new TestPlanEntitlementService(),
            new TestUnitOfWork());

        var result = await handler.Handle(
            new ProvisionCompanyForUserCommand(user.PublicId, "Acme HR", "SV", CreateInitialLegalRepresentative()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("acme-hr-2", companyRepository.Items.Last().Slug);
    }

    private static ProvisionCompanyForUserCommandHandler CreateHandler(
        TestUserRepository userRepository,
        TestCompanyRepository companyRepository,
        TestCompanySubscriptionRepository subscriptionRepository,
        TestUserCompanyRepository userCompanyRepository,
        TestIamAdministrationRepository iamRepository,
        TestLegalRepresentativeRepository legalRepresentativeRepository,
        IPlanEntitlementService planEntitlementService,
        TestUnitOfWork unitOfWork)
    {
        var commercialPlanRepository = new TestCommercialPlanRepository();

        var provisioningService = new CompanyProvisioningService(
            userRepository,
            companyRepository,
            commercialPlanRepository,
            subscriptionRepository,
            userCompanyRepository,
            iamRepository,
            legalRepresentativeRepository,
            new TestCountryCatalogRepository(),
            new TestCompanyPreferenceRepository(),
            new TestLocationSeedService(),
            planEntitlementService,
            unitOfWork,
            new FixedDateTimeProvider(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)));

        return new ProvisionCompanyForUserCommandHandler(
            userRepository,
            userCompanyRepository,
            provisioningService,
            unitOfWork,
            NullLogger<ProvisionCompanyForUserCommandHandler>.Instance);
    }

    private static InitialLegalRepresentativeInput CreateInitialLegalRepresentative(bool? isPrimary = true) =>
        new(
            "Ana",
            "Mendoza",
            "TAX_ID",
            "0614-290190-102-3",
            "Representante Legal",
            LegalRepresentativeRepresentationType.PrimaryLegalRepresentative,
            "Representación general",
            "Acta notarial",
            new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            null,
            "ana@company.com",
            "+50370000000",
            IsPrimary: isPrimary);

    private static User CreatePersistedUser(string email)
    {
        var user = User.RegisterLocal(
            "Ana",
            "Mendoza",
            email,
            "hashed-password",
            "SV",
            "landing");

        SetEntityId(user, 100);
        return user;
    }

    private static void SetEntityId(Entity entity, long id)
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(entity, [id]);
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class TestLocationSeedService : ILocationSeedService
    {
        public Task InitializeDefaultsAsync(
            Guid tenantId,
            string countryCode,
            string countryName,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestCountryCatalogRepository : ICountryCatalogRepository
    {
        public Task<IReadOnlyCollection<CountryCatalogItemResponse>> GetActiveItemsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<CountryCatalogItemResponse>>(
            [
                new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), "SV", "El Salvador", 1, "es"),
                new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"), "GT", "Guatemala", 2, "es")
            ]);

        public Task<CountryCatalogLookup?> GetActiveByCodeAsync(string countryCode, CancellationToken cancellationToken)
        {
            var normalizedCode = countryCode.Trim().ToUpperInvariant();

            return Task.FromResult<CountryCatalogLookup?>(
                normalizedCode switch
                {
                    "SV" => new CountryCatalogLookup(1, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), "SV", "El Salvador", true, "es"),
                    "GT" => new CountryCatalogLookup(2, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"), "GT", "Guatemala", true, "es"),
                    _ => null
                });
        }
    }

    private sealed class TestCompanyPreferenceRepository : ICompanyPreferenceRepository
    {
        private readonly List<CompanyPreference> _items = [];

        public void Add(CompanyPreference preference) => _items.Add(preference);

        public Task<CompanyPreference?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.SingleOrDefault(preference => preference.TenantId == tenantId));
    }

    private sealed class TestUserRepository : IUserRepository
    {
        private readonly List<User> _items = [];

        public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.SingleOrDefault(user => user.Id == userId));

        public Task<User?> GetByPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.SingleOrDefault(user => user.PublicId == userPublicId));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            var normalizedEmail = User.NormalizeEmail(email);
            return Task.FromResult(_items.SingleOrDefault(user => user.NormalizedEmail == normalizedEmail));
        }

        public Task<User?> GetByExternalProviderAsync(AuthProvider authProvider, string providerUserId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.SingleOrDefault(user =>
                user.AuthProvider == authProvider &&
                user.ProviderUserId == providerUserId));

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            if (user.Id == 0)
            {
                SetEntityId(user, _items.Count + 1);
            }

            _items.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Seed(User user) => _items.Add(user);
    }

    private sealed class TestCompanyRepository : ICompanyRepository
    {
        private long _nextId = 1;

        public List<Company> Items { get; } = [];

        public void Add(Company company)
        {
            if (company.Id == 0)
            {
                SetEntityId(company, _nextId++);
            }

            Items.Add(company);
        }

        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(company => company.Slug == slug));

        public Task<Company?> FindByPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(company => company.PublicId == companyPublicId));

        public Task<AccountCompanyDetailResponse?> FindOwnedByUserAsync(
            Guid companyPublicId,
            Guid ownerUserPublicId,
            Guid? activeTenantId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResponse<AccountCompanySummaryResponse>> GetOwnedByUserAsync(
            Guid ownerUserPublicId,
            CompanyListFilter filter,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountOwnedByUserAsync(
            Guid ownerUserPublicId,
            CompanyOwnershipCountFilter filter,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Company? FindById(long companyId) => Items.SingleOrDefault(company => company.Id == companyId);
    }

    private sealed class TestCompanySubscriptionRepository(TestCompanyRepository companyRepository) : ICompanySubscriptionRepository
    {
        public List<CompanySubscription> Items { get; } = [];
        public List<CompanySubscriptionStatusChangeRequest> StatusChangeRequests { get; } = [];
        public List<CompanySubscriptionPlanChange> PlanChanges { get; } = [];
        public List<CompanyCommercialAddon> CompanyAddons { get; } = [];
        public List<CompanyCommercialAddonChange> CompanyAddonChanges { get; } = [];

        public void Add(CompanySubscription subscription)
        {
            if (subscription.Id == 0)
            {
                SetEntityId(subscription, Items.Count + 1);
            }

            Items.Add(subscription);
        }

        public void AddStatusChangeRequest(CompanySubscriptionStatusChangeRequest statusChangeRequest)
        {
            if (statusChangeRequest.Id == 0)
            {
                SetEntityId(statusChangeRequest, StatusChangeRequests.Count + 1);
            }

            StatusChangeRequests.Add(statusChangeRequest);
        }

        public void AddPlanChange(CompanySubscriptionPlanChange planChange)
        {
            if (planChange.Id == 0)
            {
                SetEntityId(planChange, PlanChanges.Count + 1);
            }

            PlanChanges.Add(planChange);
        }

        public void AddCompanyAddon(CompanyCommercialAddon companyAddon)
        {
            if (companyAddon.Id == 0)
            {
                SetEntityId(companyAddon, CompanyAddons.Count + 1);
            }

            CompanyAddons.Add(companyAddon);
        }

        public void AddCompanyAddonChange(CompanyCommercialAddonChange companyAddonChange)
        {
            if (companyAddonChange.Id == 0)
            {
                SetEntityId(companyAddonChange, CompanyAddonChanges.Count + 1);
            }

            CompanyAddonChanges.Add(companyAddonChange);
        }

        public Task<string?> GetActivePlanCodeAsync(Guid companyPublicId, CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            var activePlan = Items
                .SingleOrDefault(subscription => subscription.CompanyId == company?.Id && subscription.Status == SubscriptionStatus.Active)?
                .PlanCode;

            return Task.FromResult(activePlan);
        }

        public Task<CompanySubscription?> GetActiveByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(subscription =>
                subscription.CompanyId == companyId &&
                subscription.Status == SubscriptionStatus.Active));

        public Task<CompanySubscription?> GetCurrentByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(subscription =>
                subscription.CompanyId == companyId &&
                (subscription.Status == SubscriptionStatus.Active ||
                 subscription.Status == SubscriptionStatus.Trial ||
                 subscription.Status == SubscriptionStatus.Suspended ||
                 subscription.Status == SubscriptionStatus.Draft)));

        public Task<CompanySubscription?> GetActiveByCompanyPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            return Task.FromResult(Items.SingleOrDefault(subscription =>
                subscription.CompanyId == company?.Id &&
                subscription.Status == SubscriptionStatus.Active));
        }

        public Task<CompanySubscription?> GetScheduledByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(subscription =>
                subscription.CompanyId == companyId &&
                subscription.Status == SubscriptionStatus.Scheduled));

        public Task<CompanySubscription?> GetByCompanyAndSubscriptionPublicIdAsync(
            Guid companyPublicId,
            Guid subscriptionPublicId,
            CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            return Task.FromResult(Items.SingleOrDefault(subscription =>
                subscription.CompanyId == company?.Id &&
                subscription.PublicId == subscriptionPublicId));
        }

        public Task<PlatformCompanySubscriptionOverviewResponse?> GetOverviewByCompanyPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
            Task.FromResult<PlatformCompanySubscriptionOverviewResponse?>(null);

        public Task<PagedResponse<PlatformCompanySubscriptionResponse>> SearchByCompanyPublicIdAsync(
            Guid companyPublicId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanySubscriptionResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanySubscriptionListItemResponse>> SearchAsync(
            SubscriptionStatus? status,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanySubscriptionListItemResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>> SearchStatusHistoryAsync(
            Guid companyPublicId,
            Guid subscriptionPublicId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>> SearchPlanChangesByCompanyPublicIdAsync(
            Guid companyPublicId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanyAddonResponse>> SearchCompanyAddonsByCompanyPublicIdAsync(
            Guid companyPublicId,
            CompanyAddonStatus? status,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanyAddonResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanyEligibleAddonResponse>> SearchEligibleAddonsByCompanyPublicIdAsync(
            Guid companyPublicId,
            CommercialAddonType? type,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanyEligibleAddonResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<PlatformCompanyAddonChangeResponse>> SearchAddonChangesByCompanyPublicIdAsync(
            Guid companyPublicId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<PlatformCompanyAddonChangeResponse>([], pageNumber, pageSize, 0));

        public Task<IReadOnlyCollection<Guid>> GetDueScheduledSubscriptionIdsAsync(
            DateTime utcNow,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<IReadOnlyCollection<Guid>> GetDueScheduledStatusChangeRequestIdsAsync(
            DateTime utcNow,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<IReadOnlyCollection<Guid>> GetDueExpiringSubscriptionIdsAsync(
            DateTime utcNow,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<IReadOnlyCollection<Guid>> GetDueScheduledPlanChangeIdsAsync(
            DateTime utcNow,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<IReadOnlyCollection<Guid>> GetDueScheduledAddonChangeIdsAsync(
            DateTime utcNow,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<CompanySubscription?> GetByPublicIdAsync(Guid subscriptionPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(subscription => subscription.PublicId == subscriptionPublicId));

        public Task<CompanySubscriptionStatusChangeRequest?> GetStatusChangeRequestByPublicIdAsync(
            Guid statusChangeRequestPublicId,
            CancellationToken cancellationToken) =>
            Task.FromResult(StatusChangeRequests.SingleOrDefault(request => request.PublicId == statusChangeRequestPublicId));

        public Task<CompanySubscriptionStatusChangeRequest?> GetScheduledStatusChangeRequestBySubscriptionIdAsync(
            long companySubscriptionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(StatusChangeRequests.SingleOrDefault(request =>
                request.CompanySubscriptionId == companySubscriptionId &&
                request.Status == SubscriptionStatusChangeRequestStatus.Scheduled));

        public Task<CompanySubscriptionPlanChange?> GetPlanChangeByPublicIdAsync(Guid planChangePublicId, CancellationToken cancellationToken) =>
            Task.FromResult(PlanChanges.SingleOrDefault(planChange => planChange.PublicId == planChangePublicId));

        public Task<CompanySubscriptionPlanChange?> GetPlanChangeByCompanyAndPublicIdAsync(
            Guid companyPublicId,
            Guid planChangePublicId,
            CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            return Task.FromResult(PlanChanges.SingleOrDefault(planChange =>
                planChange.CompanyId == company?.Id &&
                planChange.PublicId == planChangePublicId));
        }

        public Task<CompanySubscriptionPlanChange?> GetScheduledPlanChangeByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
            Task.FromResult(PlanChanges.SingleOrDefault(planChange =>
                planChange.CompanyId == companyId &&
                planChange.Status == SubscriptionPlanChangeStatus.Scheduled));

        public Task<CompanyCommercialAddon?> GetCompanyAddonByCompanyAndAddonPublicIdAsync(
            Guid companyPublicId,
            Guid commercialAddonPublicId,
            CancellationToken cancellationToken) =>
            Task.FromResult<CompanyCommercialAddon?>(null);

        public Task<CompanyCommercialAddon?> GetCompanyAddonByCompanyIdAndAddonIdAsync(
            long companyId,
            long commercialAddonId,
            CancellationToken cancellationToken) =>
            Task.FromResult(CompanyAddons.SingleOrDefault(addon =>
                addon.CompanyId == companyId &&
                addon.CommercialAddonId == commercialAddonId));

        public Task<CompanyCommercialAddonChange?> GetAddonChangeByPublicIdAsync(Guid addonChangePublicId, CancellationToken cancellationToken) =>
            Task.FromResult(CompanyAddonChanges.SingleOrDefault(change => change.PublicId == addonChangePublicId));

        public Task<CompanyCommercialAddonChange?> GetAddonChangeByCompanyAndPublicIdAsync(
            Guid companyPublicId,
            Guid addonChangePublicId,
            CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            return Task.FromResult(CompanyAddonChanges.SingleOrDefault(change =>
                change.CompanyId == company?.Id &&
                change.PublicId == addonChangePublicId));
        }

        public Task<CompanyCommercialAddonChange?> GetScheduledAddonChangeByCompanyAndAddonIdAsync(
            long companyId,
            long commercialAddonId,
            CancellationToken cancellationToken) =>
            Task.FromResult(CompanyAddonChanges.SingleOrDefault(change =>
                change.CompanyId == companyId &&
                change.CommercialAddonId == commercialAddonId &&
                change.Status == SubscriptionAddonChangeStatus.Scheduled));

        public Task<PlatformCompanySubscriptionResponse?> GetResponseByPublicIdAsync(
            Guid companyPublicId,
            Guid subscriptionPublicId,
            CancellationToken cancellationToken) =>
            Task.FromResult<PlatformCompanySubscriptionResponse?>(null);

        public Task<PlatformCompanySubscriptionPlanChangeResponse?> GetPlanChangeResponseByPublicIdAsync(
            Guid companyPublicId,
            Guid planChangePublicId,
            CancellationToken cancellationToken) =>
            Task.FromResult<PlatformCompanySubscriptionPlanChangeResponse?>(null);

        public Task<PlatformCompanyAddonChangeResponse?> GetAddonChangeResponseByPublicIdAsync(
            Guid companyPublicId,
            Guid addonChangePublicId,
            CancellationToken cancellationToken) =>
            Task.FromResult<PlatformCompanyAddonChangeResponse?>(null);
    }

    private sealed class TestCommercialPlanRepository : ICommercialPlanRepository
    {
        private readonly CommercialPlan _freePlan;

        public TestCommercialPlanRepository()
        {
            _freePlan = CommercialPlan.Create(
                ProvisioningConstants.FreePlanCode,
                "Free",
                "Free plan",
                0m,
                0m,
                CommercialPlanStatus.Active,
                isSystemPlan: true,
                [],
                initialVersionEffectiveFromUtc: DateTime.Parse("2026-01-01T00:00:00Z").ToUniversalTime());
            SetEntityId(_freePlan, 900);
            foreach (var version in _freePlan.Versions.Where(version => version.Id == 0))
            {
                SetEntityId(version, 9000 + version.VersionNumber);
            }
        }

        public void Add(CommercialPlan plan) => throw new NotSupportedException();

        public Task<CommercialPlan?> GetByInternalIdAsync(long commercialPlanId, CancellationToken cancellationToken) =>
            Task.FromResult(_freePlan.Id == commercialPlanId ? _freePlan : null);

        public Task<CommercialPlan?> GetByIdAsync(Guid commercialPlanId, CancellationToken cancellationToken) =>
            Task.FromResult(_freePlan.PublicId == commercialPlanId ? _freePlan : null);

        public Task<CommercialPlan?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
            Task.FromResult(_freePlan.NormalizedCode == normalizedCode ? _freePlan : null);

        public Task<bool> IsSystemPlanAsync(long commercialPlanId, CancellationToken cancellationToken) =>
            Task.FromResult(_freePlan.Id == commercialPlanId && _freePlan.IsSystemPlan);

        public Task<CommercialPlanVersion?> GetEffectiveVersionAsync(
            Guid commercialPlanId,
            DateTime effectiveAtUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                _freePlan.PublicId == commercialPlanId
                    ? _freePlan.Versions.SingleOrDefault(version => version.IsEffectiveOn(effectiveAtUtc))
                    : null);

        public Task<bool> CodeExistsAsync(string normalizedCode, long? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(_freePlan.NormalizedCode == normalizedCode &&
                            (!excludingId.HasValue || _freePlan.Id != excludingId.Value));

        public Task<PagedResponse<CommercialPlanSummaryResponse>> SearchAsync(
            CommercialPlanStatus? status,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestUserCompanyRepository(TestCompanyRepository companyRepository) : IUserCompanyRepository
    {
        public List<UserCompanyMembership> Items { get; } = [];

        public void Add(UserCompanyMembership membership)
        {
            if (membership.Id == 0)
            {
                SetEntityId(membership, Items.Count + 1);
            }

            Items.Add(membership);
        }

        public Task<bool> ExistsInCompanyAsync(Guid companyPublicId, string normalizedEmail, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasAnyMembershipAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(membership => membership.UserId == userId));

        public Task<bool> HasPrimaryCompanyAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(membership => membership.UserId == userId && membership.IsPrimary));

        public Task<Guid?> GetPrimaryCompanyPublicIdAsync(long userId, CancellationToken cancellationToken)
        {
            var membership = Items.SingleOrDefault(item => item.UserId == userId && item.IsPrimary);
            var companyPublicId = membership is null
                ? (Guid?)null
                : companyRepository.FindById(membership.CompanyId)?.PublicId;

            return Task.FromResult(companyPublicId);
        }

        public Task<UserCompanyMembership?> GetPrimaryMembershipAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.UserId == userId && item.IsPrimary));

        public Task<UserCompanyMembership?> GetMembershipAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            var membership = company is null
                ? null
                : Items.SingleOrDefault(item => item.UserId == userId && item.CompanyId == company.Id);

            return Task.FromResult(membership);
        }

        public Task<string?> GetRoleNormalizedNameAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public Task<UserCompanyMembership?> FindByUserPublicIdAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> UserExistsOutsideCompanyAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> HasActiveMembershipAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            var active = company is not null &&
                Items.Any(item => item.UserId == userId && item.CompanyId == company.Id && item.Status == UserCompanyStatus.Active);

            return Task.FromResult(active);
        }

        public Task<bool> HasAnyActiveAdministratorAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task SetPrimaryCompanyAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.Single(item => item.PublicId == companyPublicId);
            foreach (var membership in Items.Where(item => item.UserId == userId))
            {
                if (membership.CompanyId == company.Id)
                {
                    membership.MarkPrimary();
                    continue;
                }

                membership.ClearPrimary();
            }

            return Task.CompletedTask;
        }

        public Task<bool> IsLastActiveAdministratorAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResponse<CompanyUserSummaryResponse>> GetUsersAsync(
            Guid companyPublicId,
            int pageNumber,
            int pageSize,
            UserStatus? status,
            Guid? roleId,
            string? search,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CompanyUserResponse?> GetUserAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestIamAdministrationRepository : IIamAdministrationRepository
    {
        public List<IamUser> Users { get; } = [];
        public List<IamRole> Roles { get; } = [];
        public List<IamPermission> Permissions { get; } = [];

        public void AddUser(IamUser user)
        {
            if (user.Id == 0)
            {
                SetEntityId(user, Users.Count + 1);
            }

            Users.Add(user);
        }

        public void AddRole(IamRole role)
        {
            if (role.Id == 0)
            {
                SetEntityId(role, Roles.Count + 1);
            }

            Roles.Add(role);
        }

        public void RemoveRole(IamRole role) => Roles.Remove(role);

        public void AddPermission(IamPermission permission)
        {
            if (permission.Id == 0)
            {
                SetEntityId(permission, Permissions.Count + 1);
            }

            Permissions.Add(permission);
        }

        public Task<bool> UserEmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> RoleNameExistsAsync(string normalizedRoleName, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> UserPublicIdExistsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> RolePublicIdExistsAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IamUser?> FindUserByPublicIdAsync(Guid userId, bool includeRoles, CancellationToken cancellationToken) => Task.FromResult<IamUser?>(null);
        public Task<IamUser?> FindUserByTenantAndLinkedUserPublicIdAsync(Guid tenantId, Guid linkedUserPublicId, bool includeRoles, CancellationToken cancellationToken) =>
            Task.FromResult<IamUser?>(Users.SingleOrDefault(user => user.TenantId == tenantId && user.LinkedUserPublicId == linkedUserPublicId));
        public Task<IamRole?> FindRoleByPublicIdAsync(Guid roleId, bool includePermissions, CancellationToken cancellationToken) => Task.FromResult<IamRole?>(null);
        public Task<IReadOnlyList<IamRole>> GetRolesByPublicIdsAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<IamRole>>([]);
        public Task<IReadOnlyList<IamUser>> GetUsersByPublicIdsAsync(IReadOnlyCollection<Guid> userIds, bool includeRoles, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<IamUser>>([]);
        public Task<IReadOnlyList<IamUser>> GetUsersAssignedToRoleAsync(Guid roleId, bool includeRoles, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<IamUser>>([]);
        public Task<IReadOnlyList<IamUser>> GetActiveUsersAsync(bool includeRoles, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<IamUser>>([]);
        public Task<IReadOnlyCollection<Guid>> GetActiveAdministratorUserIdsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
        public Task<IReadOnlyList<IamPermission>> GetPermissionsByNormalizedCodesAsync(IReadOnlyCollection<string> normalizedPermissionCodes, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<IamPermission>>([]);
        public Task<IReadOnlyList<IamPermission>> GetPermissionsByPublicIdsAsync(IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<IamPermission>>([]);
        public Task<PagedResponse<IamUserSummaryResponse>> GetUsersAsync(int pageNumber, int pageSize, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IamUserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedResponse<IamRoleSummaryResponse>> GetRolesAsync(int pageNumber, int pageSize, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IamRoleResponse?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class TestLegalRepresentativeRepository : ILegalRepresentativeRepository
    {
        public List<LegalRepresentative> Items { get; } = [];

        public void Add(LegalRepresentative legalRepresentative)
        {
            if (legalRepresentative.Id == 0)
            {
                SetEntityId(legalRepresentative, Items.Count + 1);
            }

            Items.Add(legalRepresentative);
        }

        public Task<LegalRepresentative?> GetByIdAsync(Guid legalRepresentativeId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.PublicId == legalRepresentativeId));

        public Task<bool> ExistsOutsideTenantAsync(Guid legalRepresentativeId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> DocumentExistsAsync(
            Guid tenantId,
            string documentType,
            string normalizedDocumentNumber,
            long? excludingLegalRepresentativeId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId &&
                string.Equals(item.DocumentType, documentType, StringComparison.OrdinalIgnoreCase) &&
                item.NormalizedDocumentNumber == normalizedDocumentNumber &&
                (!excludingLegalRepresentativeId.HasValue || item.Id != excludingLegalRepresentativeId.Value)));

        public Task<PagedResponse<LegalRepresentativeListItemResponse>> SearchAsync(
            Guid tenantId,
            bool? isActive,
            bool? isPrimary,
            LegalRepresentativeRepresentationType? representationType,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LegalRepresentativeResponse?> GetResponseByIdAsync(Guid legalRepresentativeId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LegalRepresentativeUsageResponse?> GetUsageByIdAsync(Guid legalRepresentativeId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>> GetPositionTitleCatalogItemsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>>([]);

        public Task<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>> GetRepresentationTypeCatalogItemsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>>([]);

        public Task<int> GetActiveCountAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Count(item => item.TenantId == tenantId && item.IsActive));

        public Task<LegalRepresentative?> GetActivePrimaryAsync(
            Guid tenantId,
            Guid? excludingLegalRepresentativePublicId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item =>
                item.TenantId == tenantId &&
                item.IsActive &&
                item.IsPrimary == true &&
                (!excludingLegalRepresentativePublicId.HasValue || item.PublicId != excludingLegalRepresentativePublicId.Value)));

        public Task<IReadOnlyCollection<LegalRepresentativeExportRow>> GetExportRowsAsync(
            Guid tenantId,
            bool? isActive,
            bool? isPrimary,
            LegalRepresentativeRepresentationType? representationType,
            string? search,
            int? maxRows,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<ActiveLegalRepresentativeSummary>> GetActiveSummariesByCompanyAsync(
            Guid companyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<ActiveLegalRepresentativeSummary>>([]);
    }

    private sealed class TestPlanEntitlementService : IPlanEntitlementService
    {
        public int EnsureCalls { get; private set; }

        public Task EnsureSystemPlanDefaultsAsync(CancellationToken cancellationToken)
        {
            EnsureCalls++;
            return Task.CompletedTask;
        }

        public Task<bool> IsModuleEnabledAsync(Guid companyPublicId, string moduleKey, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<IReadOnlyCollection<EffectiveCommercialCapabilityGrant>> GetEffectiveCapabilitiesAsync(
            Guid companyPublicId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<EffectiveCommercialCapabilityGrant>>([]);
    }

    private sealed class ThrowingPlanEntitlementService : IPlanEntitlementService
    {
        public Task EnsureSystemPlanDefaultsAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");

        public Task<bool> IsModuleEnabledAsync(Guid companyPublicId, string moduleKey, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<IReadOnlyCollection<EffectiveCommercialCapabilityGrant>> GetEffectiveCapabilitiesAsync(
            Guid companyPublicId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<EffectiveCommercialCapabilityGrant>>([]);
    }
}
