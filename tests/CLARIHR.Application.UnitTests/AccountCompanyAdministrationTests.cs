using System.Reflection;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Application.Features.AccountCompanies.Common;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.Locations.Countries;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.LegalRepresentatives;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Application.UnitTests;

public sealed class AccountCompanyAdministrationTests
{
    private static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CurrentTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTime CreatedAtUtc = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_WhenOwnerHasCapacity_ShouldProvisionCompanyWithoutSwitchingContext()
    {
        var userRepository = new TestUserRepository();
        var currentUser = CreatePersistedUser(CurrentUserId, "owner@test.com");
        userRepository.Seed(currentUser);

        var auditService = new TestAuditService();
        var companyRepository = new TestCompanyRepository();
        var provisioningService = new TestCompanyProvisioningService(companyRepository, new ProvisionedCompanyResult(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Acme Services",
            "acme-services",
            ProvisioningConstants.FreePlanCode,
            CreatedAtUtc));

        var handler = new CreateAccountCompanyCommandHandler(
            new TestCurrentUserService(CurrentUserId),
            userRepository,
            new TestCompanyOwnershipPolicy(hasCapacity: true),
            provisioningService,
            new TestCountryCatalogRepository(),
            new TestOrgStructureCatalogRepository(),
            companyRepository,
            auditService,
            new TestTenantContext(CurrentTenantId),
            new TestUnitOfWork(),
            NullLogger<CreateAccountCompanyCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateAccountCompanyCommand("Acme Services", "SV", null, CreateInitialLegalRepresentative()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Acme Services", result.Value.Name);
        Assert.False(result.Value.IsActiveContext);
        Assert.Equal(CurrentUserId, provisioningService.LastRequest!.OwnerUserPublicId);
        Assert.Equal("SV", provisioningService.LastRequest.CountryCode);
        Assert.False(provisioningService.LastRequest.MakePrimary);
        Assert.Single(auditService.Entries);
        Assert.Equal(AuditEventTypes.CompanyCreated, auditService.Entries[0].Entry.EventType);
        Assert.Equal(result.Value.PublicId, auditService.Entries[0].TenantId);
    }

    [Fact]
    public async Task Create_WhenOwnerReachedLimit_ShouldReturnConflict()
    {
        var userRepository = new TestUserRepository();
        userRepository.Seed(CreatePersistedUser(CurrentUserId, "owner@test.com"));
        var companyRepository = new TestCompanyRepository();

        var handler = new CreateAccountCompanyCommandHandler(
            new TestCurrentUserService(CurrentUserId),
            userRepository,
            new TestCompanyOwnershipPolicy(hasCapacity: false),
            new TestCompanyProvisioningService(companyRepository, null),
            new TestCountryCatalogRepository(),
            new TestOrgStructureCatalogRepository(),
            companyRepository,
            new TestAuditService(),
            new TestTenantContext(CurrentTenantId),
            new TestUnitOfWork(),
            NullLogger<CreateAccountCompanyCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateAccountCompanyCommand("Acme Services", "SV", null, CreateInitialLegalRepresentative()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AccountCompanyErrors.CompanyLimitReached.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_ShouldRenameCompanyAndKeepSlug()
    {
        var userRepository = new TestUserRepository();
        userRepository.Seed(CreatePersistedUser(CurrentUserId, "owner@test.com"));
        var companyRepository = new TestCompanyRepository();
        var company = CreateCompany(CurrentUserId, "Acme Services", "acme-services");
        companyRepository.Add(company);
        var auditService = new TestAuditService();

        var handler = new UpdateAccountCompanyCommandHandler(
            new TestCurrentUserService(CurrentUserId),
            userRepository,
            companyRepository,
            new TestOrgStructureCatalogRepository(),
            auditService,
            new TestTenantContext(CurrentTenantId),
            new TestUnitOfWork());

        var result = await handler.Handle(
            new UpdateAccountCompanyCommand(company.PublicId, "Acme Services Group", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Acme Services Group", company.Name);
        Assert.Equal("acme-services", company.Slug);
        Assert.Single(auditService.Entries);
        Assert.Equal(AuditEventTypes.CompanyUpdated, auditService.Entries[0].Entry.EventType);
    }

    [Fact]
    public async Task Archive_WhenCompanyIsActiveContext_ShouldReturnConflict()
    {
        var userRepository = new TestUserRepository();
        var user = CreatePersistedUser(CurrentUserId, "owner@test.com");
        userRepository.Seed(user);
        var companyRepository = new TestCompanyRepository();
        var company = CreateCompany(CurrentUserId, "Acme Services", "acme-services", publicId: CurrentTenantId);
        companyRepository.Add(company);
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository);
        userCompanyRepository.Add(UserCompanyMembership.Create(user.Id, company.Id, roleId: 10, isPrimary: true));

        var handler = new ArchiveAccountCompanyCommandHandler(
            new TestCurrentUserService(CurrentUserId),
            userRepository,
            companyRepository,
            userCompanyRepository,
            new TestAuditService(),
            new TestTenantContext(CurrentTenantId),
            new TestUnitOfWork());

        var result = await handler.Handle(new ArchiveAccountCompanyCommand(company.PublicId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AccountCompanyErrors.ActiveCompanyArchiveForbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task Archive_WhenCompanyIsNotOwned_ShouldReturnForbidden()
    {
        var userRepository = new TestUserRepository();
        userRepository.Seed(CreatePersistedUser(CurrentUserId, "owner@test.com"));
        var companyRepository = new TestCompanyRepository();
        companyRepository.Add(CreateCompany(Guid.NewGuid(), "Other", "other"));

        var handler = new ArchiveAccountCompanyCommandHandler(
            new TestCurrentUserService(CurrentUserId),
            userRepository,
            companyRepository,
            new TestUserCompanyRepository(companyRepository),
            new TestAuditService(),
            new TestTenantContext(CurrentTenantId),
            new TestUnitOfWork());

        var result = await handler.Handle(new ArchiveAccountCompanyCommand(companyRepository.Items[0].PublicId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AccountCompanyErrors.OwnershipForbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task Reactivate_WhenArchivedAndWithinLimit_ShouldReturnActiveCompany()
    {
        var userRepository = new TestUserRepository();
        userRepository.Seed(CreatePersistedUser(CurrentUserId, "owner@test.com"));
        var companyRepository = new TestCompanyRepository();
        var company = CreateCompany(CurrentUserId, "Acme Services", "acme-services");
        company.Archive();
        companyRepository.Add(company);
        var auditService = new TestAuditService();

        var handler = new ReactivateAccountCompanyCommandHandler(
            new TestCurrentUserService(CurrentUserId),
            userRepository,
            companyRepository,
            new TestCompanyOwnershipPolicy(hasCapacity: true),
            auditService,
            new TestTenantContext(CurrentTenantId),
            new TestUnitOfWork());

        var result = await handler.Handle(new ReactivateAccountCompanyCommand(company.PublicId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CompanyStatus.Active, company.Status);
        Assert.Equal(AuditEventTypes.CompanyReactivated, Assert.Single(auditService.Entries).Entry.EventType);
    }

    [Fact]
    public async Task Reactivate_WhenLimitWouldBeExceeded_ShouldReturnConflict()
    {
        var userRepository = new TestUserRepository();
        userRepository.Seed(CreatePersistedUser(CurrentUserId, "owner@test.com"));
        var companyRepository = new TestCompanyRepository();
        var company = CreateCompany(CurrentUserId, "Acme Services", "acme-services");
        company.Archive();
        companyRepository.Add(company);

        var handler = new ReactivateAccountCompanyCommandHandler(
            new TestCurrentUserService(CurrentUserId),
            userRepository,
            companyRepository,
            new TestCompanyOwnershipPolicy(hasCapacity: false),
            new TestAuditService(),
            new TestTenantContext(CurrentTenantId),
            new TestUnitOfWork());

        var result = await handler.Handle(new ReactivateAccountCompanyCommand(company.PublicId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AccountCompanyErrors.CompanyReactivationLimitReached.Code, result.Error.Code);
    }

    [Fact]
    public async Task Switch_ShouldUpdatePrimaryMembershipAndReturnTokenForTargetTenant()
    {
        var userRepository = new TestUserRepository();
        var user = CreatePersistedUser(CurrentUserId, "owner@test.com");
        userRepository.Seed(user);

        var companyRepository = new TestCompanyRepository();
        var companyA = CreateCompany(CurrentUserId, "Acme One", "acme-one", publicId: CurrentTenantId);
        var companyB = CreateCompany(CurrentUserId, "Acme Two", "acme-two");
        companyRepository.Add(companyA);
        companyRepository.Add(companyB);

        var membershipRepository = new TestUserCompanyRepository(companyRepository);
        membershipRepository.Add(UserCompanyMembership.Create(user.Id, companyA.Id, roleId: 10, isPrimary: true));
        membershipRepository.Add(UserCompanyMembership.Create(user.Id, companyB.Id, roleId: 11, isPrimary: false));

        var tokenService = new TestTokenService();
        var auditService = new TestAuditService();

        var handler = new SwitchActiveCompanyCommandHandler(
            new TestCurrentUserService(CurrentUserId),
            userRepository,
            companyRepository,
            membershipRepository,
            tokenService,
            auditService,
            new TestUnitOfWork());

        var result = await handler.Handle(new SwitchActiveCompanyCommand(companyB.PublicId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(companyB.PublicId, tokenService.LastTenantId);
        Assert.Equal(companyB.PublicId, result.Value.ActiveCompany.PublicId);
        Assert.Contains(membershipRepository.Items, item => item.CompanyId == companyB.Id && item.IsPrimary);
        Assert.DoesNotContain(membershipRepository.Items, item => item.CompanyId == companyA.Id && item.IsPrimary);
        Assert.Equal(AuditEventTypes.ActiveCompanySwitched, Assert.Single(auditService.Entries).Entry.EventType);
    }

    private static User CreatePersistedUser(Guid publicId, string email)
    {
        var user = User.RegisterLocal("Owner", "User", email, "hashed-password", "SV", "tests");
        SetEntityId(user, 100);
        SetPublicId(user, publicId);
        return user;
    }

    private static Company CreateCompany(Guid ownerUserPublicId, string name, string slug, Guid? publicId = null, string countryCode = "SV")
    {
        var company = Company.Create(name, slug, ownerUserPublicId, countryCode, 1);
        SetEntityId(company, Random.Shared.NextInt64(1, 1000));
        if (publicId.HasValue)
        {
            SetPublicId(company, publicId.Value);
        }

        company.MarkCreated(CreatedAtUtc);
        return company;
    }

    private static InitialLegalRepresentativeInput CreateInitialLegalRepresentative() =>
        new(
            "Ana",
            "Mendoza",
            LegalRepresentativeDocumentType.TaxId,
            "0614-290190-102-3",
            "Representante Legal",
            LegalRepresentativeRepresentationType.PrimaryLegalRepresentative,
            "Representación general judicial y administrativa",
            "Acta de nombramiento",
            new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            null,
            "ana.mendoza@test.com",
            "+50370000000",
            IsPrimary: true);

    private static void SetEntityId(Entity entity, long id)
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(entity, [id]);
    }

    private static void SetPublicId(object instance, Guid publicId)
    {
        instance.GetType()
            .GetProperty("PublicId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(instance, [publicId]);
    }

    private sealed class TestCurrentUserService(Guid currentUserId) : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public string? UserId => currentUserId.ToString();

        public IReadOnlyCollection<string> Roles => [];

        public IReadOnlyCollection<string> Permissions => [];
    }

    private sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class TestUserRepository : IUserRepository
    {
        public List<User> Items { get; } = [];

        public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(user => user.Id == userId));

        public Task<User?> GetByPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(user => user.PublicId == userPublicId));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(user => user.NormalizedEmail == User.NormalizeEmail(email)));

        public Task<User?> GetByExternalProviderAsync(AuthProvider authProvider, string providerUserId, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Seed(User user) => Items.Add(user);
    }

    private sealed class TestCompanyRepository : ICompanyRepository
    {
        public List<Company> Items { get; } = [];

        public void Add(Company company) => Items.Add(company);

        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(company => company.Slug == slug));

        public Task<Company?> FindByPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(company => company.PublicId == companyPublicId));

        public Task<AccountCompanyDetailResponse?> FindOwnedByUserAsync(
            Guid companyPublicId,
            Guid ownerUserPublicId,
            Guid? activeTenantId,
            CancellationToken cancellationToken)
        {
            var company = Items.SingleOrDefault(item => item.PublicId == companyPublicId && item.CreatedByUserPublicId == ownerUserPublicId);
            if (company is null)
            {
                return Task.FromResult<AccountCompanyDetailResponse?>(null);
            }

            return Task.FromResult<AccountCompanyDetailResponse?>(new AccountCompanyDetailResponse(
                company.PublicId,
                company.Name,
                company.Slug,
                company.CountryCode,
                company.Status,
                ProvisioningConstants.FreePlanCode,
                activeTenantId.HasValue && activeTenantId.Value == company.PublicId,
                IsOwnedByCurrentUser: true,
                company.CreatedUtc,
                company.ModifiedUtc,
                Array.Empty<ActiveLegalRepresentativeSummaryResponse>(),
                CompanyType: null));
        }

        public async Task<PagedResponse<AccountCompanySummaryResponse>> GetOwnedByUserAsync(
            Guid ownerUserPublicId,
            CompanyListFilter filter,
            CancellationToken cancellationToken)
        {
            var query = Items.Where(item => item.CreatedByUserPublicId == ownerUserPublicId);
            if (filter.Status.HasValue)
            {
                query = query.Where(item => item.Status == filter.Status.Value);
            }

            var items = query
                .OrderBy(item => item.Name)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(item => new AccountCompanySummaryResponse(
                    item.PublicId,
                    item.Name,
                    item.Slug,
                    item.CountryCode,
                    item.Status,
                    ProvisioningConstants.FreePlanCode,
                    filter.ActiveTenantId.HasValue && filter.ActiveTenantId.Value == item.PublicId,
                    IsOwnedByCurrentUser: true,
                    item.CreatedUtc,
                    CompanyType: null))
                .ToArray();

            return await Task.FromResult(new PagedResponse<AccountCompanySummaryResponse>(
                items,
                filter.PageNumber,
                filter.PageSize,
                query.Count()));
        }

        public Task<int> CountOwnedByUserAsync(
            Guid ownerUserPublicId,
            CompanyOwnershipCountFilter filter,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.Count(item =>
                item.CreatedByUserPublicId == ownerUserPublicId &&
                filter.Statuses.Contains(item.Status)));
    }

    private sealed class TestCountryCatalogRepository : ICountryCatalogRepository
    {
        public Task<IReadOnlyCollection<CountryCatalogItemResponse>> GetActiveItemsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<CountryCatalogItemResponse>>(
                [new CountryCatalogItemResponse(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), "SV", "El Salvador", 10)]);

        public Task<CountryCatalogLookup?> GetActiveByCodeAsync(string countryCode, CancellationToken cancellationToken)
        {
            var normalizedCode = countryCode.Trim().ToUpperInvariant();
            return Task.FromResult<CountryCatalogLookup?>(
                normalizedCode == "SV"
                    ? new CountryCatalogLookup(-7001, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), "SV", "El Salvador", true)
                    : null);
        }
    }

    private sealed class TestUserCompanyRepository(TestCompanyRepository companyRepository) : IUserCompanyRepository
    {
        public List<UserCompanyMembership> Items { get; } = [];

        public void Add(UserCompanyMembership membership)
        {
            if (membership.Id == 0)
            {
                SetEntityId(membership, Items.Count + 1L);
            }

            Items.Add(membership);
        }

        public Task<bool> ExistsInCompanyAsync(Guid companyPublicId, string normalizedEmail, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasAnyMembershipAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(item => item.UserId == userId));

        public Task<bool> HasPrimaryCompanyAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(item => item.UserId == userId && item.IsPrimary));

        public Task<Guid?> GetPrimaryCompanyPublicIdAsync(long userId, CancellationToken cancellationToken)
        {
            var membership = Items.SingleOrDefault(item => item.UserId == userId && item.IsPrimary);
            var companyPublicId = membership is null
                ? (Guid?)null
                : companyRepository.Items.Single(item => item.Id == membership.CompanyId).PublicId;

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
            throw new NotSupportedException();

        public Task<bool> HasActiveMembershipAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            var active = company is not null &&
                Items.Any(item => item.UserId == userId && item.CompanyId == company.Id && item.Status == UserCompanyStatus.Active);

            return Task.FromResult(active);
        }

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
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanyUserResponse?> GetUserAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestCompanyOwnershipPolicy(bool hasCapacity) : ICompanyOwnershipPolicy
    {
        public Task<bool> HasCapacityForAnotherActiveCompanyAsync(Guid ownerUserPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(hasCapacity);
    }

    private sealed class TestCompanyProvisioningService(TestCompanyRepository companyRepository, ProvisionedCompanyResult? nextResult) : ICompanyProvisioningService
    {
        public ProvisionCompanyRequest? LastRequest { get; private set; }

        public Task<Result<ProvisionedCompanyResult>> ProvisionAsync(
            ProvisionCompanyRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (nextResult is not null)
            {
                companyRepository.Add(CreateCompany(
                    request.OwnerUserPublicId,
                    nextResult.CompanyName,
                    nextResult.Slug,
                    nextResult.CompanyId,
                    request.CountryCode));
            }

            return Task.FromResult(
                nextResult is null
                    ? Result<ProvisionedCompanyResult>.Failure(AccountCompanyErrors.CompanyLimitReached)
                    : Result<ProvisionedCompanyResult>.Success(nextResult));
        }
    }

    private sealed class TestOrgStructureCatalogRepository : IOrgStructureCatalogRepository
    {
        public void AddOrgUnitType(CLARIHR.Domain.OrgStructureCatalogs.OrgUnitTypeCatalogItem item) =>
            throw new NotSupportedException();

        public void AddFunctionalArea(CLARIHR.Domain.OrgStructureCatalogs.FunctionalAreaCatalogItem item) =>
            throw new NotSupportedException();

        public Task<CLARIHR.Domain.OrgStructureCatalogs.OrgUnitTypeCatalogItem?> GetOrgUnitTypeByIdAsync(Guid orgUnitTypeId, CancellationToken cancellationToken) =>
            Task.FromResult<CLARIHR.Domain.OrgStructureCatalogs.OrgUnitTypeCatalogItem?>(null);

        public Task<CLARIHR.Domain.OrgStructureCatalogs.FunctionalAreaCatalogItem?> GetFunctionalAreaByIdAsync(Guid functionalAreaId, CancellationToken cancellationToken) =>
            Task.FromResult<CLARIHR.Domain.OrgStructureCatalogs.FunctionalAreaCatalogItem?>(null);

        public Task<bool> ExistsOrgUnitTypeOutsideTenantAsync(Guid orgUnitTypeId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> ExistsFunctionalAreaOutsideTenantAsync(Guid functionalAreaId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> OrgUnitTypeCodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> FunctionalAreaCodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<PagedResponse<OrgUnitTypeCatalogItemResponse>> SearchOrgUnitTypesAsync(Guid tenantId, bool? isActive, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<OrgUnitTypeCatalogItemResponse>([], pageNumber, pageSize, 0));

        public Task<PagedResponse<FunctionalAreaCatalogItemResponse>> SearchFunctionalAreasAsync(Guid tenantId, bool? isActive, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<FunctionalAreaCatalogItemResponse>([], pageNumber, pageSize, 0));

        public Task<IReadOnlyCollection<CompanyTypeCatalogItemResponse>> GetActiveCompanyTypesByCountryCodeAsync(string countryCode, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<CompanyTypeCatalogItemResponse>>([]);

        public Task<OrgUnitTypeCatalogItemResponse?> GetOrgUnitTypeResponseByIdAsync(Guid orgUnitTypeId, CancellationToken cancellationToken) =>
            Task.FromResult<OrgUnitTypeCatalogItemResponse?>(null);

        public Task<FunctionalAreaCatalogItemResponse?> GetFunctionalAreaResponseByIdAsync(Guid functionalAreaId, CancellationToken cancellationToken) =>
            Task.FromResult<FunctionalAreaCatalogItemResponse?>(null);

        public Task<bool> HasOrgUnitsUsingOrgUnitTypeAsync(long orgUnitTypeCatalogItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> HasPositionCategoryClassificationsUsingOrgUnitTypeAsync(long orgUnitTypeCatalogItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> HasOrgUnitsUsingFunctionalAreaAsync(long functionalAreaCatalogItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<CatalogReferenceLookup?> GetActiveCompanyTypeLookupAsync(long countryCatalogItemId, Guid companyTypeId, CancellationToken cancellationToken) =>
            Task.FromResult<CatalogReferenceLookup?>(null);

        public Task<CatalogReferenceLookup?> GetActiveOrgUnitTypeLookupAsync(Guid tenantId, Guid orgUnitTypeId, CancellationToken cancellationToken) =>
            Task.FromResult<CatalogReferenceLookup?>(null);

        public Task<CatalogReferenceLookup?> GetActiveFunctionalAreaLookupAsync(Guid tenantId, Guid functionalAreaId, CancellationToken cancellationToken) =>
            Task.FromResult<CatalogReferenceLookup?>(null);
    }

    private sealed class TestTokenService : ITokenService
    {
        public Guid? LastTenantId { get; private set; }

        public Task<Result<AuthTokenResult>> GenerateAsync(User user, CancellationToken cancellationToken) =>
            Task.FromResult(Result<AuthTokenResult>.Success(new AuthTokenResult(
                "jwt-token",
                "refresh-token",
                900)));

        public Task<Result<AuthTokenResult>> GenerateForTenantAsync(User user, Guid tenantId, CancellationToken cancellationToken)
        {
            LastTenantId = tenantId;
            return Task.FromResult(Result<AuthTokenResult>.Success(new AuthTokenResult(
                "jwt-token",
                "refresh-token",
                900)));
        }

        public Task<Result<AuthTokenResult>> GeneratePlatformAsync(User user, CancellationToken cancellationToken) =>
            Task.FromResult(Result<AuthTokenResult>.Success(new AuthTokenResult(
                "jwt-token",
                "refresh-token",
                900)));

        public Task<Result<RefreshTokenExchangeResult>> RefreshAsync(
            string refreshToken,
            AuthClientType clientType,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestAuditService : IAuditService
    {
        public List<(Guid TenantId, AuditLogEntry Entry)> Entries { get; } = [];

        public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add((Guid.Empty, entry));
            return Task.CompletedTask;
        }

        public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add((tenantId, entry));
            return Task.CompletedTask;
        }
    }
}
