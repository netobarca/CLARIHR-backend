using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

public interface IPersonnelFileRepository
{
    void Add(PersonnelFile personnelFile);

    Task<int> CountActiveEmployeesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<PersonnelFile?> GetByIdAsync(Guid personnelFileId, CancellationToken cancellationToken);

    Task<PersonnelFile?> GetForAccessCheckAsync(Guid personnelFileId, CancellationToken cancellationToken);

    Task<PersonnelFile?> GetForProfileSectionUpdateAsync(
        Guid personnelFileId,
        PersonnelFileTrackedSection section,
        CancellationToken cancellationToken);

    Task<PersonnelFile?> GetByLinkedUserIdAsync(Guid tenantId, Guid linkedUserPublicId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid personnelFileId, CancellationToken cancellationToken);

    Task<bool> IdentificationExistsAsync(
        Guid tenantId,
        string identificationType,
        string normalizedIdentificationNumber,
        long? excludingPersonnelFileId,
        CancellationToken cancellationToken);

    Task<PagedResponse<PersonnelFileListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        PersonnelFileRecordType? recordType,
        Guid? orgUnitId,
        int? minAge,
        int? maxAge,
        string? maritalStatus,
        string? nationality,
        string? profession,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PersonnelFileShellResponse?> GetShellByIdAsync(Guid personnelFileId, CancellationToken cancellationToken);

    Task<PersonnelFileResponse?> GetResponseByIdAsync(Guid personnelFileId, CancellationToken cancellationToken);

    Task<PersonnelFilePersonalInfoResponse?> GetPersonalInfoAsync(Guid personnelFileId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileIdentificationResponse>> GetIdentificationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileIdentificationResponse?> GetIdentificationAsync(
        Guid personnelFileId,
        Guid identificationPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAddressResponse>> GetAddressesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileAddressResponse?> GetAddressAsync(
        Guid personnelFileId,
        Guid addressPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>> GetEmergencyContactsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileEmergencyContactResponse?> GetEmergencyContactAsync(
        Guid personnelFileId,
        Guid emergencyContactPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>> GetFamilyMembersAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileFamilyMemberResponse?> GetFamilyMemberAsync(
        Guid personnelFileId,
        Guid familyMemberPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileHobbyResponse>> GetHobbiesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileHobbyResponse?> GetHobbyAsync(
        Guid personnelFileId,
        Guid hobbyPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>> GetEmployeeRelationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileEmployeeRelationResponse?> GetEmployeeRelationAsync(
        Guid personnelFileId,
        Guid employeeRelationPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileBankAccountResponse>> GetBankAccountsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileBankAccountResponse?> GetBankAccountAsync(
        Guid personnelFileId,
        Guid bankAccountPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAssociationResponse>> GetAssociationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileAssociationResponse?> GetAssociationAsync(
        Guid personnelFileId,
        Guid associationPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileEducationResponse>> GetEducationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileEducationResponse?> GetEducationAsync(
        Guid personnelFileId,
        Guid educationPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileLanguageResponse>> GetLanguagesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileLanguageResponse?> GetLanguageAsync(
        Guid personnelFileId,
        Guid languagePublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileTrainingResponse>> GetTrainingsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileTrainingResponse?> GetTrainingAsync(
        Guid personnelFileId,
        Guid trainingPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>> GetPreviousEmploymentsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFilePreviousEmploymentResponse?> GetPreviousEmploymentAsync(
        Guid personnelFileId,
        Guid previousEmploymentPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileReferenceResponse>> GetReferencesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileReferenceResponse?> GetReferenceAsync(
        Guid personnelFileId,
        Guid referencePublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>> GetDocumentsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileDocumentMetadataResponse?> GetDocumentMetadataByIdAsync(
        Guid personnelFileId,
        Guid documentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileObservationResponse>> GetObservationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetBankAccountIdsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetCatalogItemsAsync(
        string? countryCode,
        string category,
        CancellationToken cancellationToken);

    // Default no-op so test doubles need not implement it; the production repository overrides it with
    // the real country-scoped query.
    Task<IReadOnlyCollection<CompensationConceptTypeResponse>> GetCompensationConceptTypesAsync(
        string? countryCode,
        CompensationNature? nature,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<CompensationConceptTypeResponse>>([]);

    // Default no-op for the same reason: the enriched settlement-concept catalog (settlement module D-07)
    // is only queried by its dedicated read endpoint.
    Task<IReadOnlyCollection<SettlementConceptResponse>> GetSettlementConceptsAsync(
        string? countryCode,
        CLARIHR.Domain.PersonnelFiles.SettlementConceptClass? conceptClass,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SettlementConceptResponse>>([]);

    /// <summary>
    /// Normalized codes of the concept types flagged as base salary (IsBaseSalary, D-12/DP-08) for the
    /// company's country. The single-active-base-salary rule matches concepts against these codes,
    /// falling back to the legacy <c>SALARIO_BASE</c> constant when the catalog carries no flag.
    /// Empty default so test doubles need not implement it.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetBaseSalaryConceptTypeCodesAsync(
        Guid companyId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<string>>([]);

    // Enriched contract-type read (RF-011): abbreviation + IsTemporary. Empty default so test doubles
    // need not implement it; the production repository overrides it with the real country-scoped query.
    Task<IReadOnlyCollection<ContractTypeResponse>> GetContractTypesAsync(
        string? countryCode,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<ContractTypeResponse>>([]);

    /// <summary>
    /// Anchored regex configured for the identification type (RF-003), for the company's country;
    /// null when the type has no format (generic validation applies). No-op default (no format) so
    /// test doubles need not implement it.
    /// </summary>
    Task<string?> GetIdentificationTypeNumberFormatAsync(
        Guid companyId,
        string identificationTypeCode,
        CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    // Enriched AFP master read (RF-007): identity/contact columns. Empty default so test doubles
    // need not implement it; the production repository overrides it with the real country-scoped query.
    Task<IReadOnlyCollection<AfpResponse>> GetAfpsAsync(
        string? countryCode,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<AfpResponse>>([]);

    Task<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>> GetReferenceCatalogItemsAsync(
        string countryCode,
        string category,
        string? parentCode,
        CancellationToken cancellationToken);

    Task<string?> GetCompanyCountryCodeAsync(Guid companyId, CancellationToken cancellationToken);

    Task<bool> CatalogCodeIsActiveAsync(
        Guid companyId,
        string category,
        string code,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the display name of a country-scoped general catalog item by code, for the company's country,
    /// so a write handler can persist a name snapshot (e.g. the off-payroll transaction type description —
    /// RN-09). Returns null when the code is not found. Only categories that require a snapshot are wired.
    /// Fail-safe default (no snapshot) so the many hand-written test doubles need not implement it; the
    /// production repository overrides it with the real country-scoped name lookup.
    /// </summary>
    Task<string?> GetCatalogItemNameAsync(
        Guid companyId,
        string category,
        string code,
        CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    /// <summary>
    /// Resolves the display names of a retirement category + reason pair (country-scoped reference catalogs)
    /// so the retirement-request write handlers can persist name snapshots (D-02 of the retirement module).
    /// Fail-safe default (no snapshot) so hand-written test doubles need not implement it; the production
    /// repository overrides it with the real country-scoped lookup.
    /// </summary>
    Task<(string? CategoryName, string? ReasonName)> GetRetirementCatalogNamesAsync(
        Guid companyId,
        string retirementCategoryCode,
        string retirementReasonCode,
        CancellationToken cancellationToken) =>
        Task.FromResult<(string?, string?)>((null, null));

    /// <summary>
    /// Resolves the display name of an ACTIVE income compensation-concept type (Nature = Ingreso) by code for the
    /// company's country, so the recurring-income write handlers can snapshot the concept name (REQ-005). Returns
    /// null when the code is not found, is inactive, or is not an income concept — the handler then rejects it
    /// (<c>RECURRING_INCOME_CONCEPT_INVALID</c>). Fail-safe default (null) so hand-written test doubles need not
    /// implement it; the production repository overrides it.
    /// </summary>
    Task<string?> GetActiveIncomeConceptNameAsync(
        Guid companyId,
        string conceptTypeCode,
        CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    Task<bool> CountryCodeIsActiveAsync(string countryCode, CancellationToken cancellationToken);

    Task<bool> ReferenceCatalogCodeIsActiveAsync(
        string countryCode,
        string category,
        string code,
        CancellationToken cancellationToken);

    Task<bool> ReferenceMunicipalityBelongsToDepartmentAsync(
        string countryCode,
        string departmentCode,
        string municipalityCode,
        CancellationToken cancellationToken);

    Task<bool> ReferenceInsuranceRangeBelongsToTypeAsync(
        string countryCode,
        string insuranceTypeCode,
        string insuranceRangeCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// True when the retirement reason code belongs to the given retirement category (both active) for
    /// the country. Mirrors <see cref="ReferenceInsuranceRangeBelongsToTypeAsync"/>. Default returns false
    /// so the many hand-written test doubles need not implement it; the production repository overrides it.
    /// </summary>
    Task<bool> ReferenceRetirementReasonBelongsToCategoryAsync(
        string countryCode,
        string retirementCategoryCode,
        string retirementReasonCode,
        CancellationToken cancellationToken) =>
        Task.FromResult(false);

    Task<PersonnelFileDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken);

    Task<bool> DocumentExistsOutsideTenantAsync(Guid documentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileExportRow>> GetExportRowsAsync(
        Guid tenantId,
        bool? isActive,
        PersonnelFileRecordType? recordType,
        Guid? orgUnitId,
        int? minAge,
        int? maxAge,
        string? maritalStatus,
        string? nationality,
        string? profession,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int? maxRows,
        CancellationToken cancellationToken);

    Task<PersonnelFileDynamicQueryResponse> DynamicQueryAsync(
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileDynamicFilterInput> filters,
        IReadOnlyCollection<string> groupBy,
        IReadOnlyCollection<PersonnelFileDynamicSortInput> sort,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PersonnelFileAnalyticsSummaryResponse> GetAnalyticsSummaryAsync(
        Guid tenantId,
        bool? isActive,
        PersonnelFileRecordType? recordType,
        Guid? orgUnitId,
        int? minAge,
        int? maxAge,
        string? search,
        CancellationToken cancellationToken);



    Task<IReadOnlyCollection<Guid>> GetLinkedUserIdsByAssignedPositionSlotAsync(
        Guid tenantId,
        Guid assignedPositionSlotId,
        CancellationToken cancellationToken);
}
