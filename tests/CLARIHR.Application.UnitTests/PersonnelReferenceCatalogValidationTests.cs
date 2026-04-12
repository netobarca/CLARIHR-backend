using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

public sealed class PersonnelReferenceCatalogValidationTests
{
    [Fact]
    public async Task ValidatePersonalInfoCodesAsync_WhenCodesAreActiveAndHierarchyIsValid_ShouldReturnNone()
    {
        var repository = new TestPersonnelFileRepository();
        repository.AddActiveCountry("SV");
        repository.AddActiveReferenceCode("SV", PersonnelReferenceCatalogCategories.MaritalStatus, "SOLTERO_A");
        repository.AddActiveReferenceCode("SV", PersonnelReferenceCatalogCategories.Profession, "ANALISTA_DE_DATOS");
        repository.AddActiveReferenceCode("SV", PersonnelReferenceCatalogCategories.Department, "SAN_SALVADOR");
        repository.AddActiveReferenceCode("SV", PersonnelReferenceCatalogCategories.Municipality, "SAN_SALVADOR_CENTRO");
        repository.AddMunicipalityDepartment("SV", "SAN_SALVADOR", "SAN_SALVADOR_CENTRO");

        var error = await PersonnelReferenceCatalogValidation.ValidatePersonalInfoCodesAsync(
            repository,
            maritalStatusCode: "SOLTERO_A",
            professionCode: "ANALISTA_DE_DATOS",
            birthCountryCode: "SV",
            birthDepartmentCode: "SAN_SALVADOR",
            birthMunicipalityCode: "SAN_SALVADOR_CENTRO",
            CancellationToken.None);

        Assert.Equal(Error.None, error);
    }

    [Fact]
    public async Task ValidatePersonalInfoCodesAsync_WhenProfessionCodeIsInactive_ShouldReturnValidationError()
    {
        var repository = new TestPersonnelFileRepository();
        repository.AddActiveCountry("SV");
        repository.AddActiveReferenceCode("SV", PersonnelReferenceCatalogCategories.MaritalStatus, "SOLTERO_A");

        var error = await PersonnelReferenceCatalogValidation.ValidatePersonalInfoCodesAsync(
            repository,
            maritalStatusCode: "SOLTERO_A",
            professionCode: "INVALID_PROFESSION",
            birthCountryCode: null,
            birthDepartmentCode: null,
            birthMunicipalityCode: null,
            CancellationToken.None);

        Assert.Equal("common.validation", error.Code);
        Assert.NotNull(error.ValidationErrors);
        Assert.Contains("professionCode", error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task ValidatePersonalInfoCodesAsync_WhenDepartmentProvidedWithoutCountry_ShouldReturnValidationError()
    {
        var repository = new TestPersonnelFileRepository();

        var error = await PersonnelReferenceCatalogValidation.ValidatePersonalInfoCodesAsync(
            repository,
            maritalStatusCode: null,
            professionCode: null,
            birthCountryCode: null,
            birthDepartmentCode: "SAN_SALVADOR",
            birthMunicipalityCode: null,
            CancellationToken.None);

        Assert.Equal("common.validation", error.Code);
        Assert.NotNull(error.ValidationErrors);
        Assert.Contains("birthCountryCode", error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task ValidatePersonalInfoCodesAsync_WhenMunicipalityDoesNotBelongToDepartment_ShouldReturnValidationError()
    {
        var repository = new TestPersonnelFileRepository();
        repository.AddActiveCountry("SV");
        repository.AddActiveReferenceCode("SV", PersonnelReferenceCatalogCategories.Department, "SAN_SALVADOR");
        repository.AddActiveReferenceCode("SV", PersonnelReferenceCatalogCategories.Municipality, "LA_LIBERTAD_SUR");

        var error = await PersonnelReferenceCatalogValidation.ValidatePersonalInfoCodesAsync(
            repository,
            maritalStatusCode: null,
            professionCode: null,
            birthCountryCode: "SV",
            birthDepartmentCode: "SAN_SALVADOR",
            birthMunicipalityCode: "LA_LIBERTAD_SUR",
            CancellationToken.None);

        Assert.Equal("common.validation", error.Code);
        Assert.NotNull(error.ValidationErrors);
        Assert.Contains("birthMunicipalityCode", error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task ValidateIdentificationTypeCodeAsync_WhenCodeIsInactive_ShouldReturnValidationError()
    {
        var repository = new TestPersonnelFileRepository();

        var error = await PersonnelReferenceCatalogValidation.ValidateIdentificationTypeCodeAsync(
            repository,
            "UNKNOWN_DOCUMENT",
            CancellationToken.None);

        Assert.Equal("common.validation", error.Code);
        Assert.NotNull(error.ValidationErrors);
        Assert.Contains("identificationTypeCode", error.ValidationErrors!.Keys);
    }

    private sealed class TestPersonnelFileRepository : IPersonnelFileRepository
    {
        private readonly HashSet<string> _activeCountries = new(StringComparer.Ordinal);
        private readonly HashSet<string> _activeReferenceCodes = new(StringComparer.Ordinal);
        private readonly HashSet<string> _municipalityDepartmentLinks = new(StringComparer.Ordinal);

        public void AddActiveCountry(string countryCode) => _activeCountries.Add(Normalize(countryCode));

        public void AddActiveReferenceCode(string countryCode, string category, string code) =>
            _activeReferenceCodes.Add($"{Normalize(countryCode)}|{Normalize(category)}|{Normalize(code)}");

        public void AddMunicipalityDepartment(string countryCode, string departmentCode, string municipalityCode) =>
            _municipalityDepartmentLinks.Add($"{Normalize(countryCode)}|{Normalize(departmentCode)}|{Normalize(municipalityCode)}");

        public Task<bool> CountryCodeIsActiveAsync(string countryCode, CancellationToken cancellationToken) =>
            Task.FromResult(_activeCountries.Contains(Normalize(countryCode)));

        public Task<bool> ReferenceCatalogCodeIsActiveAsync(
            string countryCode,
            string category,
            string code,
            CancellationToken cancellationToken) =>
            Task.FromResult(_activeReferenceCodes.Contains($"{Normalize(countryCode)}|{Normalize(category)}|{Normalize(code)}"));

        public Task<bool> ReferenceMunicipalityBelongsToDepartmentAsync(
            string countryCode,
            string departmentCode,
            string municipalityCode,
            CancellationToken cancellationToken) =>
            Task.FromResult(_municipalityDepartmentLinks.Contains($"{Normalize(countryCode)}|{Normalize(departmentCode)}|{Normalize(municipalityCode)}"));

        public void Add(PersonnelFile personnelFile) => throw new NotSupportedException();
        public void AddCustomFieldDefinition(PersonnelFileCustomFieldDefinition definition) => throw new NotSupportedException();
        public Task<int> CountActiveEmployeesAsync(Guid tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFile?> GetByIdAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFile?> GetByLinkedUserIdAsync(Guid tenantId, Guid linkedUserPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsOutsideTenantAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> IdentificationExistsAsync(Guid tenantId, string identificationType, string normalizedIdentificationNumber, long? excludingPersonnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedResponse<PersonnelFileListItemResponse>> SearchAsync(Guid tenantId, bool? isActive, PersonnelFileRecordType? recordType, Guid? orgUnitId, int? minAge, int? maxAge, string? maritalStatus, string? nationality, string? profession, DateTime? createdFromUtc, DateTime? createdToUtc, string? search, string? sortBy, PersonnelFileSortDirection sortDirection, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileResponse?> GetResponseByIdAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFilePersonalInfoResponse?> GetPersonalInfoAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileIdentificationResponse>> GetIdentificationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileAddressResponse>> GetAddressesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>> GetEmergencyContactsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>> GetFamilyMembersAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileHobbyResponse>> GetHobbiesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>> GetEmployeeRelationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileAssociationResponse>> GetAssociationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileEducationResponse>> GetEducationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileLanguageResponse>> GetLanguagesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileTrainingResponse>> GetTrainingsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>> GetPreviousEmploymentsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileReferenceResponse>> GetReferencesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>> GetDocumentsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetCatalogItemsAsync(Guid tenantId, string category, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>> GetReferenceCatalogItemsAsync(string countryCode, string category, string? parentCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> CatalogCodeIsActiveAsync(Guid tenantId, string category, string code, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileDocumentDownloadResponse?> GetDocumentDownloadByIdAsync(Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DocumentExistsOutsideTenantAsync(Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileExportRow>> GetExportRowsAsync(Guid tenantId, bool? isActive, PersonnelFileRecordType? recordType, Guid? orgUnitId, int? minAge, int? maxAge, string? maritalStatus, string? nationality, string? profession, DateTime? createdFromUtc, DateTime? createdToUtc, string? search, string? sortBy, PersonnelFileSortDirection sortDirection, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileDynamicQueryResponse> DynamicQueryAsync(Guid tenantId, IReadOnlyCollection<PersonnelFileDynamicFilterInput> filters, IReadOnlyCollection<string> groupBy, IReadOnlyCollection<PersonnelFileDynamicSortInput> sort, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileAnalyticsSummaryResponse> GetAnalyticsSummaryAsync(Guid tenantId, bool? isActive, PersonnelFileRecordType? recordType, Guid? orgUnitId, int? minAge, int? maxAge, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>> GetCustomFieldDefinitionsAsync(Guid tenantId, bool? isActive, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileCustomFieldDefinition?> GetCustomFieldDefinitionByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> CustomFieldKeyExistsAsync(Guid tenantId, string normalizedKey, long? excludingId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> GetLinkedUserIdsByAssignedPositionSlotAsync(Guid tenantId, Guid assignedPositionSlotId, CancellationToken cancellationToken) => throw new NotSupportedException();

        private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    }
}
