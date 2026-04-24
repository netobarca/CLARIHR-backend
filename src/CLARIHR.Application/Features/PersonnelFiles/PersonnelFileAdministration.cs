using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.PersonnelEducationCatalogs;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelEducationCatalogs;
using CLARIHR.Application.Features.PersonnelEducationCatalogs.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileListItemResponse(
    Guid Id,
    Guid CompanyId,
    PersonnelFileRecordType RecordType,
    PersonnelFileLifecycleStatus LifecycleStatus,
    string FullName,
    DateTime BirthDate,
    int Age,
    string? MaritalStatusCode,
    string? MaritalStatusName,
    string? ProfessionCode,
    string? ProfessionName,
    Guid? OrgUnitId,
    Guid? AssignedPositionSlotId,
    Guid? LinkedUserId,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record PersonnelFileIdentificationResponse(
    Guid Id,
    string IdentificationTypeCode,
    string? IdentificationTypeName,
    string IdentificationNumber,
    DateTime? IssuedDate,
    DateTime? ExpiryDate,
    string? Issuer,
    bool IsPrimary);

public sealed record PersonnelReferenceValueResponse(
    string Code,
    string Name);

public sealed record PersonnelEducationCatalogReferenceResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsActive);

public sealed record PersonnelFileAddressResponse(
    Guid Id,
    string AddressLine,
    string? Country,
    string? Department,
    string? Municipality,
    string? PostalCode,
    bool IsCurrent);

public sealed record PersonnelFileEmergencyContactResponse(
    Guid Id,
    string Name,
    string Relationship,
    string Phone,
    string? Address,
    string? Workplace);

public sealed record PersonnelFileFamilyMemberResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string KinshipCode,
    string? Nationality,
    DateTime? BirthDate,
    PersonnelFamilyMemberSex Sex,
    string? MaritalStatus,
    string? Occupation,
    string? DocumentType,
    string? DocumentNumber,
    string? Phone,
    bool IsStudying,
    string? StudyPlace,
    string? AcademicLevel,
    bool IsBeneficiary,
    bool IsWorking,
    string? Workplace,
    string? JobTitle,
    string? WorkPhone,
    decimal? Salary,
    bool IsDeceased,
    DateTime? DeceasedDate);

public sealed record PersonnelFileHobbyResponse(Guid Id, string HobbyName);

public sealed record PersonnelFileEmployeeRelationResponse(
    Guid Id,
    Guid RelatedEmployeePublicId,
    string RelatedEmployeeFullName,
    string Relationship);

public sealed record PersonnelFileBankAccountResponse(
    Guid Id,
    string BankCode,
    string CurrencyCode,
    string AccountNumber,
    string AccountTypeCode,
    bool IsPrimary);

public sealed record PersonnelFileAssociationResponse(
    Guid Id,
    string AssociationName,
    string? Role,
    DateTime? JoinedDate,
    DateTime? LeftDate,
    decimal? Payment);

public sealed record PersonnelFileEducationResponse(
    Guid Id,
    PersonnelEducationCatalogReferenceResponse Status,
    string? DegreeTitle,
    PersonnelEducationCatalogReferenceResponse StudyType,
    PersonnelEducationCatalogReferenceResponse Career,
    string Institution,
    string CountryCode,
    string? Specialty,
    bool IsCurrentlyStudying,
    DateTime StartDate,
    DateTime? EndDate,
    PersonnelEducationCatalogReferenceResponse? Shift,
    PersonnelEducationCatalogReferenceResponse? Modality,
    int? TotalSubjects,
    int? ApprovedSubjects);

public sealed record PersonnelFileLanguageResponse(
    Guid Id,
    string LanguageCode,
    string LevelCode,
    bool Speaks,
    bool Writes,
    bool Reads);

public sealed record PersonnelFileTrainingResponse(
    Guid Id,
    string TrainingName,
    string TrainingTypeCode,
    string? Description,
    string? Topic,
    string? Institution,
    string? Instructors,
    decimal? Score,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsInternal,
    bool IsLocal,
    string CountryCode,
    decimal DurationValue,
    string DurationUnitCode,
    decimal? CostAmount,
    string? CostCurrencyCode);

public sealed record PersonnelFilePreviousEmploymentResponse(
    Guid Id,
    string Institution,
    string? Place,
    string? LastPosition,
    string? ManagerName,
    DateTime EntryDate,
    DateTime? RetirementDate,
    string? CompanyPhone,
    string? ExitReason,
    decimal? FirstSalaryAmount,
    decimal? LastSalaryAmount,
    decimal? AverageCommissionAmount,
    string CurrencyCode);

public sealed record PersonnelFileReferenceResponse(
    Guid Id,
    string PersonName,
    string? Address,
    string Phone,
    string ReferenceTypeCode,
    string? Occupation,
    string? Workplace,
    string? WorkPhone,
    decimal KnownTimeYears);

public sealed record PersonnelFileDocumentMetadataResponse(
    Guid Id,
    string DocumentType,
    string? Observations,
    DateTime? DeliveryDate,
    DateTime? LoanDate,
    DateTime? ReturnDate,
    string FileName,
    string ContentType,
    int SizeBytes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PersonnelFileObservationResponse(
    Guid Id,
    Guid AuthorUserId,
    string Note,
    DateTime CreatedAtUtc);

public sealed record PersonnelFilePersonalInfoResponse(
    Guid Id,
    Guid CompanyId,
    PersonnelFileRecordType RecordType,
    PersonnelFileLifecycleStatus LifecycleStatus,
    string FirstName,
    string LastName,
    string FullName,
    DateTime BirthDate,
    int Age,
    string? MaritalStatusCode,
    string? MaritalStatusName,
    string? ProfessionCode,
    string? ProfessionName,
    string? Nationality,
    string? PersonalEmail,
    string? InstitutionalEmail,
    string? PersonalPhone,
    string? InstitutionalPhone,
    string? BirthCountryCode,
    string? BirthCountryName,
    string? BirthDepartmentCode,
    string? BirthDepartmentName,
    string? BirthMunicipalityCode,
    string? BirthMunicipalityName,
    string? PhotoUrl,
    Guid? OrgUnitId,
    Guid? AssignedPositionSlotId,
    Guid? LinkedUserId,
    string? CustomDataJson,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PersonnelFileResponse(
    Guid Id,
    Guid CompanyId,
    PersonnelFileRecordType RecordType,
    PersonnelFileLifecycleStatus LifecycleStatus,
    string FirstName,
    string LastName,
    string FullName,
    DateTime BirthDate,
    int Age,
    string? MaritalStatusCode,
    string? MaritalStatusName,
    string? ProfessionCode,
    string? ProfessionName,
    string? Nationality,
    string? PersonalEmail,
    string? InstitutionalEmail,
    string? PersonalPhone,
    string? InstitutionalPhone,
    string? BirthCountryCode,
    string? BirthCountryName,
    string? BirthDepartmentCode,
    string? BirthDepartmentName,
    string? BirthMunicipalityCode,
    string? BirthMunicipalityName,
    string? PhotoUrl,
    Guid? OrgUnitId,
    Guid? AssignedPositionSlotId,
    Guid? LinkedUserId,
    string? CustomDataJson,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    IReadOnlyCollection<PersonnelFileIdentificationResponse> Identifications,
    IReadOnlyCollection<PersonnelFileAddressResponse> Addresses,
    IReadOnlyCollection<PersonnelFileEmergencyContactResponse> EmergencyContacts,
    IReadOnlyCollection<PersonnelFileFamilyMemberResponse> FamilyMembers,
    IReadOnlyCollection<PersonnelFileHobbyResponse> Hobbies,
    IReadOnlyCollection<PersonnelFileEmployeeRelationResponse> EmployeeRelations,
    IReadOnlyCollection<PersonnelFileBankAccountResponse> BankAccounts,
    IReadOnlyCollection<PersonnelFileAssociationResponse> Associations,
    IReadOnlyCollection<PersonnelFileEducationResponse> Educations,
    IReadOnlyCollection<PersonnelFileLanguageResponse> Languages,
    IReadOnlyCollection<PersonnelFileTrainingResponse> Trainings,
    IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse> PreviousEmployments,
    IReadOnlyCollection<PersonnelFileReferenceResponse> References,
    IReadOnlyCollection<PersonnelFileDocumentMetadataResponse> Documents,
    IReadOnlyCollection<PersonnelFileObservationResponse> Observations,
    AllowedActionsResponse? AllowedActions = null);

public sealed record PersonnelCatalogItemResponse(
    Guid Id,
    string Category,
    string Code,
    string Name,
    bool IsSystem,
    bool IsActive,
    int SortOrder);

public sealed record PersonnelReferenceCatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    int SortOrder);

public sealed record PersonnelFileDocumentDownloadResponse(
    Guid Id,
    Guid PersonnelFileId,
    string FileName,
    string ContentType,
    byte[] FileData,
    Guid ConcurrencyToken,
    bool IsActive);

public sealed record PersonnelFileExportRow(
    Guid Id,
    PersonnelFileRecordType RecordType,
    PersonnelFileLifecycleStatus LifecycleStatus,
    string FirstName,
    string LastName,
    string FullName,
    DateTime BirthDate,
    int Age,
    string? MaritalStatus,
    string? Profession,
    string? Nationality,
    string? PersonalEmail,
    string? InstitutionalEmail,
    string? PersonalPhone,
    string? InstitutionalPhone,
    Guid? OrgUnitId,
    Guid? AssignedPositionSlotId,
    Guid? LinkedUserId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PersonnelFileAnalyticsBreakdownResponse(string Key, string Label, int Count);

public sealed record PersonnelFileAnalyticsSummaryResponse(
    int TotalCount,
    int ActiveCount,
    int InactiveCount,
    IReadOnlyCollection<PersonnelFileAnalyticsBreakdownResponse> ByRecordType,
    IReadOnlyCollection<PersonnelFileAnalyticsBreakdownResponse> ByAgeRange,
    IReadOnlyCollection<PersonnelFileAnalyticsBreakdownResponse> ByOrgUnit);

public enum PersonnelFileSortDirection
{
    Asc = 1,
    Desc = 2
}

public sealed record PersonnelFilePrintResponse(
    DateTime GeneratedAtUtc,
    IReadOnlyCollection<string> IncludedSections,
    PersonnelFileResponse PersonnelFile);

public sealed record PersonnelFileDynamicFilterInput(
    string Field,
    string Operator,
    string? Value,
    string? ValueTo,
    IReadOnlyCollection<string>? Values);

public sealed record PersonnelFileDynamicSortInput(string Field, PersonnelFileSortDirection Direction = PersonnelFileSortDirection.Asc);

public sealed record PersonnelFileDynamicGroupBucketResponse(string Key, string Label, int Count);

public sealed record PersonnelFileDynamicGroupResponse(
    string Field,
    IReadOnlyCollection<PersonnelFileDynamicGroupBucketResponse> Buckets);

public sealed record PersonnelFileDynamicQueryResponse(
    IReadOnlyCollection<PersonnelFileListItemResponse> Items,
    IReadOnlyCollection<PersonnelFileDynamicGroupResponse> Groups,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record PersonnelCustomFieldDefinitionResponse(
    Guid Id,
    Guid CompanyId,
    string Key,
    string Label,
    PersonnelCustomFieldType FieldType,
    bool IsRequired,
    bool IsActive,
    string? OptionsJson,
    int SortOrder,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record SearchPersonnelFilesQuery(
    Guid CompanyId,
    bool? IsActive,
    PersonnelFileRecordType? RecordType,
    Guid? OrgUnitId,
    int? MinAge,
    int? MaxAge,
    string? MaritalStatus,
    string? Nationality,
    string? Profession,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    string? Search,
    string? SortBy = null,
    PersonnelFileSortDirection SortDirection = PersonnelFileSortDirection.Asc,
    int PageNumber = 1,
    int PageSize = PersonnelFileValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<PersonnelFileListItemResponse>>;

public sealed record GetPersonnelFileByIdQuery(Guid PersonnelFileId) : IQuery<PersonnelFileResponse>;

public sealed record GetPersonnelFilePersonalInfoQuery(Guid PersonnelFileId) : IQuery<PersonnelFilePersonalInfoResponse>;

public sealed record GetPersonnelFileIdentificationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileIdentificationResponse>>;

public sealed record GetPersonnelFileAddressesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileAddressResponse>>;

public sealed record GetPersonnelFileEmergencyContactsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>;

public sealed record GetPersonnelFileFamilyMembersQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>;

public sealed record GetPersonnelFileHobbiesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileHobbyResponse>>;

public sealed record GetPersonnelFileEmployeeRelationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>;

public sealed record GetPersonnelFileAssociationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileAssociationResponse>>;

public sealed record GetPersonnelFileEducationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileEducationResponse>>;

public sealed record GetPersonnelFileLanguagesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileLanguageResponse>>;

public sealed record GetPersonnelFileTrainingsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileTrainingResponse>>;

public sealed record GetPersonnelFilePreviousEmploymentsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>;

public sealed record GetPersonnelFileReferencesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileReferenceResponse>>;

public sealed record GetPersonnelFileDocumentsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>;

public sealed record GetPersonnelFilePrintQuery(
    Guid PersonnelFileId,
    IReadOnlyCollection<string>? Sections) : IQuery<PersonnelFilePrintResponse>;

public sealed record GetPersonnelCatalogItemsQuery(Guid CompanyId, string Category) : IQuery<IReadOnlyCollection<PersonnelCatalogItemResponse>>;

public sealed record GetPersonnelReferenceCatalogItemsQuery(
    Guid CompanyId,
    string Category,
    string? ParentCode = null) : IQuery<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>;

public sealed record ExportPersonnelFilesQuery(
    Guid CompanyId,
    bool? IsActive,
    PersonnelFileRecordType? RecordType,
    Guid? OrgUnitId,
    int? MinAge,
    int? MaxAge,
    string? MaritalStatus,
    string? Nationality,
    string? Profession,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    string? Search,
    string? SortBy = null,
    PersonnelFileSortDirection SortDirection = PersonnelFileSortDirection.Asc,
    int? MaxRows = null)
    : IQuery<IReadOnlyCollection<PersonnelFileExportRow>>;

public sealed record DynamicQueryPersonnelFilesQuery(
    Guid CompanyId,
    IReadOnlyCollection<PersonnelFileDynamicFilterInput> Filters,
    IReadOnlyCollection<string> GroupBy,
    IReadOnlyCollection<PersonnelFileDynamicSortInput> Sort,
    string? Search,
    int PageNumber = 1,
    int PageSize = PersonnelFileValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PersonnelFileDynamicQueryResponse>;

public sealed record GetPersonnelFilesAnalyticsSummaryQuery(
    Guid CompanyId,
    bool? IsActive,
    PersonnelFileRecordType? RecordType,
    Guid? OrgUnitId,
    int? MinAge,
    int? MaxAge,
    string? Search)
    : IQuery<PersonnelFileAnalyticsSummaryResponse>;

public sealed record GetPersonnelFileDocumentDownloadQuery(Guid DocumentId) : IQuery<PersonnelFileDocumentDownloadResponse>;

public sealed record GetPersonnelCustomFieldDefinitionsQuery(Guid CompanyId, bool? IsActive) : IQuery<IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>>;

public sealed record CreatePersonnelFileCommand(
    Guid CompanyId,
    PersonnelFileRecordType RecordType,
    string FirstName,
    string LastName,
    DateTime BirthDate,
    string? MaritalStatusCode,
    string? ProfessionCode,
    string? Nationality,
    string? PersonalEmail,
    string? InstitutionalEmail,
    string? PersonalPhone,
    string? InstitutionalPhone,
    string? BirthCountryCode,
    string? BirthDepartmentCode,
    string? BirthMunicipalityCode,
    string? PhotoUrl,
    Guid? OrgUnitId,
    Guid? AssignedPositionSlotId,
    string? CustomDataJson,
    IReadOnlyCollection<IdentificationInput> Identifications)
    : ICommand<PersonnelFileResponse>;

public sealed record UpdatePersonnelFilePersonalInfoCommand(
    Guid PersonnelFileId,
    PersonnelFileRecordType RecordType,
    string FirstName,
    string LastName,
    DateTime BirthDate,
    string? MaritalStatusCode,
    string? ProfessionCode,
    string? Nationality,
    string? PersonalEmail,
    string? InstitutionalEmail,
    string? PersonalPhone,
    string? InstitutionalPhone,
    string? BirthCountryCode,
    string? BirthDepartmentCode,
    string? BirthMunicipalityCode,
    string? PhotoUrl,
    Guid? OrgUnitId,
    Guid? AssignedPositionSlotId,
    string? CustomDataJson,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileIdentificationsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<IdentificationInput> Identifications,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileAddressesCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<AddressInput> Addresses,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileEmergencyContactsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<EmergencyContactInput> Contacts,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileFamilyMembersCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<FamilyMemberInput> FamilyMembers,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileHobbiesCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<HobbyInput> Hobbies,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileEmployeeRelationsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<EmployeeRelationInput> Relations,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileBankAccountsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<BankAccountInput> BankAccounts,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileAssociationsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<AssociationInput> Associations,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileEducationsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<EducationInput> Educations,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileLanguagesCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<LanguageInput> Languages,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileTrainingsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<TrainingInput> Trainings,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFilePreviousEmploymentsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<PreviousEmploymentInput> PreviousEmployments,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ReplacePersonnelFileReferencesCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<ReferenceInput> References,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record ActivatePersonnelFileCommand(Guid PersonnelFileId, Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record InactivatePersonnelFileCommand(Guid PersonnelFileId, Guid ConcurrencyToken)
    : ICommand<PersonnelFileResponse>;

public sealed record UploadPersonnelFileDocumentCommand(
    Guid PersonnelFileId,
    string DocumentType,
    string? Observations,
    DateTime? DeliveryDate,
    DateTime? LoanDate,
    DateTime? ReturnDate,
    string FileName,
    string ContentType,
    byte[] FileData,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileDocumentMetadataResponse>;

public sealed record InactivatePersonnelFileDocumentCommand(Guid DocumentId, Guid ConcurrencyToken)
    : ICommand<PersonnelFileDocumentMetadataResponse>;

public sealed record AddPersonnelFileObservationCommand(Guid PersonnelFileId, string Note, Guid ConcurrencyToken)
    : ICommand<PersonnelFileObservationResponse>;

public sealed record CreatePersonnelCustomFieldDefinitionCommand(
    Guid CompanyId,
    string Key,
    string Label,
    PersonnelCustomFieldType FieldType,
    bool IsRequired,
    bool IsActive,
    string? OptionsJson,
    int SortOrder)
    : ICommand<PersonnelCustomFieldDefinitionResponse>;

public sealed record UpdatePersonnelCustomFieldDefinitionCommand(
    Guid DefinitionId,
    string Key,
    string Label,
    PersonnelCustomFieldType FieldType,
    bool IsRequired,
    bool IsActive,
    string? OptionsJson,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<PersonnelCustomFieldDefinitionResponse>;

public sealed record IdentificationInput(
    string IdentificationTypeCode,
    string IdentificationNumber,
    DateTime? IssuedDate,
    DateTime? ExpiryDate,
    string? Issuer,
    bool IsPrimary = false);

public sealed record AddressInput(
    string AddressLine,
    string? Country,
    string? Department,
    string? Municipality,
    string? PostalCode,
    bool IsCurrent = false);

public sealed record EmergencyContactInput(
    string Name,
    string Relationship,
    string Phone,
    string? Address,
    string? Workplace);

public sealed record FamilyMemberInput(
    string FirstName,
    string LastName,
    string KinshipCode,
    string? Nationality,
    DateTime? BirthDate,
    PersonnelFamilyMemberSex Sex,
    string? MaritalStatus,
    string? Occupation,
    string? DocumentType,
    string? DocumentNumber,
    string? Phone,
    bool IsStudying,
    string? StudyPlace,
    string? AcademicLevel,
    bool IsBeneficiary,
    bool IsWorking,
    string? Workplace,
    string? JobTitle,
    string? WorkPhone,
    decimal? Salary,
    bool IsDeceased,
    DateTime? DeceasedDate);

public sealed record HobbyInput(string HobbyName);

public sealed record EmployeeRelationInput(Guid RelatedEmployeePublicId, string Relationship);

public sealed record BankAccountInput(
    string BankCode,
    string CurrencyCode,
    string AccountNumber,
    string AccountTypeCode,
    bool IsPrimary = false);

public sealed record AssociationInput(
    string AssociationName,
    string? Role,
    DateTime? JoinedDate,
    DateTime? LeftDate,
    decimal? Payment);

public sealed record EducationInput(
    Guid StatusPublicId,
    string? DegreeTitle,
    Guid StudyTypePublicId,
    Guid CareerPublicId,
    string Institution,
    string CountryCode,
    string? Specialty,
    bool IsCurrentlyStudying,
    DateTime StartDate,
    DateTime? EndDate,
    Guid? ShiftPublicId,
    Guid? ModalityPublicId,
    int? TotalSubjects,
    int? ApprovedSubjects);

public sealed record LanguageInput(
    string LanguageCode,
    string LevelCode,
    bool Speaks,
    bool Writes,
    bool Reads);

public sealed record TrainingInput(
    string TrainingName,
    string TrainingTypeCode,
    string? Description,
    string? Topic,
    string? Institution,
    string? Instructors,
    decimal? Score,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsInternal,
    bool IsLocal,
    string CountryCode,
    decimal DurationValue,
    string DurationUnitCode,
    decimal? CostAmount,
    string? CostCurrencyCode);

public sealed record PreviousEmploymentInput(
    string Institution,
    string? Place,
    string? LastPosition,
    string? ManagerName,
    DateTime EntryDate,
    DateTime? RetirementDate,
    string? CompanyPhone,
    string? ExitReason,
    decimal? FirstSalaryAmount,
    decimal? LastSalaryAmount,
    decimal? AverageCommissionAmount,
    string CurrencyCode);

public sealed record ReferenceInput(
    string PersonName,
    string? Address,
    string Phone,
    string ReferenceTypeCode,
    string? Occupation,
    string? Workplace,
    string? WorkPhone,
    decimal KnownTimeYears);

internal sealed class SearchPersonnelFilesQueryValidator : AbstractValidator<SearchPersonnelFilesQuery>
{
    public SearchPersonnelFilesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.MaritalStatus).MaximumLength(80);
        RuleFor(query => query.Nationality).MaximumLength(120);
        RuleFor(query => query.Profession).MaximumLength(120);
        RuleFor(query => query.SortBy)
            .MaximumLength(80)
            .Must(static sortBy => string.IsNullOrWhiteSpace(sortBy) || PersonnelFileDynamicQuerySpec.IsSortableField(sortBy))
            .WithMessage("SortBy is not supported.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PersonnelFileValidationRules.MaxPageSize);
        RuleFor(query => query.MinAge).GreaterThanOrEqualTo(0).When(static query => query.MinAge.HasValue);
        RuleFor(query => query.MaxAge).GreaterThanOrEqualTo(0).When(static query => query.MaxAge.HasValue);
        RuleFor(query => query)
            .Must(static query => !query.MinAge.HasValue || !query.MaxAge.HasValue || query.MinAge.Value <= query.MaxAge.Value)
            .WithMessage("MinAge cannot be greater than MaxAge.");
        RuleFor(query => query)
            .Must(static query => !query.CreatedFromUtc.HasValue || !query.CreatedToUtc.HasValue || query.CreatedFromUtc.Value <= query.CreatedToUtc.Value)
            .WithMessage("CreatedFromUtc cannot be greater than CreatedToUtc.");
    }
}

internal sealed class GetPersonnelFileByIdQueryValidator : AbstractValidator<GetPersonnelFileByIdQuery>
{
    public GetPersonnelFileByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFilePersonalInfoQueryValidator : AbstractValidator<GetPersonnelFilePersonalInfoQuery>
{
    public GetPersonnelFilePersonalInfoQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileIdentificationsQueryValidator : AbstractValidator<GetPersonnelFileIdentificationsQuery>
{
    public GetPersonnelFileIdentificationsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileAddressesQueryValidator : AbstractValidator<GetPersonnelFileAddressesQuery>
{
    public GetPersonnelFileAddressesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEmergencyContactsQueryValidator : AbstractValidator<GetPersonnelFileEmergencyContactsQuery>
{
    public GetPersonnelFileEmergencyContactsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileFamilyMembersQueryValidator : AbstractValidator<GetPersonnelFileFamilyMembersQuery>
{
    public GetPersonnelFileFamilyMembersQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileHobbiesQueryValidator : AbstractValidator<GetPersonnelFileHobbiesQuery>
{
    public GetPersonnelFileHobbiesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEmployeeRelationsQueryValidator : AbstractValidator<GetPersonnelFileEmployeeRelationsQuery>
{
    public GetPersonnelFileEmployeeRelationsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileAssociationsQueryValidator : AbstractValidator<GetPersonnelFileAssociationsQuery>
{
    public GetPersonnelFileAssociationsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEducationsQueryValidator : AbstractValidator<GetPersonnelFileEducationsQuery>
{
    public GetPersonnelFileEducationsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileLanguagesQueryValidator : AbstractValidator<GetPersonnelFileLanguagesQuery>
{
    public GetPersonnelFileLanguagesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileTrainingsQueryValidator : AbstractValidator<GetPersonnelFileTrainingsQuery>
{
    public GetPersonnelFileTrainingsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFilePreviousEmploymentsQueryValidator : AbstractValidator<GetPersonnelFilePreviousEmploymentsQuery>
{
    public GetPersonnelFilePreviousEmploymentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileReferencesQueryValidator : AbstractValidator<GetPersonnelFileReferencesQuery>
{
    public GetPersonnelFileReferencesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileDocumentsQueryValidator : AbstractValidator<GetPersonnelFileDocumentsQuery>
{
    public GetPersonnelFileDocumentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFilePrintQueryValidator : AbstractValidator<GetPersonnelFilePrintQuery>
{
    public GetPersonnelFilePrintQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleForEach(query => query.Sections!)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFilePrintSections.IsSupported)
            .WithMessage("Section is not supported.")
            .When(query => query.Sections is not null);
    }
}

internal sealed class GetPersonnelCatalogItemsQueryValidator : AbstractValidator<GetPersonnelCatalogItemsQuery>
{
    public GetPersonnelCatalogItemsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Category).NotEmpty().MaximumLength(80);
    }
}

internal sealed class GetPersonnelReferenceCatalogItemsQueryValidator : AbstractValidator<GetPersonnelReferenceCatalogItemsQuery>
{
    public GetPersonnelReferenceCatalogItemsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Category)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .WithMessage("Category format is invalid.");
        RuleFor(query => query.ParentCode)
            .MaximumLength(120)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(query => !string.IsNullOrWhiteSpace(query.ParentCode))
            .WithMessage("ParentCode format is invalid.");
    }
}

internal sealed class ExportPersonnelFilesQueryValidator : AbstractValidator<ExportPersonnelFilesQuery>
{
    public ExportPersonnelFilesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.MaritalStatus).MaximumLength(80);
        RuleFor(query => query.Nationality).MaximumLength(120);
        RuleFor(query => query.Profession).MaximumLength(120);
        RuleFor(query => query.SortBy)
            .MaximumLength(80)
            .Must(static sortBy => string.IsNullOrWhiteSpace(sortBy) || PersonnelFileDynamicQuerySpec.IsSortableField(sortBy))
            .WithMessage("SortBy is not supported.");
        RuleFor(query => query.MinAge).GreaterThanOrEqualTo(0).When(static query => query.MinAge.HasValue);
        RuleFor(query => query.MaxAge).GreaterThanOrEqualTo(0).When(static query => query.MaxAge.HasValue);
        RuleFor(query => query)
            .Must(static query => !query.MinAge.HasValue || !query.MaxAge.HasValue || query.MinAge.Value <= query.MaxAge.Value)
            .WithMessage("MinAge cannot be greater than MaxAge.");
        RuleFor(query => query)
            .Must(static query => !query.CreatedFromUtc.HasValue || !query.CreatedToUtc.HasValue || query.CreatedFromUtc.Value <= query.CreatedToUtc.Value)
            .WithMessage("CreatedFromUtc cannot be greater than CreatedToUtc.");
    }
}

internal sealed class DynamicQueryPersonnelFilesQueryValidator : AbstractValidator<DynamicQueryPersonnelFilesQuery>
{
    public DynamicQueryPersonnelFilesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PersonnelFileValidationRules.MaxPageSize);

        RuleFor(query => query.GroupBy)
            .Must(groupBy => groupBy.Count <= PersonnelFileDynamicQuerySpec.MaxGroupFields)
            .WithMessage($"A maximum of {PersonnelFileDynamicQuerySpec.MaxGroupFields} group fields is allowed.");
        RuleForEach(query => query.GroupBy)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFileDynamicQuerySpec.IsGroupableField)
            .WithMessage("GroupBy field is not supported.");

        RuleForEach(query => query.Filters)
            .SetValidator(new PersonnelFileDynamicFilterInputValidator());
        RuleForEach(query => query.Sort)
            .SetValidator(new PersonnelFileDynamicSortInputValidator());
    }
}

internal sealed class PersonnelFileDynamicFilterInputValidator : AbstractValidator<PersonnelFileDynamicFilterInput>
{
    public PersonnelFileDynamicFilterInputValidator()
    {
        RuleFor(input => input.Field).NotEmpty().MaximumLength(80);
        RuleFor(input => input.Operator).NotEmpty().MaximumLength(20);

        RuleFor(input => input)
            .Must(static input => PersonnelFileDynamicQuerySpec.IsSupportedFilter(input.Field, input.Operator))
            .WithMessage("Filter field/operator is not supported.");

        RuleFor(input => input)
            .Must(static input => PersonnelFileDynamicQuerySpec.HasRequiredValue(input))
            .WithMessage("Filter payload is invalid for the requested operator.");
    }
}

internal sealed class PersonnelFileDynamicSortInputValidator : AbstractValidator<PersonnelFileDynamicSortInput>
{
    public PersonnelFileDynamicSortInputValidator()
    {
        RuleFor(input => input.Field)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFileDynamicQuerySpec.IsSortableField)
            .WithMessage("Sort field is not supported.");
    }
}

internal sealed class GetPersonnelFilesAnalyticsSummaryQueryValidator : AbstractValidator<GetPersonnelFilesAnalyticsSummaryQuery>
{
    public GetPersonnelFilesAnalyticsSummaryQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
    }
}

internal sealed class GetPersonnelFileDocumentDownloadQueryValidator : AbstractValidator<GetPersonnelFileDocumentDownloadQuery>
{
    public GetPersonnelFileDocumentDownloadQueryValidator()
    {
        RuleFor(query => query.DocumentId).NotEmpty();
    }
}

internal sealed class CreatePersonnelFileCommandValidator : AbstractValidator<CreatePersonnelFileCommand>
{
    public CreatePersonnelFileCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(PersonnelFileValidationRules.IsValidName)
            .WithMessage("FirstName format is invalid.");
        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(PersonnelFileValidationRules.IsValidName)
            .WithMessage("LastName format is invalid.");
        RuleFor(command => command.BirthDate).NotEmpty();
        RuleFor(command => command.PersonalEmail).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.PersonalEmail));
        RuleFor(command => command.InstitutionalEmail).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.InstitutionalEmail));
        RuleFor(command => command.AssignedPositionSlotId)
            .NotEqual(Guid.Empty)
            .When(static command => command.AssignedPositionSlotId.HasValue)
            .OverridePropertyName("assignedPositionSlotPublicId");
        RuleFor(command => command.PersonalPhone)
            .MaximumLength(40)
            .Must(PersonnelFileValidationRules.IsValidPhone)
            .When(command => !string.IsNullOrWhiteSpace(command.PersonalPhone))
            .WithMessage("PersonalPhone format is invalid.");
        RuleFor(command => command.InstitutionalPhone)
            .MaximumLength(40)
            .Must(PersonnelFileValidationRules.IsValidPhone)
            .When(command => !string.IsNullOrWhiteSpace(command.InstitutionalPhone))
            .WithMessage("InstitutionalPhone format is invalid.");
        RuleFor(command => command.MaritalStatusCode)
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(command => !string.IsNullOrWhiteSpace(command.MaritalStatusCode))
            .WithMessage("MaritalStatusCode format is invalid.");
        RuleFor(command => command.ProfessionCode)
            .MaximumLength(120)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(command => !string.IsNullOrWhiteSpace(command.ProfessionCode))
            .WithMessage("ProfessionCode format is invalid.");
        RuleFor(command => command.BirthCountryCode)
            .MaximumLength(3)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(command => !string.IsNullOrWhiteSpace(command.BirthCountryCode))
            .WithMessage("BirthCountryCode format is invalid.");
        RuleFor(command => command.BirthDepartmentCode)
            .MaximumLength(120)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(command => !string.IsNullOrWhiteSpace(command.BirthDepartmentCode))
            .WithMessage("BirthDepartmentCode format is invalid.");
        RuleFor(command => command.BirthMunicipalityCode)
            .MaximumLength(120)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(command => !string.IsNullOrWhiteSpace(command.BirthMunicipalityCode))
            .WithMessage("BirthMunicipalityCode format is invalid.");
        RuleFor(command => command.AssignedPositionSlotId)
            .NotNull()
            .When(static command => command.RecordType == PersonnelFileRecordType.Employee)
            .WithMessage("AssignedPositionSlotPublicId is required for employee personnel files.")
            .OverridePropertyName("assignedPositionSlotPublicId");
        RuleFor(command => command.AssignedPositionSlotId)
            .Must((command, assignedPositionSlotId) =>
                command.RecordType != PersonnelFileRecordType.Candidate || !assignedPositionSlotId.HasValue)
            .WithMessage("AssignedPositionSlotPublicId is not allowed for candidate personnel files.")
            .OverridePropertyName("assignedPositionSlotPublicId");
        RuleFor(command => command.Identifications).NotEmpty();
    }
}

internal sealed class UpdatePersonnelFilePersonalInfoCommandValidator : AbstractValidator<UpdatePersonnelFilePersonalInfoCommand>
{
    public UpdatePersonnelFilePersonalInfoCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(PersonnelFileValidationRules.IsValidName)
            .WithMessage("FirstName format is invalid.");
        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(PersonnelFileValidationRules.IsValidName)
            .WithMessage("LastName format is invalid.");
        RuleFor(command => command.BirthDate).NotEmpty();
        RuleFor(command => command.PersonalEmail).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.PersonalEmail));
        RuleFor(command => command.InstitutionalEmail).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.InstitutionalEmail));
        RuleFor(command => command.MaritalStatusCode)
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(command => !string.IsNullOrWhiteSpace(command.MaritalStatusCode))
            .WithMessage("MaritalStatusCode format is invalid.");
        RuleFor(command => command.ProfessionCode)
            .MaximumLength(120)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(command => !string.IsNullOrWhiteSpace(command.ProfessionCode))
            .WithMessage("ProfessionCode format is invalid.");
        RuleFor(command => command.BirthCountryCode)
            .MaximumLength(3)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(command => !string.IsNullOrWhiteSpace(command.BirthCountryCode))
            .WithMessage("BirthCountryCode format is invalid.");
        RuleFor(command => command.BirthDepartmentCode)
            .MaximumLength(120)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(command => !string.IsNullOrWhiteSpace(command.BirthDepartmentCode))
            .WithMessage("BirthDepartmentCode format is invalid.");
        RuleFor(command => command.BirthMunicipalityCode)
            .MaximumLength(120)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(command => !string.IsNullOrWhiteSpace(command.BirthMunicipalityCode))
            .WithMessage("BirthMunicipalityCode format is invalid.");
        RuleFor(command => command.AssignedPositionSlotId)
            .NotEqual(Guid.Empty)
            .When(static command => command.AssignedPositionSlotId.HasValue)
            .OverridePropertyName("assignedPositionSlotPublicId");
        RuleFor(command => command.AssignedPositionSlotId)
            .NotNull()
            .When(static command => command.RecordType == PersonnelFileRecordType.Employee)
            .WithMessage("AssignedPositionSlotPublicId is required for employee personnel files.")
            .OverridePropertyName("assignedPositionSlotPublicId");
        RuleFor(command => command.AssignedPositionSlotId)
            .Must((command, assignedPositionSlotId) =>
                command.RecordType != PersonnelFileRecordType.Candidate || !assignedPositionSlotId.HasValue)
            .WithMessage("AssignedPositionSlotPublicId is not allowed for candidate personnel files.")
            .OverridePropertyName("assignedPositionSlotPublicId");
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ReplacePersonnelFileIdentificationsCommandValidator : AbstractValidator<ReplacePersonnelFileIdentificationsCommand>
{
    public ReplacePersonnelFileIdentificationsCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Identifications).NotEmpty();
        RuleForEach(command => command.Identifications).SetValidator(new IdentificationInputValidator());
    }
}

internal sealed class ReplacePersonnelFileAddressesCommandValidator : AbstractValidator<ReplacePersonnelFileAddressesCommand>
{
    public ReplacePersonnelFileAddressesCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Addresses).SetValidator(new AddressInputValidator());
    }
}

internal sealed class ReplacePersonnelFileEmergencyContactsCommandValidator : AbstractValidator<ReplacePersonnelFileEmergencyContactsCommand>
{
    public ReplacePersonnelFileEmergencyContactsCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Contacts).SetValidator(new EmergencyContactInputValidator());
    }
}

internal sealed class ReplacePersonnelFileFamilyMembersCommandValidator : AbstractValidator<ReplacePersonnelFileFamilyMembersCommand>
{
    public ReplacePersonnelFileFamilyMembersCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.FamilyMembers).SetValidator(new FamilyMemberInputValidator());
    }
}

internal sealed class ReplacePersonnelFileHobbiesCommandValidator : AbstractValidator<ReplacePersonnelFileHobbiesCommand>
{
    public ReplacePersonnelFileHobbiesCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Hobbies).SetValidator(new HobbyInputValidator());
    }
}

internal sealed class ReplacePersonnelFileEmployeeRelationsCommandValidator : AbstractValidator<ReplacePersonnelFileEmployeeRelationsCommand>
{
    public ReplacePersonnelFileEmployeeRelationsCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Relations).SetValidator(new EmployeeRelationInputValidator());
    }
}

internal sealed class ReplacePersonnelFileBankAccountsCommandValidator : AbstractValidator<ReplacePersonnelFileBankAccountsCommand>
{
    public ReplacePersonnelFileBankAccountsCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.BankAccounts).SetValidator(new BankAccountInputValidator());
    }
}

internal sealed class ReplacePersonnelFileAssociationsCommandValidator : AbstractValidator<ReplacePersonnelFileAssociationsCommand>
{
    public ReplacePersonnelFileAssociationsCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Associations).SetValidator(new AssociationInputValidator());
    }
}

internal sealed class ReplacePersonnelFileEducationsCommandValidator : AbstractValidator<ReplacePersonnelFileEducationsCommand>
{
    public ReplacePersonnelFileEducationsCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Educations).SetValidator(new EducationInputValidator());
    }
}

internal sealed class ReplacePersonnelFileLanguagesCommandValidator : AbstractValidator<ReplacePersonnelFileLanguagesCommand>
{
    public ReplacePersonnelFileLanguagesCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Languages).SetValidator(new LanguageInputValidator());
    }
}

internal sealed class ReplacePersonnelFileTrainingsCommandValidator : AbstractValidator<ReplacePersonnelFileTrainingsCommand>
{
    public ReplacePersonnelFileTrainingsCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Trainings).SetValidator(new TrainingInputValidator());
    }
}

internal sealed class ReplacePersonnelFilePreviousEmploymentsCommandValidator : AbstractValidator<ReplacePersonnelFilePreviousEmploymentsCommand>
{
    public ReplacePersonnelFilePreviousEmploymentsCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.PreviousEmployments).SetValidator(new PreviousEmploymentInputValidator());
    }
}

internal sealed class ReplacePersonnelFileReferencesCommandValidator : AbstractValidator<ReplacePersonnelFileReferencesCommand>
{
    public ReplacePersonnelFileReferencesCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.References).SetValidator(new ReferenceInputValidator());
    }
}

internal sealed class ActivatePersonnelFileCommandValidator : AbstractValidator<ActivatePersonnelFileCommand>
{
    public ActivatePersonnelFileCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivatePersonnelFileCommandValidator : AbstractValidator<InactivatePersonnelFileCommand>
{
    public InactivatePersonnelFileCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class UploadPersonnelFileDocumentCommandValidator : AbstractValidator<UploadPersonnelFileDocumentCommand>
{
    public UploadPersonnelFileDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.DocumentType).NotEmpty().MaximumLength(100);
        RuleFor(command => command.FileName).NotEmpty().MaximumLength(260);
        RuleFor(command => command.ContentType).NotEmpty().MaximumLength(200);
        RuleFor(command => command.FileData).NotNull().Must(static data => data.Length > 0).WithMessage("FileData is required.");
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command)
            .Must(static command => !command.LoanDate.HasValue || !command.ReturnDate.HasValue || command.ReturnDate.Value.Date >= command.LoanDate.Value.Date)
            .WithMessage(PersonnelFileErrors.DocumentLoanDatesInvalid.Message);
    }
}

internal sealed class InactivatePersonnelFileDocumentCommandValidator : AbstractValidator<InactivatePersonnelFileDocumentCommand>
{
    public InactivatePersonnelFileDocumentCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileObservationCommandValidator : AbstractValidator<AddPersonnelFileObservationCommand>
{
    public AddPersonnelFileObservationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Note).NotEmpty().MaximumLength(4000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelCustomFieldDefinitionsQueryValidator : AbstractValidator<GetPersonnelCustomFieldDefinitionsQuery>
{
    public GetPersonnelCustomFieldDefinitionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class CreatePersonnelCustomFieldDefinitionCommandValidator : AbstractValidator<CreatePersonnelCustomFieldDefinitionCommand>
{
    public CreatePersonnelCustomFieldDefinitionCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Key)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .WithMessage("Key format is invalid.");
        RuleFor(command => command.Label).NotEmpty().MaximumLength(200);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.OptionsJson).MaximumLength(12000);
    }
}

internal sealed class UpdatePersonnelCustomFieldDefinitionCommandValidator : AbstractValidator<UpdatePersonnelCustomFieldDefinitionCommand>
{
    public UpdatePersonnelCustomFieldDefinitionCommandValidator()
    {
        RuleFor(command => command.DefinitionId).NotEmpty();
        RuleFor(command => command.Key)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .WithMessage("Key format is invalid.");
        RuleFor(command => command.Label).NotEmpty().MaximumLength(200);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.OptionsJson).MaximumLength(12000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class IdentificationInputValidator : AbstractValidator<IdentificationInput>
{
    public IdentificationInputValidator()
    {
        RuleFor(input => input.IdentificationTypeCode)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .WithMessage("IdentificationTypeCode format is invalid.");
        RuleFor(input => input.IdentificationNumber)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .WithMessage("IdentificationNumber format is invalid.");
        RuleFor(input => input)
            .Must(static input => !input.IssuedDate.HasValue || !input.ExpiryDate.HasValue || input.ExpiryDate.Value.Date >= input.IssuedDate.Value.Date)
            .WithMessage(PersonnelFileErrors.EffectiveDatesInvalid.Message);
    }
}

internal sealed class AddressInputValidator : AbstractValidator<AddressInput>
{
    public AddressInputValidator()
    {
        RuleFor(input => input.AddressLine).NotEmpty().MaximumLength(500);
        RuleFor(input => input.Country).MaximumLength(120);
        RuleFor(input => input.Department).MaximumLength(120);
        RuleFor(input => input.Municipality).MaximumLength(120);
        RuleFor(input => input.PostalCode).MaximumLength(40);
    }
}

internal sealed class EmergencyContactInputValidator : AbstractValidator<EmergencyContactInput>
{
    public EmergencyContactInputValidator()
    {
        RuleFor(input => input.Name).NotEmpty().MaximumLength(150);
        RuleFor(input => input.Relationship).NotEmpty().MaximumLength(80);
        RuleFor(input => input.Phone)
            .NotEmpty()
            .MaximumLength(40)
            .Must(PersonnelFileValidationRules.IsValidPhone)
            .WithMessage("Phone format is invalid.");
        RuleFor(input => input.Address).MaximumLength(500);
        RuleFor(input => input.Workplace).MaximumLength(200);
    }
}

internal sealed class FamilyMemberInputValidator : AbstractValidator<FamilyMemberInput>
{
    public FamilyMemberInputValidator()
    {
        RuleFor(input => input.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(PersonnelFileValidationRules.IsValidName)
            .WithMessage("FirstName format is invalid.");
        RuleFor(input => input.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(PersonnelFileValidationRules.IsValidName)
            .WithMessage("LastName format is invalid.");
        RuleFor(input => input.KinshipCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.DocumentType).MaximumLength(80);
        RuleFor(input => input.DocumentNumber).MaximumLength(80);
        RuleFor(input => input.Phone).MaximumLength(40);
        RuleFor(input => input.WorkPhone).MaximumLength(40);
        RuleFor(input => input.Salary).GreaterThanOrEqualTo(0).When(static input => input.Salary.HasValue);

        RuleFor(input => input)
            .Must(static input => !input.IsStudying || (!string.IsNullOrWhiteSpace(input.StudyPlace) && !string.IsNullOrWhiteSpace(input.AcademicLevel)))
            .WithMessage(PersonnelFileErrors.FamilyMemberRuleViolation.Message);

        RuleFor(input => input)
            .Must(static input => !input.IsWorking || (!string.IsNullOrWhiteSpace(input.Workplace) && !string.IsNullOrWhiteSpace(input.JobTitle)))
            .WithMessage(PersonnelFileErrors.FamilyMemberRuleViolation.Message);

        RuleFor(input => input)
            .Must(static input => !input.IsDeceased || input.DeceasedDate.HasValue)
            .WithMessage(PersonnelFileErrors.FamilyMemberRuleViolation.Message);
    }
}

internal sealed class HobbyInputValidator : AbstractValidator<HobbyInput>
{
    public HobbyInputValidator()
    {
        RuleFor(input => input.HobbyName).NotEmpty().MaximumLength(120);
    }
}

internal sealed class EmployeeRelationInputValidator : AbstractValidator<EmployeeRelationInput>
{
    public EmployeeRelationInputValidator()
    {
        RuleFor(input => input.RelatedEmployeePublicId).NotEmpty();
        RuleFor(input => input.Relationship).NotEmpty().MaximumLength(80);
    }
}

internal sealed class BankAccountInputValidator : AbstractValidator<BankAccountInput>
{
    public BankAccountInputValidator()
    {
        RuleFor(input => input.BankCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.CurrencyCode).NotEmpty().MaximumLength(40);
        RuleFor(input => input.AccountNumber).NotEmpty().MaximumLength(80);
        RuleFor(input => input.AccountTypeCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class AssociationInputValidator : AbstractValidator<AssociationInput>
{
    public AssociationInputValidator()
    {
        RuleFor(input => input.AssociationName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.Role).MaximumLength(120);
        RuleFor(input => input.Payment).GreaterThanOrEqualTo(0).When(static input => input.Payment.HasValue);
        RuleFor(input => input)
            .Must(static input => !input.JoinedDate.HasValue || !input.LeftDate.HasValue || input.LeftDate.Value.Date >= input.JoinedDate.Value.Date)
            .WithMessage(PersonnelFileErrors.EffectiveDatesInvalid.Message);
    }
}

internal sealed class EducationInputValidator : AbstractValidator<EducationInput>
{
    public EducationInputValidator()
    {
        RuleFor(input => input.StatusPublicId).NotEmpty();
        RuleFor(input => input.DegreeTitle).MaximumLength(200);
        RuleFor(input => input.StudyTypePublicId).NotEmpty();
        RuleFor(input => input.CareerPublicId).NotEmpty();
        RuleFor(input => input.Institution).NotEmpty().MaximumLength(200);
        RuleFor(input => input.CountryCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.Specialty).MaximumLength(200);
        RuleFor(input => input.TotalSubjects).GreaterThanOrEqualTo(0).When(static input => input.TotalSubjects.HasValue);
        RuleFor(input => input.ApprovedSubjects).GreaterThanOrEqualTo(0).When(static input => input.ApprovedSubjects.HasValue);
        RuleFor(input => input)
            .Must(static input => input.IsCurrentlyStudying || input.EndDate.HasValue)
            .WithMessage("EndDate is required when IsCurrentlyStudying is false.");
        RuleFor(input => input)
            .Must(static input => !input.EndDate.HasValue || input.EndDate.Value.Date >= input.StartDate.Date)
            .WithMessage(PersonnelFileErrors.EffectiveDatesInvalid.Message);
        RuleFor(input => input)
            .Must(static input => !input.TotalSubjects.HasValue || !input.ApprovedSubjects.HasValue || input.ApprovedSubjects.Value <= input.TotalSubjects.Value)
            .WithMessage("ApprovedSubjects cannot be greater than TotalSubjects.");
    }
}

internal sealed class LanguageInputValidator : AbstractValidator<LanguageInput>
{
    public LanguageInputValidator()
    {
        RuleFor(input => input.LanguageCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.LevelCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input)
            .Must(static input => input.Speaks || input.Writes || input.Reads)
            .WithMessage("At least one of Speaks, Writes, or Reads must be true.");
    }
}

internal sealed class TrainingInputValidator : AbstractValidator<TrainingInput>
{
    public TrainingInputValidator()
    {
        RuleFor(input => input.TrainingName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.TrainingTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.Description).MaximumLength(1000);
        RuleFor(input => input.Topic).MaximumLength(200);
        RuleFor(input => input.Institution).MaximumLength(200);
        RuleFor(input => input.Instructors).MaximumLength(500);
        RuleFor(input => input.CountryCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.DurationValue).GreaterThan(0);
        RuleFor(input => input.DurationUnitCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.CostAmount).GreaterThanOrEqualTo(0).When(static input => input.CostAmount.HasValue);
        RuleFor(input => input.CostCurrencyCode)
            .NotEmpty()
            .MaximumLength(40);
        RuleFor(input => input)
            .Must(static input => !input.EndDate.HasValue || input.EndDate.Value.Date >= input.StartDate.Date)
            .WithMessage(PersonnelFileErrors.EffectiveDatesInvalid.Message);
    }
}

internal sealed class PreviousEmploymentInputValidator : AbstractValidator<PreviousEmploymentInput>
{
    public PreviousEmploymentInputValidator()
    {
        RuleFor(input => input.Institution).NotEmpty().MaximumLength(200);
        RuleFor(input => input.Place).MaximumLength(200);
        RuleFor(input => input.LastPosition).MaximumLength(150);
        RuleFor(input => input.ManagerName).MaximumLength(150);
        RuleFor(input => input.CompanyPhone)
            .MaximumLength(40)
            .Must(PersonnelFileValidationRules.IsValidPhone)
            .When(input => !string.IsNullOrWhiteSpace(input.CompanyPhone))
            .WithMessage("CompanyPhone format is invalid.");
        RuleFor(input => input.ExitReason).MaximumLength(500);
        RuleFor(input => input.FirstSalaryAmount).GreaterThanOrEqualTo(0).When(static input => input.FirstSalaryAmount.HasValue);
        RuleFor(input => input.LastSalaryAmount).GreaterThanOrEqualTo(0).When(static input => input.LastSalaryAmount.HasValue);
        RuleFor(input => input.AverageCommissionAmount).GreaterThanOrEqualTo(0).When(static input => input.AverageCommissionAmount.HasValue);
        RuleFor(input => input.CurrencyCode).NotEmpty().MaximumLength(40);
        RuleFor(input => input)
            .Must(static input => !input.RetirementDate.HasValue || input.RetirementDate.Value.Date >= input.EntryDate.Date)
            .WithMessage(PersonnelFileErrors.EffectiveDatesInvalid.Message);
    }
}

internal sealed class ReferenceInputValidator : AbstractValidator<ReferenceInput>
{
    public ReferenceInputValidator()
    {
        RuleFor(input => input.PersonName).NotEmpty().MaximumLength(150);
        RuleFor(input => input.Address).MaximumLength(500);
        RuleFor(input => input.Phone)
            .NotEmpty()
            .MaximumLength(40)
            .Must(PersonnelFileValidationRules.IsValidPhone)
            .WithMessage("Phone format is invalid.");
        RuleFor(input => input.ReferenceTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.Occupation).MaximumLength(120);
        RuleFor(input => input.Workplace).MaximumLength(200);
        RuleFor(input => input.WorkPhone)
            .MaximumLength(40)
            .Must(PersonnelFileValidationRules.IsValidPhone)
            .When(input => !string.IsNullOrWhiteSpace(input.WorkPhone))
            .WithMessage("WorkPhone format is invalid.");
        RuleFor(input => input.KnownTimeYears).GreaterThanOrEqualTo(0);
    }
}

internal static class PersonnelFilePrintSections
{
    public const string PersonalInfo = "personal-info";
    public const string Identifications = "identifications";
    public const string Addresses = "addresses";
    public const string EmergencyContacts = "emergency-contacts";
    public const string FamilyMembers = "family-members";
    public const string Hobbies = "hobbies";
    public const string EmployeeRelations = "employee-relations";
    public const string BankAccounts = "bank-accounts";
    public const string Associations = "associations";
    public const string Educations = "educations";
    public const string Languages = "languages";
    public const string Trainings = "trainings";
    public const string PreviousEmployments = "previous-employments";
    public const string References = "references";
    public const string Documents = "documents";
    public const string Observations = "observations";

    private static readonly string[] All =
    [
        PersonalInfo,
        Identifications,
        Addresses,
        EmergencyContacts,
        FamilyMembers,
        Hobbies,
        EmployeeRelations,
        BankAccounts,
        Associations,
        Educations,
        Languages,
        Trainings,
        PreviousEmployments,
        References,
        Documents,
        Observations
    ];

    private static readonly HashSet<string> Supported = All.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsSupported(string section) => Supported.Contains(Normalize(section));

    public static IReadOnlyCollection<string> Resolve(IReadOnlyCollection<string>? requestedSections)
    {
        if (requestedSections is null || requestedSections.Count == 0)
        {
            return All;
        }

        var resolved = requestedSections
            .Select(Normalize)
            .Where(IsSupported)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return resolved.Length == 0
            ? All
            : resolved;
    }

    public static PersonnelFileResponse Filter(PersonnelFileResponse response, IReadOnlyCollection<string> includedSections)
    {
        var include = includedSections.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return response with
        {
            Identifications = include.Contains(Identifications) ? response.Identifications : Array.Empty<PersonnelFileIdentificationResponse>(),
            Addresses = include.Contains(Addresses) ? response.Addresses : Array.Empty<PersonnelFileAddressResponse>(),
            EmergencyContacts = include.Contains(EmergencyContacts) ? response.EmergencyContacts : Array.Empty<PersonnelFileEmergencyContactResponse>(),
            FamilyMembers = include.Contains(FamilyMembers) ? response.FamilyMembers : Array.Empty<PersonnelFileFamilyMemberResponse>(),
            Hobbies = include.Contains(Hobbies) ? response.Hobbies : Array.Empty<PersonnelFileHobbyResponse>(),
            EmployeeRelations = include.Contains(EmployeeRelations) ? response.EmployeeRelations : Array.Empty<PersonnelFileEmployeeRelationResponse>(),
            BankAccounts = include.Contains(BankAccounts) ? response.BankAccounts : Array.Empty<PersonnelFileBankAccountResponse>(),
            Associations = include.Contains(Associations) ? response.Associations : Array.Empty<PersonnelFileAssociationResponse>(),
            Educations = include.Contains(Educations) ? response.Educations : Array.Empty<PersonnelFileEducationResponse>(),
            Languages = include.Contains(Languages) ? response.Languages : Array.Empty<PersonnelFileLanguageResponse>(),
            Trainings = include.Contains(Trainings) ? response.Trainings : Array.Empty<PersonnelFileTrainingResponse>(),
            PreviousEmployments = include.Contains(PreviousEmployments) ? response.PreviousEmployments : Array.Empty<PersonnelFilePreviousEmploymentResponse>(),
            References = include.Contains(References) ? response.References : Array.Empty<PersonnelFileReferenceResponse>(),
            Documents = include.Contains(Documents) ? response.Documents : Array.Empty<PersonnelFileDocumentMetadataResponse>(),
            Observations = include.Contains(Observations) ? response.Observations : Array.Empty<PersonnelFileObservationResponse>()
        };
    }

    private static string Normalize(string section) => section.Trim().ToLowerInvariant();
}

public static class PersonnelFileDynamicQuerySpec
{
    public const int MaxGroupFields = 3;
    public const int MaxGroupBucketsPerField = 100;

    private static readonly IReadOnlySet<string> GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "recordtype",
        "maritalstatus",
        "nationality",
        "orgunitid",
        "isactive"
    };

    private static readonly IReadOnlySet<string> SortableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "fullname",
        "firstname",
        "lastname",
        "birthdate",
        "age",
        "recordtype",
        "maritalstatus",
        "nationality",
        "profession",
        "orgunitid",
        "isactive",
        "createdatutc",
        "modifiedatutc"
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> FilterOperators =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["recordtype"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "in" },
            ["maritalstatus"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "in" },
            ["nationality"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "in" },
            ["profession"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "contains" },
            ["orgunitid"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "in" },
            ["isactive"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq" },
            ["age"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "gte", "lte", "between" },
            ["birthdate"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "gte", "lte", "between" },
            ["createdatutc"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "gte", "lte", "between" },
            ["firstname"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "contains" },
            ["lastname"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "contains" },
            ["fullname"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "contains" }
        };

    public static string NormalizeField(string field) => field.Trim().ToLowerInvariant();

    public static string NormalizeOperator(string @operator) => @operator.Trim().ToLowerInvariant();

    public static bool IsGroupableField(string field) => GroupableFields.Contains(NormalizeField(field));

    public static bool IsSortableField(string field) => SortableFields.Contains(NormalizeField(field));

    public static bool IsSupportedFilter(string field, string @operator)
    {
        var normalizedField = NormalizeField(field);
        var normalizedOperator = NormalizeOperator(@operator);
        return FilterOperators.TryGetValue(normalizedField, out var operators) && operators.Contains(normalizedOperator);
    }

    public static bool HasRequiredValue(PersonnelFileDynamicFilterInput filter)
    {
        var normalizedOperator = NormalizeOperator(filter.Operator);
        return normalizedOperator switch
        {
            "in" => filter.Values is { Count: > 0 } && filter.Values.Any(value => !string.IsNullOrWhiteSpace(value)),
            "between" => !string.IsNullOrWhiteSpace(filter.Value) && !string.IsNullOrWhiteSpace(filter.ValueTo),
            _ => !string.IsNullOrWhiteSpace(filter.Value)
        };
    }
}

internal static class PersonnelCurriculumCatalogCategories
{
    public const string EducationStatus = "CurriculumEducationStatus";
    public const string StudyType = "CurriculumStudyType";
    public const string Shift = "CurriculumShift";
    public const string Modality = "CurriculumModality";
    public const string Language = "CurriculumLanguage";
    public const string LanguageLevel = "CurriculumLanguageLevel";
    public const string TrainingType = "CurriculumTrainingType";
    public const string DurationUnit = "CurriculumDurationUnit";
    public const string ReferenceType = "CurriculumReferenceType";
    public const string Country = "Country";
    public const string Currency = "Currency";
}

internal static class PersonnelCurriculumCatalogValidation
{
    public static async Task<Error> ValidateCodeAsync(
        IPersonnelFileRepository repository,
        Guid tenantId,
        string fieldName,
        string category,
        string code,
        CancellationToken cancellationToken)
    {
        var isActive = await repository.CatalogCodeIsActiveAsync(tenantId, category, code, cancellationToken);
        return isActive
            ? Error.None
            : ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    [fieldName] = [$"Catalog code '{code}' is not active for category '{category}'."]
                });
    }
}

internal static class PersonnelReferenceCatalogValidation
{
    public static async Task<Error> ValidatePersonalInfoCodesAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string? maritalStatusCode,
        string? professionCode,
        string? birthCountryCode,
        string? birthDepartmentCode,
        string? birthMunicipalityCode,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(maritalStatusCode))
        {
            var statusError = await ValidateOptionalReferenceCodeAsync(
                repository,
                "maritalStatusCode",
                await ResolveCompanyCountryCodeAsync(repository, companyId, cancellationToken),
                PersonnelReferenceCatalogCategories.MaritalStatus,
                maritalStatusCode,
                cancellationToken);
            if (statusError != Error.None)
            {
                return statusError;
            }
        }

        if (!string.IsNullOrWhiteSpace(professionCode))
        {
            var professionError = await ValidateOptionalReferenceCodeAsync(
                repository,
                "professionCode",
                await ResolveCompanyCountryCodeAsync(repository, companyId, cancellationToken),
                PersonnelReferenceCatalogCategories.Profession,
                professionCode,
                cancellationToken);
            if (professionError != Error.None)
            {
                return professionError;
            }
        }

        return await ValidateBirthLocationAsync(
            repository,
            birthCountryCode,
            birthDepartmentCode,
            birthMunicipalityCode,
            cancellationToken);
    }

    public static Task<Error> ValidateIdentificationTypeCodeAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string identificationTypeCode,
        CancellationToken cancellationToken) =>
        ValidateOptionalReferenceCodeForCompanyAsync(
            repository,
            companyId,
            "identificationTypeCode",
            PersonnelReferenceCatalogCategories.IdentificationType,
            identificationTypeCode,
            cancellationToken);

    public static Task<Error> ValidateKinshipCodeAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string fieldName,
        string kinshipCode,
        CancellationToken cancellationToken) =>
        ValidateOptionalReferenceCodeForCompanyAsync(
            repository,
            companyId,
            fieldName,
            PersonnelReferenceCatalogCategories.Kinship,
            kinshipCode,
            cancellationToken);

    private static async Task<Error> ValidateBirthLocationAsync(
        IPersonnelFileRepository repository,
        string? birthCountryCode,
        string? birthDepartmentCode,
        string? birthMunicipalityCode,
        CancellationToken cancellationToken)
    {
        var normalizedCountry = string.IsNullOrWhiteSpace(birthCountryCode)
            ? null
            : birthCountryCode.Trim().ToUpperInvariant();
        var normalizedDepartment = string.IsNullOrWhiteSpace(birthDepartmentCode)
            ? null
            : birthDepartmentCode.Trim().ToUpperInvariant();
        var normalizedMunicipality = string.IsNullOrWhiteSpace(birthMunicipalityCode)
            ? null
            : birthMunicipalityCode.Trim().ToUpperInvariant();

        if (normalizedDepartment is not null && normalizedCountry is null)
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthCountryCode"] = ["BirthCountryCode is required when BirthDepartmentCode is provided."]
                });
        }

        if (normalizedMunicipality is not null && normalizedDepartment is null)
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthDepartmentCode"] = ["BirthDepartmentCode is required when BirthMunicipalityCode is provided."]
                });
        }

        if (normalizedCountry is null)
        {
            return Error.None;
        }

        if (!await repository.CountryCodeIsActiveAsync(normalizedCountry, cancellationToken))
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthCountryCode"] = [$"Country code '{normalizedCountry}' is not active."]
                });
        }

        if (normalizedDepartment is null && normalizedMunicipality is null)
        {
            return Error.None;
        }

        if (normalizedCountry != LocationValidationRules.ElSalvadorCountryCode)
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthCountryCode"] = ["Birth department and municipality catalogs are only available for country code 'SV' in this phase."]
                });
        }

        if (normalizedDepartment is not null &&
            !await repository.ReferenceCatalogCodeIsActiveAsync(
                normalizedCountry,
                PersonnelReferenceCatalogCategories.Department,
                normalizedDepartment,
                cancellationToken))
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthDepartmentCode"] = [$"Catalog code '{normalizedDepartment}' is not active for category '{PersonnelReferenceCatalogCategories.Department}'."]
                });
        }

        if (normalizedMunicipality is not null &&
            !await repository.ReferenceCatalogCodeIsActiveAsync(
                normalizedCountry,
                PersonnelReferenceCatalogCategories.Municipality,
                normalizedMunicipality,
                cancellationToken))
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthMunicipalityCode"] = [$"Catalog code '{normalizedMunicipality}' is not active for category '{PersonnelReferenceCatalogCategories.Municipality}'."]
                });
        }

        if (normalizedDepartment is not null &&
            normalizedMunicipality is not null &&
            !await repository.ReferenceMunicipalityBelongsToDepartmentAsync(
                normalizedCountry,
                normalizedDepartment,
                normalizedMunicipality,
                cancellationToken))
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthMunicipalityCode"] = ["BirthMunicipalityCode does not belong to the selected BirthDepartmentCode."]
                });
        }

        return Error.None;
    }

    private static async Task<Error> ValidateOptionalReferenceCodeAsync(
        IPersonnelFileRepository repository,
        string fieldName,
        string countryCode,
        string category,
        string? code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Error.None;
        }

        var isActive = await repository.ReferenceCatalogCodeIsActiveAsync(countryCode, category, code, cancellationToken);
        return isActive
            ? Error.None
            : ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    [fieldName] = [$"Catalog code '{code.Trim().ToUpperInvariant()}' is not active for category '{category}'."]
                });
    }

    private static async Task<Error> ValidateOptionalReferenceCodeForCompanyAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string fieldName,
        string category,
        string? code,
        CancellationToken cancellationToken)
    {
        var companyCountryCode = await ResolveCompanyCountryCodeAsync(repository, companyId, cancellationToken);
        return await ValidateOptionalReferenceCodeAsync(
            repository,
            fieldName,
            companyCountryCode,
            category,
            code,
            cancellationToken);
    }

    private static async Task<string> ResolveCompanyCountryCodeAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var countryCode = await repository.GetCompanyCountryCodeAsync(companyId, cancellationToken);
        return string.IsNullOrWhiteSpace(countryCode)
            ? LocationValidationRules.ElSalvadorCountryCode
            : countryCode.Trim().ToUpperInvariant();
    }
}

internal sealed class SearchPersonnelFilesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchPersonnelFilesQuery, PagedResponse<PersonnelFileListItemResponse>>
{
    public async Task<Result<PagedResponse<PersonnelFileListItemResponse>>> Handle(
        SearchPersonnelFilesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PersonnelFileListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.IsActive,
            query.RecordType,
            query.OrgUnitId,
            query.MinAge,
            query.MaxAge,
            query.MaritalStatus,
            query.Nationality,
            query.Profession,
            query.CreatedFromUtc,
            query.CreatedToUtc,
            query.Search,
            query.SortBy,
            query.SortDirection,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<PersonnelFileListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => PersonnelFilePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<PersonnelFileListItemResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetPersonnelFileByIdQuery, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        GetPersonnelFileByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.PersonnelFileId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = PersonnelFilePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);
            return Result<PersonnelFileResponse>.Success(response);
        }

        return Result<PersonnelFileResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.PersonnelFileId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : PersonnelFileErrors.NotFound);
    }
}

internal abstract class GetPersonnelFileSectionQueryHandlerBase
{
    protected static async Task<Result<TResponse>?> EnsureCanReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository repository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<TResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetByIdAsync(personnelFileId, cancellationToken);
        if (personnelFile is not null)
        {
            return null;
        }

        return Result<TResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : PersonnelFileErrors.NotFound);
    }
}

internal sealed class GetPersonnelFilePersonalInfoQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFilePersonalInfoQuery, PersonnelFilePersonalInfoResponse>
{
    public async Task<Result<PersonnelFilePersonalInfoResponse>> Handle(
        GetPersonnelFilePersonalInfoQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<PersonnelFilePersonalInfoResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetPersonalInfoAsync(query.PersonnelFileId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file personal info could not be resolved after authorization.");

        return Result<PersonnelFilePersonalInfoResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFileIdentificationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileIdentificationsQuery, IReadOnlyCollection<PersonnelFileIdentificationResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileIdentificationResponse>>> Handle(
        GetPersonnelFileIdentificationsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileIdentificationResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetIdentificationsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileIdentificationResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileAddressesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAddressesQuery, IReadOnlyCollection<PersonnelFileAddressResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileAddressResponse>>> Handle(
        GetPersonnelFileAddressesQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileAddressResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetAddressesAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileAddressResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileEmergencyContactsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEmergencyContactsQuery, IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>> Handle(
        GetPersonnelFileEmergencyContactsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetEmergencyContactsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileFamilyMembersQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileFamilyMembersQuery, IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>> Handle(
        GetPersonnelFileFamilyMembersQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetFamilyMembersAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileHobbiesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileHobbiesQuery, IReadOnlyCollection<PersonnelFileHobbyResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileHobbyResponse>>> Handle(
        GetPersonnelFileHobbiesQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileHobbyResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetHobbiesAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileHobbyResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileEmployeeRelationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEmployeeRelationsQuery, IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>> Handle(
        GetPersonnelFileEmployeeRelationsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetEmployeeRelationsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileAssociationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAssociationsQuery, IReadOnlyCollection<PersonnelFileAssociationResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileAssociationResponse>>> Handle(
        GetPersonnelFileAssociationsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileAssociationResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetAssociationsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileAssociationResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileEducationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEducationsQuery, IReadOnlyCollection<PersonnelFileEducationResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileEducationResponse>>> Handle(
        GetPersonnelFileEducationsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileEducationResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetEducationsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileEducationResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileLanguagesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileLanguagesQuery, IReadOnlyCollection<PersonnelFileLanguageResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileLanguageResponse>>> Handle(
        GetPersonnelFileLanguagesQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileLanguageResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetLanguagesAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileLanguageResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileTrainingsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileTrainingsQuery, IReadOnlyCollection<PersonnelFileTrainingResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileTrainingResponse>>> Handle(
        GetPersonnelFileTrainingsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileTrainingResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetTrainingsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileTrainingResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFilePreviousEmploymentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFilePreviousEmploymentsQuery, IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>> Handle(
        GetPersonnelFilePreviousEmploymentsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetPreviousEmploymentsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileReferencesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileReferencesQuery, IReadOnlyCollection<PersonnelFileReferenceResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileReferenceResponse>>> Handle(
        GetPersonnelFileReferencesQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileReferenceResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetReferencesAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileReferenceResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileDocumentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileDocumentsQuery, IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>> Handle(
        GetPersonnelFileDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetDocumentsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFilePrintQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetPersonnelFilePrintQuery, PersonnelFilePrintResponse>
{
    public async Task<Result<PersonnelFilePrintResponse>> Handle(
        GetPersonnelFilePrintQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFilePrintResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFilePrintResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.PersonnelFileId, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFilePrintResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(query.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : PersonnelFileErrors.NotFound);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        response = PersonnelFilePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);
        var includedSections = PersonnelFilePrintSections.Resolve(query.Sections);
        var filtered = PersonnelFilePrintSections.Filter(response, includedSections);

        return Result<PersonnelFilePrintResponse>.Success(
            new PersonnelFilePrintResponse(DateTime.UtcNow, includedSections, filtered));
    }
}

internal sealed class GetPersonnelCatalogItemsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository)
    : IQueryHandler<GetPersonnelCatalogItemsQuery, IReadOnlyCollection<PersonnelCatalogItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelCatalogItemResponse>>> Handle(
        GetPersonnelCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<PersonnelCatalogItemResponse>>.Failure(authorizationResult.Error);
        }

        var items = await repository.GetCatalogItemsAsync(query.CompanyId, query.Category, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelCatalogItemResponse>>.Success(items);
    }
}

internal sealed class GetPersonnelReferenceCatalogItemsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository)
    : IQueryHandler<GetPersonnelReferenceCatalogItemsQuery, IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>> Handle(
        GetPersonnelReferenceCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>.Failure(authorizationResult.Error);
        }

        var items = await repository.GetReferenceCatalogItemsAsync(
            query.CompanyId,
            query.Category,
            query.ParentCode,
            cancellationToken);
        return Result<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>.Success(items);
    }
}

internal sealed class ExportPersonnelFilesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository)
    : IQueryHandler<ExportPersonnelFilesQuery, IReadOnlyCollection<PersonnelFileExportRow>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileExportRow>>> Handle(
        ExportPersonnelFilesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<PersonnelFileExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await repository.GetExportRowsAsync(
            query.CompanyId,
            query.IsActive,
            query.RecordType,
            query.OrgUnitId,
            query.MinAge,
            query.MaxAge,
            query.MaritalStatus,
            query.Nationality,
            query.Profession,
            query.CreatedFromUtc,
            query.CreatedToUtc,
            query.Search,
            query.SortBy,
            query.SortDirection,
            query.MaxRows,
            cancellationToken);

        return Result<IReadOnlyCollection<PersonnelFileExportRow>>.Success(rows);
    }
}

internal sealed class DynamicQueryPersonnelFilesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<DynamicQueryPersonnelFilesQuery, PersonnelFileDynamicQueryResponse>
{
    public async Task<Result<PersonnelFileDynamicQueryResponse>> Handle(
        DynamicQueryPersonnelFilesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileDynamicQueryResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.DynamicQueryAsync(
            query.CompanyId,
            query.Filters,
            query.GroupBy,
            query.Sort,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PersonnelFileDynamicQueryResponse>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => PersonnelFilePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PersonnelFileDynamicQueryResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFilesAnalyticsSummaryQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository)
    : IQueryHandler<GetPersonnelFilesAnalyticsSummaryQuery, PersonnelFileAnalyticsSummaryResponse>
{
    public async Task<Result<PersonnelFileAnalyticsSummaryResponse>> Handle(
        GetPersonnelFilesAnalyticsSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileAnalyticsSummaryResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetAnalyticsSummaryAsync(
            query.CompanyId,
            query.IsActive,
            query.RecordType,
            query.OrgUnitId,
            query.MinAge,
            query.MaxAge,
            query.Search,
            cancellationToken);

        return Result<PersonnelFileAnalyticsSummaryResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFileDocumentDownloadQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetPersonnelFileDocumentDownloadQuery, PersonnelFileDocumentDownloadResponse>
{
    public async Task<Result<PersonnelFileDocumentDownloadResponse>> Handle(
        GetPersonnelFileDocumentDownloadQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileDocumentDownloadResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileDocumentDownloadResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetDocumentDownloadByIdAsync(query.DocumentId, cancellationToken);
        if (response is not null)
        {
            return Result<PersonnelFileDocumentDownloadResponse>.Success(response);
        }

        return Result<PersonnelFileDocumentDownloadResponse>.Failure(
            await repository.DocumentExistsOutsideTenantAsync(query.DocumentId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : PersonnelFileErrors.DocumentNotFound);
    }
}

internal sealed class GetPersonnelCustomFieldDefinitionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository)
    : IQueryHandler<GetPersonnelCustomFieldDefinitionsQuery, IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>>> Handle(
        GetPersonnelCustomFieldDefinitionsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetCustomFieldDefinitionsAsync(query.CompanyId, query.IsActive, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>>.Success(response);
    }
}

internal static class PersonnelFilePolicyAdapter
{
    public static PersonnelFileListItemResponse ApplyAllowedActions(
        PersonnelFileListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                PersonnelFilePermissionCodes.ResourceKey,
                response.RecordType.ToString(),
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }

    public static PersonnelFileResponse ApplyAllowedActions(
        PersonnelFileResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                PersonnelFilePermissionCodes.ResourceKey,
                response.RecordType.ToString(),
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }
}

internal sealed class CreatePersonnelFileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    IPersonnelFileProfilePhotoService profilePhotoService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePersonnelFileCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        CreatePersonnelFileCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileResponse>.Failure(authorizationResult.Error);
        }

        var definitions = await repository.GetCustomFieldDefinitionsAsync(command.CompanyId, isActive: true, cancellationToken);
        var customDataValidation = PersonnelFileValidationRules.ValidateCustomData(definitions, command.CustomDataJson);
        if (customDataValidation != Error.None)
        {
            return Result<PersonnelFileResponse>.Failure(customDataValidation);
        }

        var personalInfoCatalogValidation = await PersonnelReferenceCatalogValidation.ValidatePersonalInfoCodesAsync(
            repository,
            command.CompanyId,
            command.MaritalStatusCode,
            command.ProfessionCode,
            command.BirthCountryCode,
            command.BirthDepartmentCode,
            command.BirthMunicipalityCode,
            cancellationToken);
        if (personalInfoCatalogValidation != Error.None)
        {
            return Result<PersonnelFileResponse>.Failure(personalInfoCatalogValidation);
        }

        var identificationEntities = new List<PersonnelFileIdentification>();
        foreach (var identification in command.Identifications)
        {
            var normalizedIdentificationTypeCode = identification.IdentificationTypeCode.Trim().ToUpperInvariant();
            var identificationTypeValidation = await PersonnelReferenceCatalogValidation.ValidateIdentificationTypeCodeAsync(
                repository,
                command.CompanyId,
                normalizedIdentificationTypeCode,
                cancellationToken);
            if (identificationTypeValidation != Error.None)
            {
                return Result<PersonnelFileResponse>.Failure(identificationTypeValidation);
            }

            var normalized = identification.IdentificationNumber.Trim().ToUpperInvariant();
            var exists = await repository.IdentificationExistsAsync(
                command.CompanyId,
                normalizedIdentificationTypeCode,
                normalized,
                excludingPersonnelFileId: null,
                cancellationToken);
            if (exists)
            {
                return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.IdentificationConflict);
            }

            identificationEntities.Add(PersonnelFileIdentification.Create(
                normalizedIdentificationTypeCode,
                identification.IdentificationNumber,
                identification.IssuedDate,
                identification.ExpiryDate,
                identification.Issuer,
                identification.IsPrimary));
        }

        var personnelFile = PersonnelFile.Create(
            command.RecordType,
            command.FirstName,
            command.LastName,
            command.BirthDate,
            command.MaritalStatusCode,
            command.ProfessionCode,
            command.Nationality,
            command.PersonalEmail,
            command.InstitutionalEmail,
            command.PersonalPhone,
            command.InstitutionalPhone,
            command.BirthCountryCode,
            command.BirthDepartmentCode,
            command.BirthMunicipalityCode,
            photoUrl: null,
            command.OrgUnitId,
            command.AssignedPositionSlotId,
            command.CustomDataJson,
            identificationEntities);
        personnelFile.SetTenantId(command.CompanyId);

        var photoWritePlanResult = await profilePhotoService.PrepareWriteAsync(
            command.CompanyId,
            personnelFile.PublicId,
            command.PhotoUrl,
            currentPersistedPhotoUrl: null,
            cancellationToken);
        if (photoWritePlanResult.IsFailure)
        {
            return Result<PersonnelFileResponse>.Failure(photoWritePlanResult.Error);
        }

        var photoWritePlan = photoWritePlanResult.Value;
        personnelFile.UpdatePersonalInfo(
            command.RecordType,
            command.FirstName,
            command.LastName,
            command.BirthDate,
            command.MaritalStatusCode,
            command.ProfessionCode,
            command.Nationality,
            command.PersonalEmail,
            command.InstitutionalEmail,
            command.PersonalPhone,
            command.InstitutionalPhone,
            command.BirthCountryCode,
            command.BirthDepartmentCode,
            command.BirthMunicipalityCode,
            photoWritePlan.PersistedPhotoUrl,
            command.OrgUnitId,
            command.AssignedPositionSlotId,
            command.CustomDataJson);

        foreach (var identification in personnelFile.Identifications)
        {
            identification.SetTenantId(command.CompanyId);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileCreated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Create,
                    $"Created personnel file {personnelFile.FullName}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            await profilePhotoService.CleanupAfterPersistenceSuccessAsync(photoWritePlan, cancellationToken);
            return Result<PersonnelFileResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await profilePhotoService.CleanupAfterPersistenceFailureAsync(photoWritePlan, cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFilePersonalInfoCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    IPersonnelFileProfilePhotoService profilePhotoService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFilePersonalInfoCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        UpdatePersonnelFilePersonalInfoCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetByIdAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (personnelFile.RecordType != command.RecordType)
        {
            return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.RecordTypeTransitionNotAllowed);
        }

        var definitions = await repository.GetCustomFieldDefinitionsAsync(personnelFile.TenantId, isActive: true, cancellationToken);
        var customDataValidation = PersonnelFileValidationRules.ValidateCustomData(definitions, command.CustomDataJson);
        if (customDataValidation != Error.None)
        {
            return Result<PersonnelFileResponse>.Failure(customDataValidation);
        }

        var personalInfoCatalogValidation = await PersonnelReferenceCatalogValidation.ValidatePersonalInfoCodesAsync(
            repository,
            personnelFile.TenantId,
            command.MaritalStatusCode,
            command.ProfessionCode,
            command.BirthCountryCode,
            command.BirthDepartmentCode,
            command.BirthMunicipalityCode,
            cancellationToken);
        if (personalInfoCatalogValidation != Error.None)
        {
            return Result<PersonnelFileResponse>.Failure(personalInfoCatalogValidation);
        }

        var photoWritePlanResult = await profilePhotoService.PrepareWriteAsync(
            personnelFile.TenantId,
            personnelFile.PublicId,
            command.PhotoUrl,
            personnelFile.PhotoUrl,
            cancellationToken);
        if (photoWritePlanResult.IsFailure)
        {
            return Result<PersonnelFileResponse>.Failure(photoWritePlanResult.Error);
        }

        var photoWritePlan = photoWritePlanResult.Value;

        var before = await repository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            try
            {
                personnelFile.UpdatePersonalInfo(
                    command.RecordType,
                    command.FirstName,
                    command.LastName,
                    command.BirthDate,
                    command.MaritalStatusCode,
                    command.ProfessionCode,
                    command.Nationality,
                    command.PersonalEmail,
                    command.InstitutionalEmail,
                    command.PersonalPhone,
                    command.InstitutionalPhone,
                    command.BirthCountryCode,
                    command.BirthDepartmentCode,
                    command.BirthMunicipalityCode,
                    photoWritePlan.PersistedPhotoUrl,
                    command.OrgUnitId,
                    command.AssignedPositionSlotId,
                    command.CustomDataJson);
            }
            catch (InvalidOperationException)
            {
                await profilePhotoService.CleanupAfterPersistenceFailureAsync(photoWritePlan, cancellationToken);
                return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.ProvisioningFieldsLocked);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated personnel file {personnelFile.FullName} personal info.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await profilePhotoService.CleanupAfterPersistenceSuccessAsync(photoWritePlan, cancellationToken);

            return Result<PersonnelFileResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await profilePhotoService.CleanupAfterPersistenceFailureAsync(photoWritePlan, cancellationToken);
            throw;
        }
    }
}

internal abstract class ReplacePersonnelFileSectionCommandHandlerBase
{
    protected static async Task<(Result<PersonnelFileResponse>? Failure, PersonnelFile? File)> LoadForUpdateAsync(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository repository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<PersonnelFileResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<PersonnelFileResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await repository.GetByIdAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<PersonnelFileResponse>.Failure(
                    await repository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    protected static async Task<Result<PersonnelFileResponse>> PersistSectionAsync(
        PersonnelFile personnelFile,
        string auditMessage,
        IAuditService auditService,
        IPersonnelFileRepository repository,
        IUnitOfWork unitOfWork,
        string eventType,
        CancellationToken cancellationToken)
    {
        var before = await repository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file response could not be resolved before section update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file response could not be resolved after section update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    eventType,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    auditMessage,
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ReplacePersonnelFileIdentificationsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileIdentificationsCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileIdentificationsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entities = new List<PersonnelFileIdentification>();
        foreach (var item in command.Identifications)
        {
            var normalizedIdentificationTypeCode = item.IdentificationTypeCode.Trim().ToUpperInvariant();
            var identificationTypeValidation = await PersonnelReferenceCatalogValidation.ValidateIdentificationTypeCodeAsync(
                repository,
                personnelFile!.TenantId,
                normalizedIdentificationTypeCode,
                cancellationToken);
            if (identificationTypeValidation != Error.None)
            {
                return Result<PersonnelFileResponse>.Failure(identificationTypeValidation);
            }

            var normalized = item.IdentificationNumber.Trim().ToUpperInvariant();
            if (await repository.IdentificationExistsAsync(
                    personnelFile!.TenantId,
                    normalizedIdentificationTypeCode,
                    normalized,
                    personnelFile.Id,
                    cancellationToken))
            {
                return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.IdentificationConflict);
            }

            entities.Add(PersonnelFileIdentification.Create(
                normalizedIdentificationTypeCode,
                item.IdentificationNumber,
                item.IssuedDate,
                item.ExpiryDate,
                item.Issuer,
                item.IsPrimary));
        }

        personnelFile!.ReplaceIdentifications(entities);
        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} identifications.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ReplacePersonnelFileAddressesCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileAddressesCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileAddressesCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entities = command.Addresses
            .Select(item => PersonnelFileAddress.Create(
                item.AddressLine,
                item.Country,
                item.Department,
                item.Municipality,
                item.PostalCode,
                item.IsCurrent))
            .ToArray();

        personnelFile!.ReplaceAddresses(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} addresses.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ReplacePersonnelFileEmergencyContactsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileEmergencyContactsCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileEmergencyContactsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entities = command.Contacts
            .Select(item => PersonnelFileEmergencyContact.Create(
                item.Name,
                item.Relationship,
                item.Phone,
                item.Address,
                item.Workplace))
            .ToArray();

        personnelFile!.ReplaceEmergencyContacts(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} emergency contacts.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ReplacePersonnelFileFamilyMembersCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileFamilyMembersCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileFamilyMembersCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var familyMembers = command.FamilyMembers as IReadOnlyList<FamilyMemberInput> ?? command.FamilyMembers.ToArray();
        var entities = new List<PersonnelFileFamilyMember>(familyMembers.Count);
        try
        {
            for (var index = 0; index < familyMembers.Count; index++)
            {
                var item = familyMembers[index];
                var kinshipCodeValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
                    repository,
                    personnelFile!.TenantId,
                    $"items[{index}].kinshipCode",
                    item.KinshipCode,
                    cancellationToken);
                if (kinshipCodeValidation != Error.None)
                {
                    return Result<PersonnelFileResponse>.Failure(kinshipCodeValidation);
                }

                entities.Add(PersonnelFileFamilyMember.Create(
                    item.FirstName,
                    item.LastName,
                    item.KinshipCode,
                    item.Nationality,
                    item.BirthDate,
                    item.Sex,
                    item.MaritalStatus,
                    item.Occupation,
                    item.DocumentType,
                    item.DocumentNumber,
                    item.Phone,
                    item.IsStudying,
                    item.StudyPlace,
                    item.AcademicLevel,
                    item.IsBeneficiary,
                    item.IsWorking,
                    item.Workplace,
                    item.JobTitle,
                    item.WorkPhone,
                    item.Salary,
                    item.IsDeceased,
                    item.DeceasedDate));
            }
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.FamilyMemberRuleViolation);
        }

        personnelFile!.ReplaceFamilyMembers(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} family members.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ReplacePersonnelFileHobbiesCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileHobbiesCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileHobbiesCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entities = command.Hobbies
            .Select(item => PersonnelFileHobby.Create(item.HobbyName))
            .ToArray();

        personnelFile!.ReplaceHobbies(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} hobbies.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ReplacePersonnelFileEmployeeRelationsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileEmployeeRelationsCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileEmployeeRelationsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var relations = command.Relations.ToArray();
        var entities = new List<PersonnelFileEmployeeRelation>(relations.Length);
        for (var index = 0; index < relations.Length; index++)
        {
            var relation = relations[index];
            if (relation.RelatedEmployeePublicId == personnelFile!.PublicId)
            {
                return Result<PersonnelFileResponse>.Failure(
                    ErrorCatalog.Validation(
                        new Dictionary<string, string[]>
                        {
                            [$"relations[{index}].relatedEmployeePublicId"] = ["A personnel file cannot be related to itself."]
                        }));
            }

            var relatedPersonnelFile = await repository.GetByIdAsync(relation.RelatedEmployeePublicId, cancellationToken);
            if (relatedPersonnelFile is null || relatedPersonnelFile.RecordType != PersonnelFileRecordType.Employee)
            {
                return Result<PersonnelFileResponse>.Failure(
                    ErrorCatalog.Validation(
                        new Dictionary<string, string[]>
                        {
                            [$"relations[{index}].relatedEmployeePublicId"] = ["RelatedEmployeePublicId must reference an existing employee personnel file in the same tenant."]
                        }));
            }

            entities.Add(PersonnelFileEmployeeRelation.Create(relatedPersonnelFile.Id, relation.Relationship));
        }

        personnelFile!.ReplaceEmployeeRelations(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} employee relations.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ReplacePersonnelFileBankAccountsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileBankAccountsCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileBankAccountsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entities = command.BankAccounts
            .Select(item => PersonnelFileBankAccount.Create(
                item.BankCode,
                item.CurrencyCode,
                item.AccountNumber,
                item.AccountTypeCode,
                item.IsPrimary))
            .ToArray();

        personnelFile!.ReplaceBankAccounts(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} bank accounts.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ReplacePersonnelFileAssociationsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileAssociationsCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileAssociationsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entities = new List<PersonnelFileAssociation>();
        try
        {
            entities.AddRange(command.Associations.Select(item => PersonnelFileAssociation.Create(
                item.AssociationName,
                item.Role,
                item.JoinedDate,
                item.LeftDate,
                item.Payment)));
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        personnelFile!.ReplaceAssociations(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} associations.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ReplacePersonnelFileEducationsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelEducationCatalogRepository personnelEducationCatalogRepository,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileEducationsCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileEducationsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entities = new List<PersonnelFileEducation>();
        try
        {
            foreach (var item in command.Educations)
            {
                var statusLookup = await personnelEducationCatalogRepository.GetActiveLookupByIdAsync(
                    personnelFile!.TenantId,
                    PersonnelEducationCatalogType.EducationStatus,
                    item.StatusPublicId,
                    cancellationToken);
                if (statusLookup is null)
                {
                    return Result<PersonnelFileResponse>.Failure(CreateEducationCatalogValidationError(nameof(item.StatusPublicId), item.StatusPublicId));
                }

                var studyTypeLookup = await personnelEducationCatalogRepository.GetActiveLookupByIdAsync(
                    personnelFile.TenantId,
                    PersonnelEducationCatalogType.StudyType,
                    item.StudyTypePublicId,
                    cancellationToken);
                if (studyTypeLookup is null)
                {
                    return Result<PersonnelFileResponse>.Failure(CreateEducationCatalogValidationError(nameof(item.StudyTypePublicId), item.StudyTypePublicId));
                }

                var countryError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
                    repository,
                    personnelFile.TenantId,
                    nameof(item.CountryCode),
                    PersonnelCurriculumCatalogCategories.Country,
                    item.CountryCode,
                    cancellationToken);
                if (countryError != Error.None)
                {
                    return Result<PersonnelFileResponse>.Failure(countryError);
                }

                PersonnelEducationCatalogLookup? shiftValue = null;
                if (item.ShiftPublicId.HasValue)
                {
                    shiftValue = await personnelEducationCatalogRepository.GetActiveLookupByIdAsync(
                        personnelFile.TenantId,
                        PersonnelEducationCatalogType.Shift,
                        item.ShiftPublicId.Value,
                        cancellationToken);
                    if (shiftValue is null)
                    {
                        return Result<PersonnelFileResponse>.Failure(CreateEducationCatalogValidationError(nameof(item.ShiftPublicId), item.ShiftPublicId.Value));
                    }
                }

                PersonnelEducationCatalogLookup? modalityValue = null;
                if (item.ModalityPublicId.HasValue)
                {
                    modalityValue = await personnelEducationCatalogRepository.GetActiveLookupByIdAsync(
                        personnelFile.TenantId,
                        PersonnelEducationCatalogType.Modality,
                        item.ModalityPublicId.Value,
                        cancellationToken);
                    if (modalityValue is null)
                    {
                        return Result<PersonnelFileResponse>.Failure(CreateEducationCatalogValidationError(nameof(item.ModalityPublicId), item.ModalityPublicId.Value));
                    }
                }

                var careerLookup = await personnelEducationCatalogRepository.GetActiveLookupByIdAsync(
                    personnelFile.TenantId,
                    PersonnelEducationCatalogType.Career,
                    item.CareerPublicId,
                    cancellationToken);
                if (careerLookup is null)
                {
                    return Result<PersonnelFileResponse>.Failure(CreateEducationCatalogValidationError(nameof(item.CareerPublicId), item.CareerPublicId));
                }

                entities.Add(PersonnelFileEducation.Create(
                    statusLookup.InternalId,
                    item.DegreeTitle,
                    studyTypeLookup.InternalId,
                    careerLookup.InternalId,
                    item.Institution,
                    item.CountryCode,
                    item.Specialty,
                    item.IsCurrentlyStudying,
                    item.StartDate,
                    item.EndDate,
                    shiftValue?.InternalId,
                    modalityValue?.InternalId,
                    item.TotalSubjects,
                    item.ApprovedSubjects));
            }
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        personnelFile!.ReplaceEducations(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} educations.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }

    private static Error CreateEducationCatalogValidationError(string fieldName, Guid publicId) =>
        ErrorCatalog.Validation(
            new Dictionary<string, string[]>
            {
                [fieldName] = [$"Catalog item '{publicId}' is not active or does not belong to the tenant."]
            });
}

internal sealed class ReplacePersonnelFileLanguagesCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileLanguagesCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileLanguagesCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entities = new List<PersonnelFileLanguage>();
        try
        {
            foreach (var item in command.Languages)
            {
                var languageError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
                    repository,
                    personnelFile!.TenantId,
                    nameof(item.LanguageCode),
                    PersonnelCurriculumCatalogCategories.Language,
                    item.LanguageCode,
                    cancellationToken);
                if (languageError != Error.None)
                {
                    return Result<PersonnelFileResponse>.Failure(languageError);
                }

                var levelError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
                    repository,
                    personnelFile.TenantId,
                    nameof(item.LevelCode),
                    PersonnelCurriculumCatalogCategories.LanguageLevel,
                    item.LevelCode,
                    cancellationToken);
                if (levelError != Error.None)
                {
                    return Result<PersonnelFileResponse>.Failure(levelError);
                }

                entities.Add(PersonnelFileLanguage.Create(
                    item.LanguageCode,
                    item.LevelCode,
                    item.Speaks,
                    item.Writes,
                    item.Reads));
            }
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        personnelFile!.ReplaceLanguages(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} languages.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ReplacePersonnelFileTrainingsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileTrainingsCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileTrainingsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entities = new List<PersonnelFileTraining>();
        try
        {
            foreach (var item in command.Trainings)
            {
                var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
                    repository,
                    personnelFile!.TenantId,
                    nameof(item.TrainingTypeCode),
                    PersonnelCurriculumCatalogCategories.TrainingType,
                    item.TrainingTypeCode,
                    cancellationToken);
                if (typeError != Error.None)
                {
                    return Result<PersonnelFileResponse>.Failure(typeError);
                }

                var countryError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
                    repository,
                    personnelFile.TenantId,
                    nameof(item.CountryCode),
                    PersonnelCurriculumCatalogCategories.Country,
                    item.CountryCode,
                    cancellationToken);
                if (countryError != Error.None)
                {
                    return Result<PersonnelFileResponse>.Failure(countryError);
                }

                var durationUnitError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
                    repository,
                    personnelFile.TenantId,
                    nameof(item.DurationUnitCode),
                    PersonnelCurriculumCatalogCategories.DurationUnit,
                    item.DurationUnitCode,
                    cancellationToken);
                if (durationUnitError != Error.None)
                {
                    return Result<PersonnelFileResponse>.Failure(durationUnitError);
                }

                var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
                    repository,
                    personnelFile.TenantId,
                    nameof(item.CostCurrencyCode),
                    PersonnelCurriculumCatalogCategories.Currency,
                    item.CostCurrencyCode ?? string.Empty,
                    cancellationToken);
                if (currencyError != Error.None)
                {
                    return Result<PersonnelFileResponse>.Failure(currencyError);
                }

                entities.Add(PersonnelFileTraining.Create(
                    item.TrainingName,
                    item.TrainingTypeCode,
                    item.Description,
                    item.Topic,
                    item.Institution,
                    item.Instructors,
                    item.Score,
                    item.StartDate,
                    item.EndDate,
                    item.IsInternal,
                    item.IsLocal,
                    item.CountryCode,
                    item.DurationValue,
                    item.DurationUnitCode,
                    item.CostAmount,
                    item.CostCurrencyCode));
            }
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        personnelFile!.ReplaceTrainings(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} trainings.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ReplacePersonnelFilePreviousEmploymentsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFilePreviousEmploymentsCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFilePreviousEmploymentsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entities = new List<PersonnelFilePreviousEmployment>();
        try
        {
            foreach (var item in command.PreviousEmployments)
            {
                var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
                    repository,
                    personnelFile!.TenantId,
                    nameof(item.CurrencyCode),
                    PersonnelCurriculumCatalogCategories.Currency,
                    item.CurrencyCode,
                    cancellationToken);
                if (currencyError != Error.None)
                {
                    return Result<PersonnelFileResponse>.Failure(currencyError);
                }

                entities.Add(PersonnelFilePreviousEmployment.Create(
                    item.Institution,
                    item.Place,
                    item.LastPosition,
                    item.ManagerName,
                    item.EntryDate,
                    item.RetirementDate,
                    item.CompanyPhone,
                    item.ExitReason,
                    item.FirstSalaryAmount,
                    item.LastSalaryAmount,
                    item.AverageCommissionAmount,
                    item.CurrencyCode));
            }
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        personnelFile!.ReplacePreviousEmployments(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} previous employments.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ReplacePersonnelFileReferencesCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileReferencesCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ReplacePersonnelFileReferencesCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entities = new List<PersonnelFileReference>();
        try
        {
            foreach (var item in command.References)
            {
                var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
                    repository,
                    personnelFile!.TenantId,
                    nameof(item.ReferenceTypeCode),
                    PersonnelCurriculumCatalogCategories.ReferenceType,
                    item.ReferenceTypeCode,
                    cancellationToken);
                if (typeError != Error.None)
                {
                    return Result<PersonnelFileResponse>.Failure(typeError);
                }

                entities.Add(PersonnelFileReference.Create(
                    item.PersonName,
                    item.Address,
                    item.Phone,
                    item.ReferenceTypeCode,
                    item.Occupation,
                    item.Workplace,
                    item.WorkPhone,
                    item.KnownTimeYears));
            }
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        personnelFile!.ReplaceReferences(entities);

        return await PersistSectionAsync(
            personnelFile,
            $"Updated personnel file {personnelFile.FullName} references.",
            auditService,
            repository,
            unitOfWork,
            AuditEventTypes.PersonnelFileUpdated,
            cancellationToken);
    }
}

internal sealed class ActivatePersonnelFileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivatePersonnelFileCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        ActivatePersonnelFileCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetByIdAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileActivated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Reactivate,
                    $"Activated personnel file {personnelFile.FullName}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivatePersonnelFileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivatePersonnelFileCommand, PersonnelFileResponse>
{
    public async Task<Result<PersonnelFileResponse>> Handle(
        InactivatePersonnelFileCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetByIdAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileInactivated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Deactivate,
                    $"Inactivated personnel file {personnelFile.FullName}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UploadPersonnelFileDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UploadPersonnelFileDocumentCommand, PersonnelFileDocumentMetadataResponse>
{
    public async Task<Result<PersonnelFileDocumentMetadataResponse>> Handle(
        UploadPersonnelFileDocumentCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(authorizationResult.Error);
        }

        if (command.FileData.Length > PersonnelFileValidationRules.MaxDocumentFileSizeBytes)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(PersonnelFileErrors.DocumentFileTooLarge);
        }

        if (!PersonnelFileValidationRules.IsAllowedDocumentExtension(command.FileName) ||
            !PersonnelFileValidationRules.IsAllowedDocumentContentType(command.FileName, command.ContentType))
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(PersonnelFileErrors.DocumentContentTypeUnsupported);
        }

        var personnelFile = await repository.GetByIdAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var shaBytes = SHA256.HashData(command.FileData);
        var sha256 = Convert.ToHexString(shaBytes).ToLowerInvariant();

        PersonnelFileDocument document;
        try
        {
            document = PersonnelFileDocument.Create(
                command.DocumentType,
                command.Observations,
                command.DeliveryDate,
                command.LoanDate,
                command.ReturnDate,
                command.FileName,
                command.ContentType,
                command.FileData.Length,
                sha256,
                command.FileData);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(PersonnelFileErrors.DocumentLoanDatesInvalid);
        }

        var before = await repository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file response could not be resolved before document upload.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddDocument(document);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file response could not be resolved after document upload.");

            var uploaded = after.Documents.Single(item => item.Id == document.PublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileDocumentUploaded,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Uploaded document {uploaded.FileName} for personnel file {personnelFile.FullName}.",
                    Before: before,
                    After: uploaded),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileDocumentMetadataResponse>.Success(uploaded);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivatePersonnelFileDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivatePersonnelFileDocumentCommand, PersonnelFileDocumentMetadataResponse>
{
    public async Task<Result<PersonnelFileDocumentMetadataResponse>> Handle(
        InactivatePersonnelFileDocumentCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(authorizationResult.Error);
        }

        var document = await repository.GetDocumentByIdAsync(command.DocumentId, cancellationToken);
        if (document is null)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(
                await repository.DocumentExistsOutsideTenantAsync(command.DocumentId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.DocumentNotFound);
        }

        if (document.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            document.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var download = await repository.GetDocumentDownloadByIdAsync(command.DocumentId, cancellationToken)
                ?? throw new InvalidOperationException("Document could not be resolved after inactivation.");

            var response = new PersonnelFileDocumentMetadataResponse(
                download.Id,
                DocumentType: string.Empty,
                Observations: null,
                DeliveryDate: null,
                LoanDate: null,
                ReturnDate: null,
                download.FileName,
                download.ContentType,
                download.FileData.Length,
                download.IsActive,
                download.ConcurrencyToken,
                CreatedAtUtc: DateTime.UtcNow,
                ModifiedAtUtc: DateTime.UtcNow);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileDocumentInactivated,
                    AuditEntityTypes.PersonnelFile,
                    download.PersonnelFileId,
                    download.FileName,
                    AuditActions.Deactivate,
                    $"Inactivated personnel file document {download.FileName}.",
                    After: new { documentId = download.Id, isActive = download.IsActive }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileDocumentMetadataResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileObservationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileObservationCommand, PersonnelFileObservationResponse>
{
    public async Task<Result<PersonnelFileObservationResponse>> Handle(
        AddPersonnelFileObservationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileObservationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileObservationResponse>.Failure(authorizationResult.Error);
        }

        if (!Guid.TryParse(currentUserService.UserId, out var authorId))
        {
            return Result<PersonnelFileObservationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var personnelFile = await repository.GetByIdAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileObservationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileObservationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var observation = PersonnelFileObservation.Create(authorId, command.Note);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddObservation(observation);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = new PersonnelFileObservationResponse(
                observation.PublicId,
                authorId,
                observation.Note,
                observation.CreatedUtc);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileObservationAdded,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added observation for personnel file {personnelFile.FullName}.",
                    After: response),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileObservationResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class CreatePersonnelCustomFieldDefinitionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePersonnelCustomFieldDefinitionCommand, PersonnelCustomFieldDefinitionResponse>
{
    public async Task<Result<PersonnelCustomFieldDefinitionResponse>> Handle(
        CreatePersonnelCustomFieldDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelCustomFieldDefinitionResponse>.Failure(authorizationResult.Error);
        }

        var normalizedKey = command.Key.Trim().ToUpperInvariant();
        if (await repository.CustomFieldKeyExistsAsync(command.CompanyId, normalizedKey, excludingId: null, cancellationToken))
        {
            return Result<PersonnelCustomFieldDefinitionResponse>.Failure(PersonnelFileErrors.CustomFieldKeyConflict);
        }

        var definition = PersonnelFileCustomFieldDefinition.Create(
            command.Key,
            command.Label,
            command.FieldType,
            command.IsRequired,
            command.IsActive,
            command.OptionsJson,
            command.SortOrder);
        definition.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddCustomFieldDefinition(definition);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var created = await repository.GetCustomFieldDefinitionsAsync(command.CompanyId, null, cancellationToken);
            var response = created.Single(item => item.Id == definition.PublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelCustomFieldDefinitionCreated,
                    AuditEntityTypes.PersonnelFile,
                    definition.PublicId,
                    definition.Key,
                    AuditActions.Create,
                    $"Created personnel custom field definition {definition.Key}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelCustomFieldDefinitionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelCustomFieldDefinitionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelCustomFieldDefinitionCommand, PersonnelCustomFieldDefinitionResponse>
{
    public async Task<Result<PersonnelCustomFieldDefinitionResponse>> Handle(
        UpdatePersonnelCustomFieldDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelCustomFieldDefinitionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelCustomFieldDefinitionResponse>.Failure(authorizationResult.Error);
        }

        var definition = await repository.GetCustomFieldDefinitionByIdAsync(command.DefinitionId, cancellationToken);
        if (definition is null)
        {
            return Result<PersonnelCustomFieldDefinitionResponse>.Failure(PersonnelFileErrors.CustomFieldDefinitionNotFound);
        }

        if (definition.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelCustomFieldDefinitionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var normalizedKey = command.Key.Trim().ToUpperInvariant();
        if (await repository.CustomFieldKeyExistsAsync(definition.TenantId, normalizedKey, definition.Id, cancellationToken))
        {
            return Result<PersonnelCustomFieldDefinitionResponse>.Failure(PersonnelFileErrors.CustomFieldKeyConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            definition.Update(
                command.Key,
                command.Label,
                command.FieldType,
                command.IsRequired,
                command.IsActive,
                command.OptionsJson,
                command.SortOrder);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var updated = await repository.GetCustomFieldDefinitionsAsync(definition.TenantId, null, cancellationToken);
            var response = updated.Single(item => item.Id == definition.PublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelCustomFieldDefinitionUpdated,
                    AuditEntityTypes.PersonnelFile,
                    definition.PublicId,
                    definition.Key,
                    AuditActions.Update,
                    $"Updated personnel custom field definition {definition.Key}.",
                    After: response),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelCustomFieldDefinitionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
