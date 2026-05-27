using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
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
        Guid documentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileObservationResponse>> GetObservationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetBankAccountIdsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetCatalogItemsAsync(
        Guid companyId,
        string category,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>> GetReferenceCatalogItemsAsync(
        Guid companyId,
        string category,
        string? parentCode,
        CancellationToken cancellationToken);

    Task<string?> GetCompanyCountryCodeAsync(Guid companyId, CancellationToken cancellationToken);

    Task<bool> CatalogCodeIsActiveAsync(
        Guid companyId,
        string category,
        string code,
        CancellationToken cancellationToken);

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
