using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

public sealed class PersonnelFilesCoreCommandTests
{
    private static readonly Guid TenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task Create_WhenSuccessful_ShouldReturnShellWithoutLoadingFullResponse()
    {
        var repository = new TestPersonnelFileRepository();
        var auditService = new TestAuditService();
        var photoService = new TestProfilePhotoService();
        var unitOfWork = new TestUnitOfWork();
        var handler = new CreatePersonnelFileCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            auditService,
            photoService,
            unitOfWork);

        var result = await handler.Handle(
            new CreatePersonnelFileCommand(
                TenantId,
                PersonnelFileRecordType.Candidate,
                "Ana",
                "Ramirez",
                new DateTime(1991, 4, 5),
                MaritalStatusCode: null,
                ProfessionCode: null,
                Nationality: "SV",
                PersonalEmail: null,
                InstitutionalEmail: null,
                PersonalPhone: null,
                InstitutionalPhone: null,
                BirthCountryCode: null,
                BirthDepartmentCode: null,
                BirthMunicipalityCode: null,
                PhotoFilePublicId: null,
                OrgUnitId: null,
                AssignedPositionSlotId: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, repository.GetResponseByIdCalls);
        Assert.Equal("Ana Ramirez", result.Value.FullName);
        Assert.Equal(PersonnelFileLifecycleStatus.Draft, result.Value.LifecycleStatus);
        Assert.Equal(1, auditService.LogCalls);
        Assert.Equal(2, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Patch_WhenSettingIsActiveFalse_ShouldInactivateAndReturnPersonalInfo()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Lucia", "Perez");
        var repository = new TestPersonnelFileRepository(file);
        var auditService = new TestAuditService();
        var unitOfWork = new TestUnitOfWork();
        var handler = new PatchPersonnelFileCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            auditService,
            new TestProfilePhotoService(),
            new FixedTenantContext(TenantId),
            unitOfWork);

        var result = await handler.Handle(
            new PatchPersonnelFileCommand(
                file.PublicId,
                file.ConcurrencyToken,
                [new PersonnelFilePatchOperation("replace", "/isActive", null, JsonSerializer.SerializeToElement(false))]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsActive);
        Assert.False(file.IsActive);
        Assert.Equal(file.PublicId, result.Value.Id);
        Assert.Equal(1, auditService.LogCalls);
        Assert.Equal(2, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Patch_WhenReplacingCoreField_ShouldUpdateAndKeepActiveState()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Mario", "Lopez");
        var repository = new TestPersonnelFileRepository(file);
        var auditService = new TestAuditService();
        var unitOfWork = new TestUnitOfWork();
        var handler = new PatchPersonnelFileCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            auditService,
            new TestProfilePhotoService(),
            new FixedTenantContext(TenantId),
            unitOfWork);

        var result = await handler.Handle(
            new PatchPersonnelFileCommand(
                file.PublicId,
                file.ConcurrencyToken,
                [new PersonnelFilePatchOperation("replace", "/firstName", null, JsonSerializer.SerializeToElement("Mariano"))]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Mariano", result.Value.FirstName);
        Assert.True(file.IsActive);
        Assert.Equal("Mariano Lopez", file.FullName);
    }

    [Fact]
    public async Task Patch_WhenConcurrencyTokenStale_ShouldReturnConflict()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ada", "Lovelace");
        var repository = new TestPersonnelFileRepository(file);
        var handler = new PatchPersonnelFileCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new TestAuditService(),
            new TestProfilePhotoService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());

        var result = await handler.Handle(
            new PatchPersonnelFileCommand(
                file.PublicId,
                Guid.NewGuid(),
                [new PersonnelFilePatchOperation("replace", "/isActive", null, JsonSerializer.SerializeToElement(false))]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONCURRENCY_CONFLICT", result.Error.Code);
    }

    private static PersonnelFile CreatePersonnelFile(PersonnelFileRecordType recordType, string firstName, string lastName)
    {
        var file = PersonnelFile.Create(
            recordType,
            firstName,
            lastName,
            new DateTime(1990, 1, 1),
            maritalStatus: null,
            profession: null,
            nationality: "SV",
            personalEmail: null,
            institutionalEmail: null,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoFilePublicId: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: recordType == PersonnelFileRecordType.Employee ? Guid.NewGuid() : null);
        file.SetTenantId(TenantId);
        return file;
    }

    private sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class AllowPersonnelFileAuthorizationService : IPersonnelFileAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Error TenantMismatch(RbacPermissionAction action) =>
            new("TENANT_MISMATCH", "Tenant mismatch.", ErrorType.Forbidden);
    }

    private sealed class TestAuditService : IAuditService
    {
        public int LogCalls { get; private set; }

        public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        {
            LogCalls++;
            return Task.CompletedTask;
        }

        public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken)
        {
            LogCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestProfilePhotoService : IPersonnelFileProfilePhotoService
    {
        public Task<Result<PersonnelFileProfilePhotoWritePlan>> PrepareWriteAsync(
            Guid companyId,
            Guid personnelFileId,
            Guid? requestedPhotoFilePublicId,
            Guid? currentPersistedPhotoFilePublicId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<PersonnelFileProfilePhotoWritePlan>.Success(new PersonnelFileProfilePhotoWritePlan(null, null)));

        public Task<string?> ResolveForReadAsync(Guid? persistedPhotoFilePublicId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(persistedPhotoFilePublicId?.ToString());

        public Task CleanupAfterPersistenceFailureAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task CleanupAfterPersistenceSuccessAsync(PersonnelFileProfilePhotoWritePlan plan, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TestPersonnelFileRepository(params PersonnelFile[] files) : IPersonnelFileRepository
    {
        private readonly Dictionary<Guid, PersonnelFile> _files = files.ToDictionary(static file => file.PublicId);

        public int GetResponseByIdCalls { get; private set; }

        public void Add(PersonnelFile personnelFile) => _files[personnelFile.PublicId] = personnelFile;

        public Task<int> CountActiveEmployeesAsync(Guid tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFile?> GetByIdAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
            Task.FromResult(_files.GetValueOrDefault(personnelFileId));

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
                _files.TryGetValue(personnelFileId, out var file)
                    ? new PersonnelFileShellResponse(
                        file.PublicId,
                        file.TenantId,
                        file.RecordType,
                        file.LifecycleStatus,
                        file.FullName,
                        file.PhotoFilePublicId?.ToString(),
                        file.IsActive,
                        file.OrgUnitPublicId,
                        file.AssignedPositionSlotPublicId,
                        file.LinkedUserPublicId,
                        file.ConcurrencyToken,
                        file.CreatedUtc,
                        file.ModifiedUtc)
                    : null);

        public Task<PersonnelFileResponse?> GetResponseByIdAsync(Guid personnelFileId, CancellationToken cancellationToken)
        {
            GetResponseByIdCalls++;
            throw new NotSupportedException();
        }

        public Task<PersonnelFilePersonalInfoResponse?> GetPersonalInfoAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
            Task.FromResult(
                _files.TryGetValue(personnelFileId, out var file)
                    ? new PersonnelFilePersonalInfoResponse(
                        file.PublicId,
                        file.TenantId,
                        file.RecordType,
                        file.LifecycleStatus,
                        file.FirstName,
                        file.LastName,
                        file.FullName,
                        file.BirthDate,
                        0,
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
                        file.AssignedPositionSlotPublicId,
                        file.LinkedUserPublicId,
                        file.IsActive,
                        file.ConcurrencyToken,
                        file.CreatedUtc,
                        file.ModifiedUtc)
                    : null);
        public Task<IReadOnlyCollection<PersonnelFileIdentificationResponse>> GetIdentificationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileAddressResponse>> GetAddressesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>> GetEmergencyContactsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>> GetFamilyMembersAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileHobbyResponse>> GetHobbiesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>> GetEmployeeRelationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileBankAccountResponse>> GetBankAccountsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileAssociationResponse>> GetAssociationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileEducationResponse>> GetEducationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileLanguageResponse>> GetLanguagesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileTrainingResponse>> GetTrainingsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>> GetPreviousEmploymentsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileReferenceResponse>> GetReferencesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>> GetDocumentsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileDocumentMetadataResponse?> GetDocumentMetadataByIdAsync(Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileObservationResponse>> GetObservationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> GetBankAccountIdsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetCatalogItemsAsync(Guid companyId, string category, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>> GetReferenceCatalogItemsAsync(Guid companyId, string category, string? parentCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetCompanyCountryCodeAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult<string?>("SV");
        public Task<bool> CatalogCodeIsActiveAsync(Guid companyId, string category, string code, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> CountryCodeIsActiveAsync(string countryCode, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ReferenceCatalogCodeIsActiveAsync(string countryCode, string category, string code, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ReferenceMunicipalityBelongsToDepartmentAsync(string countryCode, string departmentCode, string municipalityCode, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<PersonnelFileDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DocumentExistsOutsideTenantAsync(Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileExportRow>> GetExportRowsAsync(Guid tenantId, bool? isActive, PersonnelFileRecordType? recordType, Guid? orgUnitId, int? minAge, int? maxAge, string? maritalStatus, string? nationality, string? profession, DateTime? createdFromUtc, DateTime? createdToUtc, string? search, string? sortBy, PersonnelFileSortDirection sortDirection, int? maxRows, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileDynamicQueryResponse> DynamicQueryAsync(Guid tenantId, IReadOnlyCollection<PersonnelFileDynamicFilterInput> filters, IReadOnlyCollection<string> groupBy, IReadOnlyCollection<PersonnelFileDynamicSortInput> sort, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileAnalyticsSummaryResponse> GetAnalyticsSummaryAsync(Guid tenantId, bool? isActive, PersonnelFileRecordType? recordType, Guid? orgUnitId, int? minAge, int? maxAge, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> GetLinkedUserIdsByAssignedPositionSlotAsync(Guid tenantId, Guid assignedPositionSlotId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
