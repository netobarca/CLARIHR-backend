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

public sealed class PersonnelFileEmployeeRelationsCommandTests
{
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Handle_WhenRelatedEmployeeExists_ShouldPersistRelationUsingPersonnelFileReference()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Employee, "Ana", "Owner");
        var relatedEmployee = CreatePersonnelFile(PersonnelFileRecordType.Employee, "Luis", "Related");
        var repository = new TestPersonnelFileRepository(personnelFile, relatedEmployee);
        var handler = CreateHandler(repository);

        var result = await handler.Handle(
            new ReplacePersonnelFileEmployeeRelationsCommand(
                personnelFile.PublicId,
                [new EmployeeRelationInput(relatedEmployee.PublicId, "Sibling")],
                personnelFile.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var relation = Assert.Single(personnelFile.EmployeeRelations);
        Assert.Equal(relatedEmployee.Id, relation.RelatedPersonnelFileId);
        Assert.Equal("Sibling", relation.Relationship);
        Assert.Equal(0, repository.GetResponseByIdCalls);
        Assert.Equal(2, repository.GetEmployeeRelationsCalls);
    }

    [Fact]
    public async Task Handle_WhenRelatedEmployeeIsSamePersonnelFile_ShouldReturnValidationError()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Employee, "Ana", "Owner");
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateHandler(repository);

        var result = await handler.Handle(
            new ReplacePersonnelFileEmployeeRelationsCommand(
                personnelFile.PublicId,
                [new EmployeeRelationInput(personnelFile.PublicId, "Sibling")],
                personnelFile.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.NotNull(result.Error.ValidationErrors);
        Assert.Contains("relations[0].relatedEmployeePublicId", result.Error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Handle_WhenRelatedPersonnelFileIsNotAnEmployee_ShouldReturnValidationError()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Employee, "Ana", "Owner");
        var candidateFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Luis", "Candidate");
        var repository = new TestPersonnelFileRepository(personnelFile, candidateFile);
        var handler = CreateHandler(repository);

        var result = await handler.Handle(
            new ReplacePersonnelFileEmployeeRelationsCommand(
                personnelFile.PublicId,
                [new EmployeeRelationInput(candidateFile.PublicId, "Sibling")],
                personnelFile.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.NotNull(result.Error.ValidationErrors);
        Assert.Contains("relations[0].relatedEmployeePublicId", result.Error.ValidationErrors!.Keys);
    }

    private static ReplacePersonnelFileEmployeeRelationsCommandHandler CreateHandler(TestPersonnelFileRepository repository)
    {
        return new ReplacePersonnelFileEmployeeRelationsCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
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
            nationality: null,
            personalEmail: null,
            institutionalEmail: null,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoUrl: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: recordType == PersonnelFileRecordType.Employee ? Guid.NewGuid() : null,
            customDataJson: null);
        file.SetTenantId(TenantId);
        SetEntityId(file, Random.Shared.NextInt64(1, long.MaxValue));
        return file;
    }

    private static void SetEntityId(PersonnelFile file, long value)
    {
        var property = typeof(PersonnelFile).BaseType?.BaseType?.GetProperty("Id");
        property?.SetValue(file, value);
    }

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
            file.PhotoUrl,
            file.OrgUnitPublicId,
            file.AssignedPositionSlotPublicId,
            file.LinkedUserPublicId,
            file.CustomDataJson,
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

    private sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AllowPersonnelFileAuthorizationService : IPersonnelFileAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Error TenantMismatch(RbacPermissionAction action) =>
            new("TENANT_MISMATCH", "Tenant mismatch.", ErrorType.Forbidden);
    }

    private sealed class TestPersonnelFileRepository(params PersonnelFile[] files) : IPersonnelFileRepository
    {
        private readonly Dictionary<Guid, PersonnelFile> _files = files.ToDictionary(file => file.PublicId);

        public int GetResponseByIdCalls { get; private set; }
        public int GetEmployeeRelationsCalls { get; private set; }

        public void Add(PersonnelFile personnelFile) => throw new NotSupportedException();

        public void AddCustomFieldDefinition(PersonnelFileCustomFieldDefinition definition) => throw new NotSupportedException();

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
                        file.PhotoUrl,
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
            return Task.FromResult(_files.TryGetValue(personnelFileId, out var file) ? ToResponse(file) : null);
        }

        public Task<PersonnelFilePersonalInfoResponse?> GetPersonalInfoAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileIdentificationResponse>> GetIdentificationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileAddressResponse>> GetAddressesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>> GetEmergencyContactsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>> GetFamilyMembersAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileHobbyResponse>> GetHobbiesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>> GetEmployeeRelationsAsync(Guid personnelFileId, CancellationToken cancellationToken)
        {
            GetEmployeeRelationsCalls++;

            if (!_files.TryGetValue(personnelFileId, out var file))
            {
                return Task.FromResult<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>(Array.Empty<PersonnelFileEmployeeRelationResponse>());
            }

            var response = file.EmployeeRelations
                .Select(relation =>
                {
                    var relatedEmployeePublicId = relation.RelatedPersonnelFileId is long relatedPersonnelFileId
                        ? _files.Values.FirstOrDefault(candidate => candidate.Id == relatedPersonnelFileId)?.PublicId ?? Guid.Empty
                        : Guid.Empty;
                    var relatedEmployeeFullName = relation.RelatedPersonnelFileId is long relatedId
                        ? _files.Values.FirstOrDefault(candidate => candidate.Id == relatedId)?.FullName ?? string.Empty
                        : string.Empty;

                    return new PersonnelFileEmployeeRelationResponse(
                        relation.PublicId,
                        relatedEmployeePublicId,
                        relatedEmployeeFullName,
                        relation.Relationship);
                })
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>(response);
        }

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

        public Task<IReadOnlyCollection<Guid>> GetBankAccountIdsAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());

        public Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetCatalogItemsAsync(Guid companyId, string category, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>> GetReferenceCatalogItemsAsync(Guid companyId, string category, string? parentCode, CancellationToken cancellationToken) => throw new NotSupportedException();

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

        public Task<IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>> GetCustomFieldDefinitionsAsync(Guid tenantId, bool? isActive, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileCustomFieldDefinition?> GetCustomFieldDefinitionByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> CustomFieldKeyExistsAsync(Guid tenantId, string normalizedKey, long? excludingId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Guid>> GetLinkedUserIdsByAssignedPositionSlotAsync(Guid tenantId, Guid assignedPositionSlotId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
