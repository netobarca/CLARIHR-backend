using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.PositionSlots;

namespace CLARIHR.Application.UnitTests;

public sealed class FinalizePersonnelFileTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task Preview_WhenCreateUserAccountIsTrueAndFieldsAreMissing_ShouldReturnIssues()
    {
        var slotId = Guid.NewGuid();
        var personnelFile = CreateDraftEmployee(slotId, institutionalEmail: null);
        var personnelFileRepository = new TestPersonnelFileRepository(personnelFile);
        var positionSlotRepository = new TestPositionSlotRepository(
            CreateSlot(slotId),
            CreateSlotResponse(slotId, roleId: null));
        var handler = CreatePreviewHandler(
            personnelFileRepository,
            positionSlotRepository,
            new TestUserRepository());

        var result = await handler.Handle(
            new PreviewFinalizePersonnelFileQuery(personnelFile.PublicId, CreateUserAccount: true, PositionSlotPublicId: slotId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsEligible);
        Assert.Contains(
            result.Value.Issues,
            issue => issue.Code == PersonnelFileErrors.FinalizeRequiresInstitutionalEmail.Code &&
                     issue.NavigationKey == FinalizePersonnelFileNavigationKeys.PersonnelFiles);
        Assert.Contains(
            result.Value.Issues,
            issue => issue.Code == PersonnelFileErrors.FinalizeRequiresPositionSlotRole.Code &&
                     issue.NavigationKey == FinalizePersonnelFileNavigationKeys.PersonalInfo);
    }

    [Fact]
    public async Task Preview_WhenCreateUserAccountIsFalse_ShouldIgnoreMissingPositionSlotRole()
    {
        var slotId = Guid.NewGuid();
        var personnelFile = CreateDraftEmployee(slotId, institutionalEmail: "ana@clarihr.test");
        var personnelFileRepository = new TestPersonnelFileRepository(personnelFile);
        var positionSlotRepository = new TestPositionSlotRepository(
            CreateSlot(slotId),
            CreateSlotResponse(slotId, roleId: null));
        var handler = CreatePreviewHandler(
            personnelFileRepository,
            positionSlotRepository,
            new TestUserRepository());

        var result = await handler.Handle(
            new PreviewFinalizePersonnelFileQuery(personnelFile.PublicId, CreateUserAccount: false, PositionSlotPublicId: slotId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsEligible);
        Assert.Empty(result.Value.Issues);
    }

    [Fact]
    public async Task Handle_WhenCreateUserAccountIsFalse_ShouldFinalizeWithoutProvisioning()
    {
        var slotId = Guid.NewGuid();
        var personnelFile = CreateDraftEmployee(slotId, institutionalEmail: "ana@clarihr.test");
        var personnelFileRepository = new TestPersonnelFileRepository(personnelFile);
        var positionSlotRepository = new TestPositionSlotRepository(
            CreateSlot(slotId),
            CreateSlotResponse(slotId, roleId: null));
        var provisioningService = new TestCompanyUserProvisioningService();
        var handler = CreateHandler(
            personnelFileRepository,
            positionSlotRepository,
            provisioningService,
            new TestUserRepository());

        var result = await handler.Handle(
            new FinalizePersonnelFileCommand(personnelFile.PublicId, personnelFile.ConcurrencyToken, CreateUserAccount: false, PositionSlotPublicId: slotId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PersonnelFileLifecycleStatus.Completed, result.Value.PersonnelFile.LifecycleStatus);
        Assert.Null(result.Value.User);
        Assert.Null(result.Value.InvitationExpiresUtc);
        Assert.Null(personnelFile.LinkedUserPublicId);
        Assert.Equal(0, provisioningService.ProvisionCalls);
    }

    [Fact]
    public async Task Handle_WhenCreateUserAccountIsFalseAndInstitutionalEmailMissing_ShouldReturnValidationError()
    {
        var slotId = Guid.NewGuid();
        var personnelFile = CreateDraftEmployee(slotId, institutionalEmail: null);
        var personnelFileRepository = new TestPersonnelFileRepository(personnelFile);
        var positionSlotRepository = new TestPositionSlotRepository(
            CreateSlot(slotId),
            CreateSlotResponse(slotId, roleId: null));
        var provisioningService = new TestCompanyUserProvisioningService();
        var handler = CreateHandler(
            personnelFileRepository,
            positionSlotRepository,
            provisioningService,
            new TestUserRepository());

        var result = await handler.Handle(
            new FinalizePersonnelFileCommand(personnelFile.PublicId, personnelFile.ConcurrencyToken, CreateUserAccount: false, PositionSlotPublicId: slotId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PersonnelFileErrors.FinalizeRequiresInstitutionalEmail.Code, result.Error.Code);
        Assert.Equal(0, provisioningService.ProvisionCalls);
    }

    [Fact]
    public async Task Handle_WhenCreateUserAccountIsTrue_ShouldKeepProvisioningFlow()
    {
        var slotId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var personnelFile = CreateDraftEmployee(slotId, institutionalEmail: "ana@clarihr.test");
        var personnelFileRepository = new TestPersonnelFileRepository(personnelFile);
        var positionSlotRepository = new TestPositionSlotRepository(
            CreateSlot(slotId),
            CreateSlotResponse(slotId, roleId));
        var provisioningService = new TestCompanyUserProvisioningService
        {
            NextResult = Result<CompanyUserProvisioningResult>.Success(
                new CompanyUserProvisioningResult(
                    User.InviteLocalWithTemporaryPassword("Ana", "Mendoza", "ana@clarihr.test", "hashed-password", "SV", "tests"),
                    new CompanyUserResponse(Guid.NewGuid(), "ana@clarihr.test", "Ana", "Mendoza", [], UserStatus.PendingActivation),
                    new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                    WasCreated: true,
                    MembershipReused: false,
                    InvitationIssued: true))
        };

        var handler = CreateHandler(
            personnelFileRepository,
            positionSlotRepository,
            provisioningService,
            new TestUserRepository());

        var result = await handler.Handle(
            new FinalizePersonnelFileCommand(personnelFile.PublicId, personnelFile.ConcurrencyToken, CreateUserAccount: true, PositionSlotPublicId: slotId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.User);
        Assert.NotNull(personnelFile.LinkedUserPublicId);
        Assert.Equal(1, provisioningService.ProvisionCalls);
    }

    [Fact]
    public async Task Handle_WhenCreateUserAccountIsTrueAndPositionSlotRoleIsMissing_ShouldReturnValidationError()
    {
        var slotId = Guid.NewGuid();
        var personnelFile = CreateDraftEmployee(slotId, institutionalEmail: "ana@clarihr.test");
        var personnelFileRepository = new TestPersonnelFileRepository(personnelFile);
        var positionSlotRepository = new TestPositionSlotRepository(
            CreateSlot(slotId),
            CreateSlotResponse(slotId, roleId: null));
        var provisioningService = new TestCompanyUserProvisioningService();
        var handler = CreateHandler(
            personnelFileRepository,
            positionSlotRepository,
            provisioningService,
            new TestUserRepository());

        var result = await handler.Handle(
            new FinalizePersonnelFileCommand(personnelFile.PublicId, personnelFile.ConcurrencyToken, CreateUserAccount: true, PositionSlotPublicId: slotId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PersonnelFileErrors.FinalizeRequiresPositionSlotRole.Code, result.Error.Code);
        Assert.Equal(0, provisioningService.ProvisionCalls);
    }

    private static FinalizePersonnelFileCommandHandler CreateHandler(
        TestPersonnelFileRepository personnelFileRepository,
        TestPositionSlotRepository positionSlotRepository,
        TestCompanyUserProvisioningService provisioningService,
        TestUserRepository userRepository)
    {
        var authorizationService = new AllowPersonnelFileAuthorizationService();
        var auditService = new TestAuditService();
        var tenantContext = new TestTenantContext(TenantId);
        var unitOfWork = new TestUnitOfWork();

        return new FinalizePersonnelFileCommandHandler(
            authorizationService,
            personnelFileRepository,
            positionSlotRepository,
            new PersonnelFileFinalizationService(provisioningService),
            userRepository,
            auditService,
            tenantContext,
            unitOfWork);
    }

    private static PreviewFinalizePersonnelFileQueryHandler CreatePreviewHandler(
        TestPersonnelFileRepository personnelFileRepository,
        TestPositionSlotRepository positionSlotRepository,
        TestUserRepository userRepository)
    {
        var authorizationService = new AllowPersonnelFileAuthorizationService();
        var tenantContext = new TestTenantContext(TenantId);
        return new PreviewFinalizePersonnelFileQueryHandler(
            authorizationService,
            personnelFileRepository,
            positionSlotRepository,
            userRepository,
            tenantContext);
    }

    private static PersonnelFile CreateDraftEmployee(Guid slotId, string? institutionalEmail)
    {
        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            "Ana",
            "Mendoza",
            new DateTime(1990, 1, 1),
            maritalStatus: null,
            profession: null,
            nationality: "SV",
            personalEmail: "ana.personal@test.com",
            institutionalEmail: institutionalEmail,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: "SV",
            birthDepartment: null,
            birthMunicipality: null,
            photoFilePublicId: null,
            orgUnitPublicId: null);
        file.SetTenantId(TenantId);
        return file;
    }

    private static PositionSlot CreateSlot(Guid slotId)
    {
        var slot = PositionSlot.Create(
            code: "DEV-001",
            title: "Developer",
            jobProfileId: 1,
            roleId: null,
            workCenterId: null,
            directDependencyPositionSlotId: null,
            functionalDependencyPositionSlotId: null,
            status: PositionSlotStatus.Vacant,
            maxEmployees: 1,
            occupiedEmployees: 0,
            isFixedTerm: false,
            effectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            effectiveToUtc: null,
            notes: null);
        slot.SetTenantId(TenantId);
        SetEntityPublicId(slot, slotId);
        return slot;
    }

    private static PositionSlotResponse CreateSlotResponse(Guid slotId, Guid? roleId) =>
        new(
            Id: slotId,
            CompanyId: TenantId,
            Code: "DEV-001",
            Title: "Developer",
            Status: PositionSlotStatus.Vacant,
            JobProfileId: Guid.NewGuid(),
            JobProfileCode: "JOB-001",
            JobProfileTitle: "Developer Profile",
            RoleId: roleId,
            RoleName: roleId is null ? null : "Employee",
            OrgUnitId: Guid.NewGuid(),
            OrgUnitCode: "ORG-001",
            OrgUnitName: "Engineering",
            WorkCenterId: null,
            WorkCenterCode: null,
            WorkCenterName: null,
            CostCenterCode: null,
            DirectDependencyPositionSlotId: null,
            DirectDependencyPositionSlotCode: null,
            FunctionalDependencyPositionSlotId: null,
            FunctionalDependencyPositionSlotCode: null,
            PositionCategoryId: null,
            PositionCategoryCode: null,
            PositionCategoryName: null,
            PositionCategoryClassificationId: null,
            PositionCategoryClassificationCode: null,
            PositionCategoryClassificationName: null,
            ContractTypeId: null,
            ContractTypeCode: null,
            ContractTypeName: null,
            MaxEmployees: 1,
            OccupiedEmployees: 0,
            EffectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveToUtc: null,
            Notes: null,
            IsActive: true,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedAtUtc: null,
            AllowedActions: null);

    private static PersonnelFileResponse ToResponse(PersonnelFile file) =>
        new(
            file.PublicId,
            file.TenantId,
            file.RecordType,
            file.LifecycleStatus,
            file.FirstName,
            file.LastName,
            file.FullName,
            file.BirthDate,
            36,
            file.MaritalStatus,
            null,
            file.Profession,
            null,
            file.Nationality,
            file.PersonalEmail,
            file.InstitutionalEmail,
            file.PersonalPhone,
            file.InstitutionalPhone,
            file.BirthCountry,
            null,
            file.BirthDepartment,
            null,
            file.BirthMunicipality,
            null,
            file.PhotoFilePublicId?.ToString(),
            file.OrgUnitPublicId,
            file.LinkedUserPublicId,
            file.IsActive,
            file.ConcurrencyToken,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            null,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            null);

    private static void SetEntityPublicId(object entity, Guid value)
    {
        var property = entity.GetType().BaseType?.GetProperty("PublicId");
        property?.SetValue(entity, value);
    }

    private sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class TestAuditService : IAuditService
    {
        public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AllowPersonnelFileAuthorizationService : IPersonnelFileAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task<bool> HasRehireAuthorizationAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(true);

        public Error TenantMismatch(RbacPermissionAction action) =>
            new("TENANT_MISMATCH", "Tenant mismatch.", ErrorType.Forbidden);
    }

    private sealed class TestCompanyUserProvisioningService : ICompanyUserProvisioningService
    {
        public int ProvisionCalls { get; private set; }

        public Result<CompanyUserProvisioningResult> NextResult { get; set; } =
            Result<CompanyUserProvisioningResult>.Failure(new Error("test.provisioning.not_configured", "Provisioning result was not configured.", ErrorType.Failure));

        public Task<Result<CompanyUserProvisioningResult>> ProvisionAsync(CompanyUserProvisioningRequest request, CancellationToken cancellationToken)
        {
            ProvisionCalls++;
            return Task.FromResult(NextResult);
        }

        public Task<Result<int>> SyncRoleAssignmentsForPositionSlotAsync(Guid companyPublicId, Guid assignedPositionSlotId, Guid roleId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestUserRepository : IUserRepository
    {
        public User? UserByEmail { get; set; }

        public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<User?> GetByPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(UserByEmail is not null && string.Equals(UserByEmail.Email, email, StringComparison.OrdinalIgnoreCase) ? UserByEmail : null);

        public Task<User?> GetByExternalProviderAsync(AuthProvider authProvider, string providerUserId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(User user, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestPositionSlotRepository(PositionSlot slot, PositionSlotResponse response) : IPositionSlotRepository
    {
        public void Add(PositionSlot slot) => throw new NotSupportedException();

        public Task<PositionSlot?> GetByIdAsync(Guid slotId, CancellationToken cancellationToken) =>
            Task.FromResult(slotId == slot.PublicId ? slot : null);

        public Task<bool> ExistsOutsideTenantAsync(Guid slotId, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingSlotId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> JobProfileExistsOutsideTenantAsync(Guid jobProfileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<long?> ResolveWorkCenterIdAsync(Guid tenantId, Guid workCenterId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> WorkCenterExistsOutsideTenantAsync(Guid workCenterId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<long?> ResolvePositionSlotIdAsync(Guid tenantId, Guid slotId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PagedResponse<PositionSlotListItemResponse>> SearchAsync(Guid tenantId, PositionSlotStatus? status, Guid? jobProfileId, Guid? orgUnitId, Guid? workCenterId, Guid? contractTypeId, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PositionSlotResponse?> GetResponseByIdAsync(Guid slotId, CancellationToken cancellationToken) =>
            Task.FromResult<PositionSlotResponse?>(slotId == slot.PublicId ? response : null);

        public Task<int> CountSlotsAsync(Guid tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PositionSlotGraphNodeData>> GetGraphNodesAsync(Guid tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PositionSlotDependencyAdjacency>> GetDependencyAdjacencyAsync(Guid tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AcquireDependencyMutationLockAsync(Guid tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PositionSlotExportRow>> GetExportRowsAsync(Guid tenantId, PositionSlotStatus? status, Guid? jobProfileId, Guid? orgUnitId, Guid? workCenterId, Guid? contractTypeId, string? search, int? maxRows, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PositionSlotJobProfileLookup?> GetJobProfileLookupAsync(Guid tenantId, Guid jobProfileId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestPersonnelFileRepository(PersonnelFile file) : IPersonnelFileRepository
    {
        public void Add(PersonnelFile personnelFile) => throw new NotSupportedException();

        public Task<int> CountActiveEmployeesAsync(Guid tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFile?> GetByIdAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
            Task.FromResult(personnelFileId == file.PublicId ? file : null);

        public Task<PersonnelFile?> GetForAccessCheckAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
            GetByIdAsync(personnelFileId, cancellationToken);

        public Task<PersonnelFile?> GetForProfileSectionUpdateAsync(Guid personnelFileId, PersonnelFileTrackedSection section, CancellationToken cancellationToken) =>
            GetByIdAsync(personnelFileId, cancellationToken);

        public Task<PersonnelFile?> GetByLinkedUserIdAsync(Guid tenantId, Guid linkedUserPublicId, CancellationToken cancellationToken) =>
            Task.FromResult<PersonnelFile?>(null);

        public Task<bool> ExistsOutsideTenantAsync(Guid personnelFileId, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> IdentificationExistsAsync(Guid tenantId, string identificationType, string normalizedIdentificationNumber, long? excludingPersonnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PagedResponse<PersonnelFileListItemResponse>> SearchAsync(Guid tenantId, bool? isActive, PersonnelFileRecordType? recordType, Guid? orgUnitId, int? minAge, int? maxAge, string? maritalStatus, string? nationality, string? profession, DateTime? createdFromUtc, DateTime? createdToUtc, string? search, string? sortBy, PersonnelFileSortDirection sortDirection, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileShellResponse?> GetShellByIdAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
            Task.FromResult(
                personnelFileId == file.PublicId
                    ? new PersonnelFileShellResponse(
                        file.PublicId,
                        file.TenantId,
                        file.RecordType,
                        file.LifecycleStatus,
                        file.FullName,
                        file.PhotoFilePublicId?.ToString(),
                        file.IsActive,
                        file.OrgUnitPublicId,
                        file.LinkedUserPublicId,
                        file.ConcurrencyToken,
                        file.CreatedUtc,
                        file.ModifiedUtc)
                    : null);

        public Task<PersonnelFileResponse?> GetResponseByIdAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
            Task.FromResult<PersonnelFileResponse?>(personnelFileId == file.PublicId ? ToResponse(file) : null);

        public Task<PersonnelFilePersonalInfoResponse?> GetPersonalInfoAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileIdentificationResponse>> GetIdentificationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileIdentificationResponse?> GetIdentificationAsync(Guid personnelFileId, Guid identificationPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileAddressResponse>> GetAddressesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileAddressResponse?> GetAddressAsync(Guid personnelFileId, Guid addressPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>> GetEmergencyContactsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileEmergencyContactResponse?> GetEmergencyContactAsync(Guid personnelFileId, Guid emergencyContactPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>> GetFamilyMembersAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileFamilyMemberResponse?> GetFamilyMemberAsync(Guid personnelFileId, Guid familyMemberPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileHobbyResponse>> GetHobbiesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileHobbyResponse?> GetHobbyAsync(Guid personnelFileId, Guid hobbyPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>> GetEmployeeRelationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileEmployeeRelationResponse?> GetEmployeeRelationAsync(Guid personnelFileId, Guid employeeRelationPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileBankAccountResponse>> GetBankAccountsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileBankAccountResponse?> GetBankAccountAsync(Guid personnelFileId, Guid bankAccountPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileAssociationResponse>> GetAssociationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileAssociationResponse?> GetAssociationAsync(Guid personnelFileId, Guid associationPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileEducationResponse>> GetEducationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileEducationResponse?> GetEducationAsync(Guid personnelFileId, Guid educationPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileLanguageResponse?> GetLanguageAsync(Guid personnelFileId, Guid languagePublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileLanguageResponse>> GetLanguagesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileTrainingResponse>> GetTrainingsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileTrainingResponse?> GetTrainingAsync(Guid personnelFileId, Guid trainingPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>> GetPreviousEmploymentsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFilePreviousEmploymentResponse?> GetPreviousEmploymentAsync(Guid personnelFileId, Guid previousEmploymentPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileReferenceResponse>> GetReferencesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileReferenceResponse?> GetReferenceAsync(Guid personnelFileId, Guid referencePublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>> GetDocumentsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileDocumentMetadataResponse?> GetDocumentMetadataByIdAsync(Guid personnelFileId, Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileObservationResponse>> GetObservationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Guid>> GetBankAccountIdsAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());

        public Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetCatalogItemsAsync(string? countryCode, string category, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>> GetReferenceCatalogItemsAsync(string countryCode, string category, string? parentCode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> GetCompanyCountryCodeAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> CatalogCodeIsActiveAsync(Guid companyId, string category, string code, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> CountryCodeIsActiveAsync(string countryCode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ReferenceCatalogCodeIsActiveAsync(string countryCode, string category, string code, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ReferenceMunicipalityBelongsToDepartmentAsync(string countryCode, string departmentCode, string municipalityCode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> DocumentExistsOutsideTenantAsync(Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileExportRow>> GetExportRowsAsync(Guid tenantId, bool? isActive, PersonnelFileRecordType? recordType, Guid? orgUnitId, int? minAge, int? maxAge, string? maritalStatus, string? nationality, string? profession, DateTime? createdFromUtc, DateTime? createdToUtc, string? search, string? sortBy, PersonnelFileSortDirection sortDirection, int? maxRows, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileDynamicQueryResponse> DynamicQueryAsync(Guid tenantId, IReadOnlyCollection<PersonnelFileDynamicFilterInput> filters, IReadOnlyCollection<string> groupBy, IReadOnlyCollection<PersonnelFileDynamicSortInput> sort, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileAnalyticsSummaryResponse> GetAnalyticsSummaryAsync(Guid tenantId, bool? isActive, PersonnelFileRecordType? recordType, Guid? orgUnitId, int? minAge, int? maxAge, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Guid>> GetLinkedUserIdsByAssignedPositionSlotAsync(Guid tenantId, Guid assignedPositionSlotId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
