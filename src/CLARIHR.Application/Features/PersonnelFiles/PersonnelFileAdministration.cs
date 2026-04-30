using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileListItemResponse(
    Guid Id,
    Guid CompanyId,
    PersonnelFileRecordType RecordType,
    PersonnelFileLifecycleStatus LifecycleStatus,
    string FullName,
    int Age,
    string? MaritalStatusCode,
    string? MaritalStatusName,
    string? ProfessionCode,
    string? ProfessionName,
    Guid? OrgUnitId,
    Guid? AssignedPositionSlotId,
    Guid? LinkedUserId,
    bool IsActive,
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
    Guid? BankPublicId,
    string BankCode,
    string? BankName,
    string? BankAlias,
    string? SwiftCode,
    string? RoutingCode,
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
    Guid PublicId,
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
    Guid PublicId,
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
    string? FileUrl,
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

public sealed record PersonnelFileShellResponse(
    Guid Id,
    Guid CompanyId,
    PersonnelFileRecordType RecordType,
    PersonnelFileLifecycleStatus LifecycleStatus,
    string FullName,
    string? PhotoUrl,
    bool IsActive,
    Guid? OrgUnitId,
    Guid? AssignedPositionSlotId,
    Guid? LinkedUserId,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record PersonnelFileSectionResult<TData>(
    TData Data,
    Guid PersonnelFileConcurrencyToken,
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

public enum PersonnelFileTrackedSection
{
    Identifications = 1,
    Addresses = 2,
    EmergencyContacts = 3,
    FamilyMembers = 4,
    Hobbies = 5,
    EmployeeRelations = 6,
    BankAccounts = 7,
    Associations = 8,
    Educations = 9,
    Languages = 10,
    Trainings = 11,
    PreviousEmployments = 12,
    References = 13,
    Documents = 14
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

public sealed record GetPersonnelFileByIdQuery(Guid PersonnelFileId) : IQuery<PersonnelFileShellResponse>;

public sealed record GetPersonnelFilePersonalInfoQuery(Guid PersonnelFileId) : IQuery<PersonnelFilePersonalInfoResponse>;

public sealed record GetPersonnelFileIdentificationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileIdentificationResponse>>;

public sealed record GetPersonnelFileAddressesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileAddressResponse>>;

public sealed record GetPersonnelFileEmergencyContactsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>;

public sealed record GetPersonnelFileFamilyMembersQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>;

public sealed record GetPersonnelFileHobbiesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileHobbyResponse>>;

public sealed record GetPersonnelFileEmployeeRelationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>;

public sealed record GetPersonnelFileBankAccountsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileBankAccountResponse>>;

public sealed record GetPersonnelFileAssociationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileAssociationResponse>>;

public sealed record GetPersonnelFileEducationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileEducationResponse>>;

public sealed record GetPersonnelFileLanguagesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileLanguageResponse>>;

public sealed record GetPersonnelFileTrainingsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileTrainingResponse>>;

public sealed record GetPersonnelFilePreviousEmploymentsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>;

public sealed record GetPersonnelFileReferencesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileReferenceResponse>>;

public sealed record GetPersonnelFileDocumentsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>;

public sealed record GetPersonnelFileObservationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileObservationResponse>>;

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
    string? CustomDataJson)
    : ICommand<PersonnelFileShellResponse>;

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
    : ICommand<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>;

public sealed record AddPersonnelFileIdentificationCommand(
    Guid PersonnelFileId,
    IdentificationInput Identification,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileIdentificationResponse>;

public sealed record UpdatePersonnelFileIdentificationCommand(
    Guid PersonnelFileId,
    Guid IdentificationPublicId,
    IdentificationInput Identification,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileIdentificationResponse>;

public sealed record DeletePersonnelFileIdentificationCommand(
    Guid PersonnelFileId,
    Guid IdentificationPublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFileAddressCommand(
    Guid PersonnelFileId,
    AddressInput Address,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileAddressResponse>;

public sealed record UpdatePersonnelFileAddressCommand(
    Guid PersonnelFileId,
    Guid AddressPublicId,
    AddressInput Address,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileAddressResponse>;

public sealed record DeletePersonnelFileAddressCommand(
    Guid PersonnelFileId,
    Guid AddressPublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFileEmergencyContactCommand(
    Guid PersonnelFileId,
    EmergencyContactInput EmergencyContact,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEmergencyContactResponse>;

public sealed record UpdatePersonnelFileEmergencyContactCommand(
    Guid PersonnelFileId,
    Guid EmergencyContactPublicId,
    EmergencyContactInput EmergencyContact,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEmergencyContactResponse>;

public sealed record DeletePersonnelFileEmergencyContactCommand(
    Guid PersonnelFileId,
    Guid EmergencyContactPublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFileFamilyMemberCommand(
    Guid PersonnelFileId,
    FamilyMemberInput FamilyMember,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileFamilyMemberResponse>;

public sealed record UpdatePersonnelFileFamilyMemberCommand(
    Guid PersonnelFileId,
    Guid FamilyMemberPublicId,
    FamilyMemberInput FamilyMember,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileFamilyMemberResponse>;

public sealed record DeletePersonnelFileFamilyMemberCommand(
    Guid PersonnelFileId,
    Guid FamilyMemberPublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFileHobbyCommand(
    Guid PersonnelFileId,
    HobbyInput Hobby,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileHobbyResponse>;

public sealed record UpdatePersonnelFileHobbyCommand(
    Guid PersonnelFileId,
    Guid HobbyPublicId,
    HobbyInput Hobby,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileHobbyResponse>;

public sealed record DeletePersonnelFileHobbyCommand(
    Guid PersonnelFileId,
    Guid HobbyPublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFileEmployeeRelationCommand(
    Guid PersonnelFileId,
    EmployeeRelationInput Relation,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEmployeeRelationResponse>;

public sealed record UpdatePersonnelFileEmployeeRelationCommand(
    Guid PersonnelFileId,
    Guid RelationPublicId,
    EmployeeRelationInput Relation,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEmployeeRelationResponse>;

public sealed record DeletePersonnelFileEmployeeRelationCommand(
    Guid PersonnelFileId,
    Guid RelationPublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFileBankAccountCommand(
    Guid PersonnelFileId,
    BankAccountInput BankAccount,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileBankAccountResponse>;

public sealed record UpdatePersonnelFileBankAccountCommand(
    Guid PersonnelFileId,
    Guid BankAccountPublicId,
    BankAccountInput BankAccount,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileBankAccountResponse>;

public sealed record DeletePersonnelFileBankAccountCommand(
    Guid PersonnelFileId,
    Guid BankAccountPublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFileAssociationCommand(
    Guid PersonnelFileId,
    AssociationInput Association,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileAssociationResponse>;

public sealed record UpdatePersonnelFileAssociationCommand(
    Guid PersonnelFileId,
    Guid AssociationPublicId,
    AssociationInput Association,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileAssociationResponse>;

public sealed record DeletePersonnelFileAssociationCommand(
    Guid PersonnelFileId,
    Guid AssociationPublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFileEducationCommand(
    Guid PersonnelFileId,
    EducationInput Education,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEducationResponse>;

public sealed record UpdatePersonnelFileEducationCommand(
    Guid PersonnelFileId,
    Guid EducationPublicId,
    EducationInput Education,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEducationResponse>;

public sealed record DeletePersonnelFileEducationCommand(
    Guid PersonnelFileId,
    Guid EducationPublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFileLanguageCommand(
    Guid PersonnelFileId,
    LanguageInput Language,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileLanguageResponse>;

public sealed record UpdatePersonnelFileLanguageCommand(
    Guid PersonnelFileId,
    Guid LanguagePublicId,
    LanguageInput Language,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileLanguageResponse>;

public sealed record DeletePersonnelFileLanguageCommand(
    Guid PersonnelFileId,
    Guid LanguagePublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFileTrainingCommand(
    Guid PersonnelFileId,
    TrainingInput Training,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileTrainingResponse>;

public sealed record UpdatePersonnelFileTrainingCommand(
    Guid PersonnelFileId,
    Guid TrainingPublicId,
    TrainingInput Training,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileTrainingResponse>;

public sealed record DeletePersonnelFileTrainingCommand(
    Guid PersonnelFileId,
    Guid TrainingPublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFilePreviousEmploymentCommand(
    Guid PersonnelFileId,
    PreviousEmploymentInput PreviousEmployment,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFilePreviousEmploymentResponse>;

public sealed record UpdatePersonnelFilePreviousEmploymentCommand(
    Guid PersonnelFileId,
    Guid PreviousEmploymentPublicId,
    PreviousEmploymentInput PreviousEmployment,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFilePreviousEmploymentResponse>;

public sealed record DeletePersonnelFilePreviousEmploymentCommand(
    Guid PersonnelFileId,
    Guid PreviousEmploymentPublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record AddPersonnelFileReferenceCommand(
    Guid PersonnelFileId,
    ReferenceInput Reference,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileReferenceResponse>;

public sealed record UpdatePersonnelFileReferenceCommand(
    Guid PersonnelFileId,
    Guid ReferencePublicId,
    ReferenceInput Reference,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileReferenceResponse>;

public sealed record DeletePersonnelFileReferenceCommand(
    Guid PersonnelFileId,
    Guid ReferencePublicId,
    Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record ReplacePersonnelFileDocumentsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<PersonnelFileDocumentInput> Documents,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>;

public sealed record ActivatePersonnelFileCommand(Guid PersonnelFileId, Guid ConcurrencyToken)
    : ICommand<PersonnelFileShellResponse>;

public sealed record InactivatePersonnelFileCommand(Guid PersonnelFileId, Guid ConcurrencyToken)
    : ICommand<PersonnelFileShellResponse>;

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
    Guid BankPublicId,
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

public sealed record PersonnelFileDocumentInput(
    Guid? DocumentPublicId,
    string DocumentType,
    string? Observations,
    DateTime? DeliveryDate,
    DateTime? LoanDate,
    DateTime? ReturnDate,
    string? FileKey,
    string? FileName,
    string? ContentType,
    byte[]? FileData);

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

internal sealed class AddPersonnelFileIdentificationCommandValidator : AbstractValidator<AddPersonnelFileIdentificationCommand>
{
    public AddPersonnelFileIdentificationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Identification).SetValidator(new IdentificationInputValidator());
    }
}

internal sealed class UpdatePersonnelFileIdentificationCommandValidator : AbstractValidator<UpdatePersonnelFileIdentificationCommand>
{
    public UpdatePersonnelFileIdentificationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.IdentificationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Identification).SetValidator(new IdentificationInputValidator());
    }
}

internal sealed class DeletePersonnelFileIdentificationCommandValidator : AbstractValidator<DeletePersonnelFileIdentificationCommand>
{
    public DeletePersonnelFileIdentificationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.IdentificationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileAddressCommandValidator : AbstractValidator<AddPersonnelFileAddressCommand>
{
    public AddPersonnelFileAddressCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Address).SetValidator(new AddressInputValidator());
    }
}

internal sealed class UpdatePersonnelFileAddressCommandValidator : AbstractValidator<UpdatePersonnelFileAddressCommand>
{
    public UpdatePersonnelFileAddressCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.AddressPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Address).SetValidator(new AddressInputValidator());
    }
}

internal sealed class DeletePersonnelFileAddressCommandValidator : AbstractValidator<DeletePersonnelFileAddressCommand>
{
    public DeletePersonnelFileAddressCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.AddressPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileEmergencyContactCommandValidator : AbstractValidator<AddPersonnelFileEmergencyContactCommand>
{
    public AddPersonnelFileEmergencyContactCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.EmergencyContact).SetValidator(new EmergencyContactInputValidator());
    }
}

internal sealed class UpdatePersonnelFileEmergencyContactCommandValidator : AbstractValidator<UpdatePersonnelFileEmergencyContactCommand>
{
    public UpdatePersonnelFileEmergencyContactCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EmergencyContactPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.EmergencyContact).SetValidator(new EmergencyContactInputValidator());
    }
}

internal sealed class DeletePersonnelFileEmergencyContactCommandValidator : AbstractValidator<DeletePersonnelFileEmergencyContactCommand>
{
    public DeletePersonnelFileEmergencyContactCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EmergencyContactPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileFamilyMemberCommandValidator : AbstractValidator<AddPersonnelFileFamilyMemberCommand>
{
    public AddPersonnelFileFamilyMemberCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.FamilyMember).SetValidator(new FamilyMemberInputValidator());
    }
}

internal sealed class UpdatePersonnelFileFamilyMemberCommandValidator : AbstractValidator<UpdatePersonnelFileFamilyMemberCommand>
{
    public UpdatePersonnelFileFamilyMemberCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.FamilyMemberPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.FamilyMember).SetValidator(new FamilyMemberInputValidator());
    }
}

internal sealed class DeletePersonnelFileFamilyMemberCommandValidator : AbstractValidator<DeletePersonnelFileFamilyMemberCommand>
{
    public DeletePersonnelFileFamilyMemberCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.FamilyMemberPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileHobbyCommandValidator : AbstractValidator<AddPersonnelFileHobbyCommand>
{
    public AddPersonnelFileHobbyCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Hobby).SetValidator(new HobbyInputValidator());
    }
}

internal sealed class UpdatePersonnelFileHobbyCommandValidator : AbstractValidator<UpdatePersonnelFileHobbyCommand>
{
    public UpdatePersonnelFileHobbyCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.HobbyPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Hobby).SetValidator(new HobbyInputValidator());
    }
}

internal sealed class DeletePersonnelFileHobbyCommandValidator : AbstractValidator<DeletePersonnelFileHobbyCommand>
{
    public DeletePersonnelFileHobbyCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.HobbyPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileEmployeeRelationCommandValidator : AbstractValidator<AddPersonnelFileEmployeeRelationCommand>
{
    public AddPersonnelFileEmployeeRelationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Relation).SetValidator(new EmployeeRelationInputValidator());
    }
}

internal sealed class UpdatePersonnelFileEmployeeRelationCommandValidator : AbstractValidator<UpdatePersonnelFileEmployeeRelationCommand>
{
    public UpdatePersonnelFileEmployeeRelationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RelationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Relation).SetValidator(new EmployeeRelationInputValidator());
    }
}

internal sealed class DeletePersonnelFileEmployeeRelationCommandValidator : AbstractValidator<DeletePersonnelFileEmployeeRelationCommand>
{
    public DeletePersonnelFileEmployeeRelationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RelationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileBankAccountCommandValidator : AbstractValidator<AddPersonnelFileBankAccountCommand>
{
    public AddPersonnelFileBankAccountCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.BankAccount).SetValidator(new BankAccountInputValidator());
    }
}

internal sealed class UpdatePersonnelFileBankAccountCommandValidator : AbstractValidator<UpdatePersonnelFileBankAccountCommand>
{
    public UpdatePersonnelFileBankAccountCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.BankAccountPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.BankAccount).SetValidator(new BankAccountInputValidator());
    }
}

internal sealed class DeletePersonnelFileBankAccountCommandValidator : AbstractValidator<DeletePersonnelFileBankAccountCommand>
{
    public DeletePersonnelFileBankAccountCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.BankAccountPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileAssociationCommandValidator : AbstractValidator<AddPersonnelFileAssociationCommand>
{
    public AddPersonnelFileAssociationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Association).SetValidator(new AssociationInputValidator());
    }
}

internal sealed class UpdatePersonnelFileAssociationCommandValidator : AbstractValidator<UpdatePersonnelFileAssociationCommand>
{
    public UpdatePersonnelFileAssociationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.AssociationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Association).SetValidator(new AssociationInputValidator());
    }
}

internal sealed class DeletePersonnelFileAssociationCommandValidator : AbstractValidator<DeletePersonnelFileAssociationCommand>
{
    public DeletePersonnelFileAssociationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.AssociationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileEducationCommandValidator : AbstractValidator<AddPersonnelFileEducationCommand>
{
    public AddPersonnelFileEducationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Education).SetValidator(new EducationInputValidator());
    }
}

internal sealed class UpdatePersonnelFileEducationCommandValidator : AbstractValidator<UpdatePersonnelFileEducationCommand>
{
    public UpdatePersonnelFileEducationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EducationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Education).SetValidator(new EducationInputValidator());
    }
}

internal sealed class DeletePersonnelFileEducationCommandValidator : AbstractValidator<DeletePersonnelFileEducationCommand>
{
    public DeletePersonnelFileEducationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EducationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileLanguageCommandValidator : AbstractValidator<AddPersonnelFileLanguageCommand>
{
    public AddPersonnelFileLanguageCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Language).SetValidator(new LanguageInputValidator());
    }
}

internal sealed class UpdatePersonnelFileLanguageCommandValidator : AbstractValidator<UpdatePersonnelFileLanguageCommand>
{
    public UpdatePersonnelFileLanguageCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.LanguagePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Language).SetValidator(new LanguageInputValidator());
    }
}

internal sealed class DeletePersonnelFileLanguageCommandValidator : AbstractValidator<DeletePersonnelFileLanguageCommand>
{
    public DeletePersonnelFileLanguageCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.LanguagePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileTrainingCommandValidator : AbstractValidator<AddPersonnelFileTrainingCommand>
{
    public AddPersonnelFileTrainingCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Training).SetValidator(new TrainingInputValidator());
    }
}

internal sealed class UpdatePersonnelFileTrainingCommandValidator : AbstractValidator<UpdatePersonnelFileTrainingCommand>
{
    public UpdatePersonnelFileTrainingCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.TrainingPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Training).SetValidator(new TrainingInputValidator());
    }
}

internal sealed class DeletePersonnelFileTrainingCommandValidator : AbstractValidator<DeletePersonnelFileTrainingCommand>
{
    public DeletePersonnelFileTrainingCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.TrainingPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFilePreviousEmploymentCommandValidator : AbstractValidator<AddPersonnelFilePreviousEmploymentCommand>
{
    public AddPersonnelFilePreviousEmploymentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.PreviousEmployment).SetValidator(new PreviousEmploymentInputValidator());
    }
}

internal sealed class UpdatePersonnelFilePreviousEmploymentCommandValidator : AbstractValidator<UpdatePersonnelFilePreviousEmploymentCommand>
{
    public UpdatePersonnelFilePreviousEmploymentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.PreviousEmploymentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.PreviousEmployment).SetValidator(new PreviousEmploymentInputValidator());
    }
}

internal sealed class DeletePersonnelFilePreviousEmploymentCommandValidator : AbstractValidator<DeletePersonnelFilePreviousEmploymentCommand>
{
    public DeletePersonnelFilePreviousEmploymentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.PreviousEmploymentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileReferenceCommandValidator : AbstractValidator<AddPersonnelFileReferenceCommand>
{
    public AddPersonnelFileReferenceCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reference).SetValidator(new ReferenceInputValidator());
    }
}

internal sealed class UpdatePersonnelFileReferenceCommandValidator : AbstractValidator<UpdatePersonnelFileReferenceCommand>
{
    public UpdatePersonnelFileReferenceCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ReferencePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reference).SetValidator(new ReferenceInputValidator());
    }
}

internal sealed class DeletePersonnelFileReferenceCommandValidator : AbstractValidator<DeletePersonnelFileReferenceCommand>
{
    public DeletePersonnelFileReferenceCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ReferencePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
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

internal sealed class ReplacePersonnelFileDocumentsCommandValidator : AbstractValidator<ReplacePersonnelFileDocumentsCommand>
{
    public ReplacePersonnelFileDocumentsCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Documents)
            .ChildRules(item =>
            {
                item.RuleFor(document => document.DocumentType).NotEmpty().MaximumLength(100);
                item.RuleFor(document => document.FileKey).MaximumLength(120);
                item.RuleFor(document => document.FileName).MaximumLength(260);
                item.RuleFor(document => document.ContentType).MaximumLength(200);
                item.RuleFor(document => document)
                    .Must(static document => !document.LoanDate.HasValue || !document.ReturnDate.HasValue || document.ReturnDate.Value.Date >= document.LoanDate.Value.Date)
                    .WithMessage(PersonnelFileErrors.DocumentLoanDatesInvalid.Message);
                item.RuleFor(document => document)
                    .Must(static document => document.DocumentPublicId.HasValue || document.FileData is not null)
                    .WithMessage("FileData is required for new documents.");
            });
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
        RuleFor(input => input.BankPublicId).NotEmpty();
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
    : IQueryHandler<GetPersonnelFileByIdQuery, PersonnelFileShellResponse>
{
    public async Task<Result<PersonnelFileShellResponse>> Handle(
        GetPersonnelFileByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileShellResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileShellResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetShellByIdAsync(query.PersonnelFileId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = PersonnelFilePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);
            return Result<PersonnelFileShellResponse>.Success(response);
        }

        return Result<PersonnelFileShellResponse>.Failure(
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

        var personnelFile = await repository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
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

internal sealed class GetPersonnelFileBankAccountsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileBankAccountsQuery, IReadOnlyCollection<PersonnelFileBankAccountResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileBankAccountResponse>>> Handle(
        GetPersonnelFileBankAccountsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileBankAccountResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetBankAccountsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileBankAccountResponse>>.Success(response);
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

internal sealed class GetPersonnelFileObservationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext)
    : GetPersonnelFileSectionQueryHandlerBase,
      IQueryHandler<GetPersonnelFileObservationsQuery, IReadOnlyCollection<PersonnelFileObservationResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileObservationResponse>>> Handle(
        GetPersonnelFileObservationsQuery query,
        CancellationToken cancellationToken)
    {
        var failure = await EnsureCanReadAsync<IReadOnlyCollection<PersonnelFileObservationResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await repository.GetObservationsAsync(query.PersonnelFileId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileObservationResponse>>.Success(response);
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

        var includedSections = PersonnelFilePrintSections.Resolve(query.Sections);
        PersonnelFileResponse? response;
        if (query.Sections is null || query.Sections.Count == 0)
        {
            response = await repository.GetResponseByIdAsync(query.PersonnelFileId, cancellationToken);
        }
        else
        {
            response = await BuildSectionScopedResponseAsync(query.PersonnelFileId, includedSections, cancellationToken);
        }

        if (response is null)
        {
            return Result<PersonnelFilePrintResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(query.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : PersonnelFileErrors.NotFound);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        response = PersonnelFilePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);
        var filtered = PersonnelFilePrintSections.Filter(response, includedSections);

        return Result<PersonnelFilePrintResponse>.Success(
            new PersonnelFilePrintResponse(DateTime.UtcNow, includedSections, filtered));
    }

    private async Task<PersonnelFileResponse?> BuildSectionScopedResponseAsync(
        Guid personnelFileId,
        IReadOnlyCollection<string> includedSections,
        CancellationToken cancellationToken)
    {
        var personalInfo = await repository.GetPersonalInfoAsync(personnelFileId, cancellationToken);
        if (personalInfo is null)
        {
            return null;
        }

        var include = includedSections.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var identifications = include.Contains(PersonnelFilePrintSections.Identifications)
            ? await repository.GetIdentificationsAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileIdentificationResponse>();
        var addresses = include.Contains(PersonnelFilePrintSections.Addresses)
            ? await repository.GetAddressesAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileAddressResponse>();
        var emergencyContacts = include.Contains(PersonnelFilePrintSections.EmergencyContacts)
            ? await repository.GetEmergencyContactsAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileEmergencyContactResponse>();
        var familyMembers = include.Contains(PersonnelFilePrintSections.FamilyMembers)
            ? await repository.GetFamilyMembersAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileFamilyMemberResponse>();
        var hobbies = include.Contains(PersonnelFilePrintSections.Hobbies)
            ? await repository.GetHobbiesAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileHobbyResponse>();
        var employeeRelations = include.Contains(PersonnelFilePrintSections.EmployeeRelations)
            ? await repository.GetEmployeeRelationsAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileEmployeeRelationResponse>();
        var bankAccounts = include.Contains(PersonnelFilePrintSections.BankAccounts)
            ? await repository.GetBankAccountsAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileBankAccountResponse>();
        var associations = include.Contains(PersonnelFilePrintSections.Associations)
            ? await repository.GetAssociationsAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileAssociationResponse>();
        var educations = include.Contains(PersonnelFilePrintSections.Educations)
            ? await repository.GetEducationsAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileEducationResponse>();
        var languages = include.Contains(PersonnelFilePrintSections.Languages)
            ? await repository.GetLanguagesAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileLanguageResponse>();
        var trainings = include.Contains(PersonnelFilePrintSections.Trainings)
            ? await repository.GetTrainingsAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileTrainingResponse>();
        var previousEmployments = include.Contains(PersonnelFilePrintSections.PreviousEmployments)
            ? await repository.GetPreviousEmploymentsAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFilePreviousEmploymentResponse>();
        var references = include.Contains(PersonnelFilePrintSections.References)
            ? await repository.GetReferencesAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileReferenceResponse>();
        var documents = include.Contains(PersonnelFilePrintSections.Documents)
            ? await repository.GetDocumentsAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileDocumentMetadataResponse>();
        var observations = include.Contains(PersonnelFilePrintSections.Observations)
            ? await repository.GetObservationsAsync(personnelFileId, cancellationToken)
            : Array.Empty<PersonnelFileObservationResponse>();

        return new PersonnelFileResponse(
            personalInfo.Id,
            personalInfo.CompanyId,
            personalInfo.RecordType,
            personalInfo.LifecycleStatus,
            personalInfo.FirstName,
            personalInfo.LastName,
            personalInfo.FullName,
            personalInfo.BirthDate,
            personalInfo.Age,
            personalInfo.MaritalStatusCode,
            personalInfo.MaritalStatusName,
            personalInfo.ProfessionCode,
            personalInfo.ProfessionName,
            personalInfo.Nationality,
            personalInfo.PersonalEmail,
            personalInfo.InstitutionalEmail,
            personalInfo.PersonalPhone,
            personalInfo.InstitutionalPhone,
            personalInfo.BirthCountryCode,
            personalInfo.BirthCountryName,
            personalInfo.BirthDepartmentCode,
            personalInfo.BirthDepartmentName,
            personalInfo.BirthMunicipalityCode,
            personalInfo.BirthMunicipalityName,
            personalInfo.PhotoUrl,
            personalInfo.OrgUnitId,
            personalInfo.AssignedPositionSlotId,
            personalInfo.LinkedUserId,
            personalInfo.CustomDataJson,
            personalInfo.IsActive,
            personalInfo.ConcurrencyToken,
            personalInfo.CreatedAtUtc,
            personalInfo.ModifiedAtUtc,
            identifications,
            addresses,
            emergencyContacts,
            familyMembers,
            hobbies,
            employeeRelations,
            bankAccounts,
            associations,
            educations,
            languages,
            trainings,
            previousEmployments,
            references,
            documents,
            observations);
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

    public static PersonnelFileShellResponse ApplyAllowedActions(
        PersonnelFileShellResponse response,
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
    : ICommandHandler<CreatePersonnelFileCommand, PersonnelFileShellResponse>
{
    private sealed record PersonnelFileLifecycleAuditSnapshot(
        Guid PublicId,
        Guid CompanyId,
        PersonnelFileRecordType RecordType,
        PersonnelFileLifecycleStatus LifecycleStatus,
        string FullName,
        string? PhotoUrl,
        bool IsActive,
        Guid? OrgUnitId,
        Guid? AssignedPositionSlotId,
        Guid? LinkedUserId,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    public async Task<Result<PersonnelFileShellResponse>> Handle(
        CreatePersonnelFileCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileShellResponse>.Failure(authorizationResult.Error);
        }

        var definitions = await repository.GetCustomFieldDefinitionsAsync(command.CompanyId, isActive: true, cancellationToken);
        var customDataValidation = PersonnelFileValidationRules.ValidateCustomData(definitions, command.CustomDataJson);
        if (customDataValidation != Error.None)
        {
            return Result<PersonnelFileShellResponse>.Failure(customDataValidation);
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
            return Result<PersonnelFileShellResponse>.Failure(personalInfoCatalogValidation);
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
            command.CustomDataJson);
        personnelFile.SetTenantId(command.CompanyId);

        var photoWritePlanResult = await profilePhotoService.PrepareWriteAsync(
            command.CompanyId,
            personnelFile.PublicId,
            command.PhotoUrl,
            currentPersistedPhotoUrl: null,
            cancellationToken);
        if (photoWritePlanResult.IsFailure)
        {
            return Result<PersonnelFileShellResponse>.Failure(photoWritePlanResult.Error);
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

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var shellResponse = CreateShellResponse(personnelFile);
            var auditSnapshot = CreateAuditSnapshot(personnelFile);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileCreated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Create,
                    $"Created personnel file {personnelFile.FullName}.",
                    After: auditSnapshot),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            await profilePhotoService.CleanupAfterPersistenceSuccessAsync(photoWritePlan, cancellationToken);
            return Result<PersonnelFileShellResponse>.Success(shellResponse);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await profilePhotoService.CleanupAfterPersistenceFailureAsync(photoWritePlan, cancellationToken);
            throw;
        }
    }

    private static PersonnelFileShellResponse CreateShellResponse(PersonnelFile personnelFile) =>
        new(
            personnelFile.PublicId,
            personnelFile.TenantId,
            personnelFile.RecordType,
            personnelFile.LifecycleStatus,
            personnelFile.FullName,
            personnelFile.PhotoUrl,
            personnelFile.IsActive,
            personnelFile.OrgUnitPublicId,
            personnelFile.AssignedPositionSlotPublicId,
            personnelFile.LinkedUserPublicId,
            personnelFile.ConcurrencyToken,
            personnelFile.CreatedUtc,
            personnelFile.ModifiedUtc);

    private static PersonnelFileLifecycleAuditSnapshot CreateAuditSnapshot(PersonnelFile personnelFile) =>
        new(
            personnelFile.PublicId,
            personnelFile.TenantId,
            personnelFile.RecordType,
            personnelFile.LifecycleStatus,
            personnelFile.FullName,
            personnelFile.PhotoUrl,
            personnelFile.IsActive,
            personnelFile.OrgUnitPublicId,
            personnelFile.AssignedPositionSlotPublicId,
            personnelFile.LinkedUserPublicId,
            personnelFile.ConcurrencyToken,
            personnelFile.CreatedUtc,
            personnelFile.ModifiedUtc);
}

internal sealed class UpdatePersonnelFilePersonalInfoCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    IPersonnelFileProfilePhotoService profilePhotoService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFilePersonalInfoCommand, PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>
{
    public async Task<Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>> Handle(
        UpdatePersonnelFilePersonalInfoCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForAccessCheckAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (personnelFile.RecordType != command.RecordType)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(PersonnelFileErrors.RecordTypeTransitionNotAllowed);
        }

        var definitions = await repository.GetCustomFieldDefinitionsAsync(personnelFile.TenantId, isActive: true, cancellationToken);
        var customDataValidation = PersonnelFileValidationRules.ValidateCustomData(definitions, command.CustomDataJson);
        if (customDataValidation != Error.None)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(customDataValidation);
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
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(personalInfoCatalogValidation);
        }

        var photoWritePlanResult = await profilePhotoService.PrepareWriteAsync(
            personnelFile.TenantId,
            personnelFile.PublicId,
            command.PhotoUrl,
            personnelFile.PhotoUrl,
            cancellationToken);
        if (photoWritePlanResult.IsFailure)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(photoWritePlanResult.Error);
        }

        var photoWritePlan = photoWritePlanResult.Value;

        var before = await repository.GetPersonalInfoAsync(personnelFile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file personal info could not be resolved before update.");

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
                return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(PersonnelFileErrors.ProvisioningFieldsLocked);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetPersonalInfoAsync(personnelFile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file personal info could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated personnel file {personnelFile.FullName} personal info.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PersonalInfo,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PersonalInfo,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await profilePhotoService.CleanupAfterPersistenceSuccessAsync(photoWritePlan, cancellationToken);

            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Success(
                ReplacePersonnelFileSectionCommandHandlerBase.CreateSectionResult(personnelFile, after));
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
    internal static PersonnelFileSectionResult<TSection> CreateSectionResult<TSection>(
        PersonnelFile personnelFile,
        TSection data) =>
        new(data, personnelFile.ConcurrencyToken, personnelFile.ModifiedUtc);

    protected static async Task<(Result<PersonnelFileSectionResult<TSection>>? Failure, PersonnelFile? File)> LoadForUpdateAsync<TSection>(
        Guid personnelFileId,
        Guid concurrencyToken,
        PersonnelFileTrackedSection trackedSection,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository repository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<PersonnelFileSectionResult<TSection>>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<PersonnelFileSectionResult<TSection>>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(personnelFileId, trackedSection, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<PersonnelFileSectionResult<TSection>>.Failure(
                    await repository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<PersonnelFileSectionResult<TSection>>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    protected static async Task<Result<PersonnelFileSectionResult<TSection>>> PersistSectionAsync<TSection>(
        PersonnelFile personnelFile,
        string sectionName,
        string auditMessage,
        Func<Guid, CancellationToken, Task<TSection>> sectionLoader,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        string eventType,
        CancellationToken cancellationToken)
    {
        var before = await sectionLoader(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await sectionLoader(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    eventType,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    auditMessage,
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = sectionName,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = sectionName,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileSectionResult<TSection>>.Success(CreateSectionResult(personnelFile, after));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileIdentificationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileIdentificationCommand, PersonnelFileIdentificationResponse>
{
    public async Task<Result<PersonnelFileIdentificationResponse>> Handle(
        AddPersonnelFileIdentificationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Identifications,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var normalizedIdentificationTypeCode = command.Identification.IdentificationTypeCode.Trim().ToUpperInvariant();
        var identificationTypeValidation = await PersonnelReferenceCatalogValidation.ValidateIdentificationTypeCodeAsync(
            repository,
            personnelFile.TenantId,
            normalizedIdentificationTypeCode,
            cancellationToken);
        if (identificationTypeValidation != Error.None)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(identificationTypeValidation);
        }

        var normalizedIdentificationNumber = command.Identification.IdentificationNumber.Trim().ToUpperInvariant();
        var exists = await repository.IdentificationExistsAsync(
            personnelFile.TenantId,
            normalizedIdentificationTypeCode,
            normalizedIdentificationNumber,
            excludingPersonnelFileId: null,
            cancellationToken);
        if (exists)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.IdentificationConflict);
        }

        var before = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);

        var identification = PersonnelFileIdentification.Create(
            normalizedIdentificationTypeCode,
            command.Identification.IdentificationNumber,
            command.Identification.IssuedDate,
            command.Identification.ExpiryDate,
            command.Identification.Issuer,
            command.Identification.IsPrimary);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddIdentification(identification);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == identification.PublicId)
                ?? throw new InvalidOperationException("Personnel file identification response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added identification for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileIdentificationResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileIdentificationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileIdentificationCommand, PersonnelFileIdentificationResponse>
{
    public async Task<Result<PersonnelFileIdentificationResponse>> Handle(
        UpdatePersonnelFileIdentificationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Identifications,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var normalizedIdentificationTypeCode = command.Identification.IdentificationTypeCode.Trim().ToUpperInvariant();
        var identificationTypeValidation = await PersonnelReferenceCatalogValidation.ValidateIdentificationTypeCodeAsync(
            repository,
            personnelFile.TenantId,
            normalizedIdentificationTypeCode,
            cancellationToken);
        if (identificationTypeValidation != Error.None)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(identificationTypeValidation);
        }

        var normalizedIdentificationNumber = command.Identification.IdentificationNumber.Trim().ToUpperInvariant();
        var exists = await repository.IdentificationExistsAsync(
            personnelFile.TenantId,
            normalizedIdentificationTypeCode,
            normalizedIdentificationNumber,
            excludingPersonnelFileId: personnelFile.Id,
            cancellationToken);
        if (exists)
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.IdentificationConflict);
        }

        var before = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateIdentification(
                command.IdentificationPublicId,
                normalizedIdentificationTypeCode,
                command.Identification.IdentificationNumber,
                command.Identification.IssuedDate,
                command.Identification.ExpiryDate,
                command.Identification.Issuer,
                command.Identification.IsPrimary);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.IdentificationPublicId)
                ?? throw new InvalidOperationException("Personnel file identification response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated identification for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileIdentificationResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileIdentificationResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileIdentificationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileIdentificationCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileIdentificationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Identifications,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveIdentification(command.IdentificationPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetIdentificationsAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted identification for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Identifications,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileAddressCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileAddressCommand, PersonnelFileAddressResponse>
{
    public async Task<Result<PersonnelFileAddressResponse>> Handle(
        AddPersonnelFileAddressCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileAddressResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileAddressResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Addresses,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileAddressResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAddressResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);
        var address = PersonnelFileAddress.Create(
            command.Address.AddressLine,
            command.Address.Country,
            command.Address.Department,
            command.Address.Municipality,
            command.Address.PostalCode,
            command.Address.IsCurrent);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddAddress(address);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == address.PublicId)
                ?? throw new InvalidOperationException("Personnel file address response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added address for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileAddressResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileAddressCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileAddressCommand, PersonnelFileAddressResponse>
{
    public async Task<Result<PersonnelFileAddressResponse>> Handle(
        UpdatePersonnelFileAddressCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileAddressResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileAddressResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Addresses,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileAddressResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAddressResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateAddress(
                command.AddressPublicId,
                command.Address.AddressLine,
                command.Address.Country,
                command.Address.Department,
                command.Address.Municipality,
                command.Address.PostalCode,
                command.Address.IsCurrent);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.AddressPublicId)
                ?? throw new InvalidOperationException("Personnel file address response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated address for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileAddressResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileAddressResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileAddressCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileAddressCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileAddressCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Addresses,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveAddress(command.AddressPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAddressesAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted address for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Addresses,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileEmergencyContactCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileEmergencyContactCommand, PersonnelFileEmergencyContactResponse>
{
    public async Task<Result<PersonnelFileEmergencyContactResponse>> Handle(
        AddPersonnelFileEmergencyContactCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileEmergencyContactResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileEmergencyContactResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.EmergencyContacts,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileEmergencyContactResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEmergencyContactResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetEmergencyContactsAsync(personnelFile.PublicId, cancellationToken);
        var emergencyContact = PersonnelFileEmergencyContact.Create(
            command.EmergencyContact.Name,
            command.EmergencyContact.Relationship,
            command.EmergencyContact.Phone,
            command.EmergencyContact.Address,
            command.EmergencyContact.Workplace);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddEmergencyContact(emergencyContact);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEmergencyContactsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == emergencyContact.PublicId)
                ?? throw new InvalidOperationException("Personnel file emergency contact response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added emergency contact for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmergencyContacts,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmergencyContacts,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEmergencyContactResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileEmergencyContactCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileEmergencyContactCommand, PersonnelFileEmergencyContactResponse>
{
    public async Task<Result<PersonnelFileEmergencyContactResponse>> Handle(
        UpdatePersonnelFileEmergencyContactCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileEmergencyContactResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileEmergencyContactResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.EmergencyContacts,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileEmergencyContactResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEmergencyContactResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetEmergencyContactsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateEmergencyContact(
                command.EmergencyContactPublicId,
                command.EmergencyContact.Name,
                command.EmergencyContact.Relationship,
                command.EmergencyContact.Phone,
                command.EmergencyContact.Address,
                command.EmergencyContact.Workplace);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEmergencyContactsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.EmergencyContactPublicId)
                ?? throw new InvalidOperationException("Personnel file emergency contact response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated emergency contact for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmergencyContacts,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmergencyContacts,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEmergencyContactResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileEmergencyContactResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileEmergencyContactCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileEmergencyContactCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileEmergencyContactCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.EmergencyContacts,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetEmergencyContactsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveEmergencyContact(command.EmergencyContactPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEmergencyContactsAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted emergency contact for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmergencyContacts,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmergencyContacts,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileFamilyMemberCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileFamilyMemberCommand, PersonnelFileFamilyMemberResponse>
{
    public async Task<Result<PersonnelFileFamilyMemberResponse>> Handle(
        AddPersonnelFileFamilyMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.FamilyMembers,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var kinshipCodeValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
            repository,
            personnelFile.TenantId,
            "kinshipCode",
            command.FamilyMember.KinshipCode,
            cancellationToken);
        if (kinshipCodeValidation != Error.None)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(kinshipCodeValidation);
        }

        var before = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);

        PersonnelFileFamilyMember familyMember;
        try
        {
            familyMember = PersonnelFileFamilyMember.Create(
                command.FamilyMember.FirstName,
                command.FamilyMember.LastName,
                command.FamilyMember.KinshipCode,
                command.FamilyMember.Nationality,
                command.FamilyMember.BirthDate,
                command.FamilyMember.Sex,
                command.FamilyMember.MaritalStatus,
                command.FamilyMember.Occupation,
                command.FamilyMember.DocumentType,
                command.FamilyMember.DocumentNumber,
                command.FamilyMember.Phone,
                command.FamilyMember.IsStudying,
                command.FamilyMember.StudyPlace,
                command.FamilyMember.AcademicLevel,
                command.FamilyMember.IsBeneficiary,
                command.FamilyMember.IsWorking,
                command.FamilyMember.Workplace,
                command.FamilyMember.JobTitle,
                command.FamilyMember.WorkPhone,
                command.FamilyMember.Salary,
                command.FamilyMember.IsDeceased,
                command.FamilyMember.DeceasedDate);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.FamilyMemberRuleViolation);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddFamilyMember(familyMember);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == familyMember.PublicId)
                ?? throw new InvalidOperationException("Personnel file family member response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added family member for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileFamilyMemberResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileFamilyMemberCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileFamilyMemberCommand, PersonnelFileFamilyMemberResponse>
{
    public async Task<Result<PersonnelFileFamilyMemberResponse>> Handle(
        UpdatePersonnelFileFamilyMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.FamilyMembers,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var kinshipCodeValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
            repository,
            personnelFile.TenantId,
            "kinshipCode",
            command.FamilyMember.KinshipCode,
            cancellationToken);
        if (kinshipCodeValidation != Error.None)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(kinshipCodeValidation);
        }

        var before = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateFamilyMember(
                command.FamilyMemberPublicId,
                command.FamilyMember.FirstName,
                command.FamilyMember.LastName,
                command.FamilyMember.KinshipCode,
                command.FamilyMember.Nationality,
                command.FamilyMember.BirthDate,
                command.FamilyMember.Sex,
                command.FamilyMember.MaritalStatus,
                command.FamilyMember.Occupation,
                command.FamilyMember.DocumentType,
                command.FamilyMember.DocumentNumber,
                command.FamilyMember.Phone,
                command.FamilyMember.IsStudying,
                command.FamilyMember.StudyPlace,
                command.FamilyMember.AcademicLevel,
                command.FamilyMember.IsBeneficiary,
                command.FamilyMember.IsWorking,
                command.FamilyMember.Workplace,
                command.FamilyMember.JobTitle,
                command.FamilyMember.WorkPhone,
                command.FamilyMember.Salary,
                command.FamilyMember.IsDeceased,
                command.FamilyMember.DeceasedDate);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.FamilyMemberPublicId)
                ?? throw new InvalidOperationException("Personnel file family member response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated family member for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileFamilyMemberResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileFamilyMemberResponse>.Failure(PersonnelFileErrors.FamilyMemberRuleViolation);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileFamilyMemberCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileFamilyMemberCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileFamilyMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.FamilyMembers,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveFamilyMember(command.FamilyMemberPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetFamilyMembersAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted family member for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.FamilyMembers,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileHobbyCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileHobbyCommand, PersonnelFileHobbyResponse>
{
    public async Task<Result<PersonnelFileHobbyResponse>> Handle(
        AddPersonnelFileHobbyCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileHobbyResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileHobbyResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Hobbies,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileHobbyResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileHobbyResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetHobbiesAsync(personnelFile.PublicId, cancellationToken);
        var hobby = PersonnelFileHobby.Create(command.Hobby.HobbyName);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddHobby(hobby);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetHobbiesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == hobby.PublicId)
                ?? throw new InvalidOperationException("Personnel file hobby response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added hobby for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Hobbies,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Hobbies,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileHobbyResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileHobbyCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileHobbyCommand, PersonnelFileHobbyResponse>
{
    public async Task<Result<PersonnelFileHobbyResponse>> Handle(
        UpdatePersonnelFileHobbyCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileHobbyResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileHobbyResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Hobbies,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileHobbyResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileHobbyResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetHobbiesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateHobby(command.HobbyPublicId, command.Hobby.HobbyName);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetHobbiesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.HobbyPublicId)
                ?? throw new InvalidOperationException("Personnel file hobby response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated hobby for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Hobbies,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Hobbies,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileHobbyResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileHobbyResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileHobbyCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileHobbyCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileHobbyCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Hobbies,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetHobbiesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveHobby(command.HobbyPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetHobbiesAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted hobby for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Hobbies,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Hobbies,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileEmployeeRelationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileEmployeeRelationCommand, PersonnelFileEmployeeRelationResponse>
{
    public async Task<Result<PersonnelFileEmployeeRelationResponse>> Handle(
        AddPersonnelFileEmployeeRelationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.EmployeeRelations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var relation = command.Relation;
        if (relation.RelatedEmployeePublicId == personnelFile.PublicId)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation.relatedEmployeePublicId"] = ["A personnel file cannot be related to itself."]
                    }));
        }

        var relatedPersonnelFile = await repository.GetForAccessCheckAsync(relation.RelatedEmployeePublicId, cancellationToken);
        if (relatedPersonnelFile is null || relatedPersonnelFile.RecordType != PersonnelFileRecordType.Employee)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation.relatedEmployeePublicId"] = ["RelatedEmployeePublicId must reference an existing employee personnel file in the same tenant."]
                    }));
        }

        var existingDuplicate = personnelFile.EmployeeRelations.Any(existing =>
            existing.RelatedPersonnelFileId == relatedPersonnelFile.Id &&
            string.Equals(existing.Relationship, PersonnelFileNormalization.Clean(relation.Relationship, "relationship"), StringComparison.OrdinalIgnoreCase));
        if (existingDuplicate)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation"] = ["An employee relation with the same related employee and relationship already exists."]
                    }));
        }

        var before = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);
        var entity = PersonnelFileEmployeeRelation.Create(relatedPersonnelFile.Id, relation.Relationship);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddEmployeeRelation(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == entity.PublicId)
                ?? throw new InvalidOperationException("Personnel file employee relation response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added employee relation for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEmployeeRelationResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileEmployeeRelationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileEmployeeRelationCommand, PersonnelFileEmployeeRelationResponse>
{
    public async Task<Result<PersonnelFileEmployeeRelationResponse>> Handle(
        UpdatePersonnelFileEmployeeRelationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.EmployeeRelations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var relation = command.Relation;
        if (relation.RelatedEmployeePublicId == personnelFile.PublicId)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation.relatedEmployeePublicId"] = ["A personnel file cannot be related to itself."]
                    }));
        }

        var relatedPersonnelFile = await repository.GetForAccessCheckAsync(relation.RelatedEmployeePublicId, cancellationToken);
        if (relatedPersonnelFile is null || relatedPersonnelFile.RecordType != PersonnelFileRecordType.Employee)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation.relatedEmployeePublicId"] = ["RelatedEmployeePublicId must reference an existing employee personnel file in the same tenant."]
                    }));
        }

        var existingDuplicate = personnelFile.EmployeeRelations.Any(existing =>
            existing.PublicId != command.RelationPublicId &&
            existing.RelatedPersonnelFileId == relatedPersonnelFile.Id &&
            string.Equals(existing.Relationship, PersonnelFileNormalization.Clean(relation.Relationship, "relationship"), StringComparison.OrdinalIgnoreCase));
        if (existingDuplicate)
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["relation"] = ["An employee relation with the same related employee and relationship already exists."]
                    }));
        }

        var before = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateEmployeeRelation(command.RelationPublicId, relatedPersonnelFile.Id, relation.Relationship);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.RelationPublicId)
                ?? throw new InvalidOperationException("Personnel file employee relation response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated employee relation for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEmployeeRelationResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileEmployeeRelationResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileEmployeeRelationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileEmployeeRelationCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileEmployeeRelationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.EmployeeRelations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveEmployeeRelation(command.RelationPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEmployeeRelationsAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted employee relation for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.EmployeeRelations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileBankAccountCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IBankCatalogRepository bankCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileBankAccountCommand, PersonnelFileBankAccountResponse>
{
    public async Task<Result<PersonnelFileBankAccountResponse>> Handle(
        AddPersonnelFileBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.BankAccounts,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var companyCountryCode = await repository.GetCompanyCountryCodeAsync(personnelFile.TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(companyCountryCode))
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(ErrorCatalog.NotFound);
        }

        var bankLookup = await bankCatalogRepository.GetActiveLookupByCountryAsync(
            companyCountryCode,
            command.BankAccount.BankPublicId,
            cancellationToken);
        if (bankLookup is null)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["bankPublicId"] =
                        [
                            "BankPublicId must reference an active bank catalog item for the company country."
                        ]
                    }));
        }

        var before = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);
        var bankAccount = PersonnelFileBankAccount.Create(
            bankLookup.InternalId,
            bankLookup.Code,
            command.BankAccount.CurrencyCode,
            command.BankAccount.AccountNumber,
            command.BankAccount.AccountTypeCode,
            command.BankAccount.IsPrimary);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddBankAccount(bankAccount);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == bankAccount.PublicId)
                ?? throw new InvalidOperationException("Personnel file bank account response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added bank account for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileBankAccountResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileBankAccountCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IBankCatalogRepository bankCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileBankAccountCommand, PersonnelFileBankAccountResponse>
{
    public async Task<Result<PersonnelFileBankAccountResponse>> Handle(
        UpdatePersonnelFileBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.BankAccounts,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!personnelFile.BankAccounts.Any(i => i.PublicId == command.BankAccountPublicId))
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(PersonnelFileErrors.NotFound);
        }

        var companyCountryCode = await repository.GetCompanyCountryCodeAsync(personnelFile.TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(companyCountryCode))
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(ErrorCatalog.NotFound);
        }

        var bankLookup = await bankCatalogRepository.GetActiveLookupByCountryAsync(
            companyCountryCode,
            command.BankAccount.BankPublicId,
            cancellationToken);
        if (bankLookup is null)
        {
            return Result<PersonnelFileBankAccountResponse>.Failure(
                ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["bankPublicId"] =
                        [
                            "BankPublicId must reference an active bank catalog item for the company country."
                        ]
                    }));
        }

        var before = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateBankAccount(
                command.BankAccountPublicId,
                bankLookup.InternalId,
                bankLookup.Code,
                command.BankAccount.CurrencyCode,
                command.BankAccount.AccountNumber,
                command.BankAccount.AccountTypeCode,
                command.BankAccount.IsPrimary);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.BankAccountPublicId)
                ?? throw new InvalidOperationException("Personnel file bank account response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated bank account for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileBankAccountResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileBankAccountCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileBankAccountCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.BankAccounts,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!personnelFile.BankAccounts.Any(i => i.PublicId == command.BankAccountPublicId))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }

        var before = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveBankAccount(command.BankAccountPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetBankAccountsAsync(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted bank account from personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.BankAccounts,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileAssociationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileAssociationCommand, PersonnelFileAssociationResponse>
{
    public async Task<Result<PersonnelFileAssociationResponse>> Handle(
        AddPersonnelFileAssociationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Associations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        PersonnelFileAssociation association;
        try
        {
            association = PersonnelFileAssociation.Create(
                command.Association.AssociationName,
                command.Association.Role,
                command.Association.JoinedDate,
                command.Association.LeftDate,
                command.Association.Payment);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        var before = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddAssociation(association);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == association.PublicId)
                ?? throw new InvalidOperationException("Personnel file association response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added association for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileAssociationResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileAssociationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileAssociationCommand, PersonnelFileAssociationResponse>
{
    public async Task<Result<PersonnelFileAssociationResponse>> Handle(
        UpdatePersonnelFileAssociationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Associations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateAssociation(
                command.AssociationPublicId,
                command.Association.AssociationName,
                command.Association.Role,
                command.Association.JoinedDate,
                command.Association.LeftDate,
                command.Association.Payment);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.AssociationPublicId)
                ?? throw new InvalidOperationException("Personnel file association response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated association for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileAssociationResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileAssociationResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileAssociationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileAssociationCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileAssociationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Associations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveAssociation(command.AssociationPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetAssociationsAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted association for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Associations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileEducationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IEducationCatalogRepository educationCatalogRepository,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileEducationCommand, PersonnelFileEducationResponse>
{
    public async Task<Result<PersonnelFileEducationResponse>> Handle(
        AddPersonnelFileEducationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileEducationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileEducationResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Educations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileEducationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var (catalogError, resolvedIds) = await ResolveEducationCatalogIdsAsync(
            command.Education, educationCatalogRepository, repository, cancellationToken);
        if (catalogError != Error.None)
        {
            return Result<PersonnelFileEducationResponse>.Failure(catalogError);
        }

        PersonnelFileEducation education;
        try
        {
            education = PersonnelFileEducation.Create(
                resolvedIds!.StatusId,
                command.Education.DegreeTitle,
                resolvedIds.StudyTypeId,
                resolvedIds.CareerId,
                command.Education.Institution,
                command.Education.CountryCode,
                command.Education.Specialty,
                command.Education.IsCurrentlyStudying,
                command.Education.StartDate,
                command.Education.EndDate,
                resolvedIds.ShiftId,
                resolvedIds.ModalityId,
                command.Education.TotalSubjects,
                command.Education.ApprovedSubjects);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        var before = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddEducation(education);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == education.PublicId)
                ?? throw new InvalidOperationException("Personnel file education response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added education for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEducationResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static Error CreateEducationCatalogValidationError(string fieldName, Guid publicId) =>
        ErrorCatalog.Validation(
            new Dictionary<string, string[]>
            {
                [fieldName] = [$"Catalog item '{publicId}' is not active or does not belong to the tenant."]
            });

    internal static async Task<(Error Error, ResolvedEducationCatalogIds? Ids)> ResolveEducationCatalogIdsAsync(
        EducationInput input,
        IEducationCatalogRepository catalogRepository,
        IPersonnelFileRepository fileRepository,
        CancellationToken cancellationToken)
    {
        var statusLookup = await catalogRepository.GetActiveLookupByIdAsync(
            EducationCatalogType.EducationStatus,
            input.StatusPublicId,
            cancellationToken);
        if (statusLookup is null)
        {
            return (CreateEducationCatalogValidationError(nameof(input.StatusPublicId), input.StatusPublicId), null);
        }

        var studyTypeLookup = await catalogRepository.GetActiveLookupByIdAsync(
            EducationCatalogType.StudyType,
            input.StudyTypePublicId,
            cancellationToken);
        if (studyTypeLookup is null)
        {
            return (CreateEducationCatalogValidationError(nameof(input.StudyTypePublicId), input.StudyTypePublicId), null);
        }

        var careerLookup = await catalogRepository.GetActiveLookupByIdAsync(
            EducationCatalogType.Career,
            input.CareerPublicId,
            cancellationToken);
        if (careerLookup is null)
        {
            return (CreateEducationCatalogValidationError(nameof(input.CareerPublicId), input.CareerPublicId), null);
        }

        var countryError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            fileRepository,
            Guid.Empty, // country validation is now tenant-independent for education catalogs
            nameof(input.CountryCode),
            PersonnelCurriculumCatalogCategories.Country,
            input.CountryCode,
            cancellationToken);
        if (countryError != Error.None)
        {
            return (countryError, null);
        }

        long? shiftId = null;
        if (input.ShiftPublicId.HasValue)
        {
            var shiftLookup = await catalogRepository.GetActiveLookupByIdAsync(
                EducationCatalogType.Shift,
                input.ShiftPublicId.Value,
                cancellationToken);
            if (shiftLookup is null)
            {
                return (CreateEducationCatalogValidationError(nameof(input.ShiftPublicId), input.ShiftPublicId.Value), null);
            }

            shiftId = shiftLookup.InternalId;
        }

        long? modalityId = null;
        if (input.ModalityPublicId.HasValue)
        {
            var modalityLookup = await catalogRepository.GetActiveLookupByIdAsync(
                EducationCatalogType.Modality,
                input.ModalityPublicId.Value,
                cancellationToken);
            if (modalityLookup is null)
            {
                return (CreateEducationCatalogValidationError(nameof(input.ModalityPublicId), input.ModalityPublicId.Value), null);
            }

            modalityId = modalityLookup.InternalId;
        }

        return (Error.None, new ResolvedEducationCatalogIds(
            statusLookup.InternalId,
            studyTypeLookup.InternalId,
            careerLookup.InternalId,
            shiftId,
            modalityId));
    }

    internal sealed record ResolvedEducationCatalogIds(
        long StatusId,
        long StudyTypeId,
        long CareerId,
        long? ShiftId,
        long? ModalityId);
}

internal sealed class UpdatePersonnelFileEducationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IEducationCatalogRepository educationCatalogRepository,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileEducationCommand, PersonnelFileEducationResponse>
{
    public async Task<Result<PersonnelFileEducationResponse>> Handle(
        UpdatePersonnelFileEducationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileEducationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileEducationResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Educations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileEducationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var (catalogError, resolvedIds) = await AddPersonnelFileEducationCommandHandler.ResolveEducationCatalogIdsAsync(
            command.Education, educationCatalogRepository, repository, cancellationToken);
        if (catalogError != Error.None)
        {
            return Result<PersonnelFileEducationResponse>.Failure(catalogError);
        }

        var before = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateEducation(
                command.EducationPublicId,
                resolvedIds!.StatusId,
                command.Education.DegreeTitle,
                resolvedIds.StudyTypeId,
                resolvedIds.CareerId,
                command.Education.Institution,
                command.Education.CountryCode,
                command.Education.Specialty,
                command.Education.IsCurrentlyStudying,
                command.Education.StartDate,
                command.Education.EndDate,
                resolvedIds.ShiftId,
                resolvedIds.ModalityId,
                command.Education.TotalSubjects,
                command.Education.ApprovedSubjects);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.EducationPublicId)
                ?? throw new InvalidOperationException("Personnel file education response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated education for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEducationResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileEducationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileEducationCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileEducationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Educations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveEducation(command.EducationPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted education for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileLanguageCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileLanguageCommand, PersonnelFileLanguageResponse>
{
    public async Task<Result<PersonnelFileLanguageResponse>> Handle(
        AddPersonnelFileLanguageCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Languages,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var languageError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Language.LanguageCode),
            PersonnelCurriculumCatalogCategories.Language,
            command.Language.LanguageCode,
            cancellationToken);
        if (languageError != Error.None)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(languageError);
        }

        var levelError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Language.LevelCode),
            PersonnelCurriculumCatalogCategories.LanguageLevel,
            command.Language.LevelCode,
            cancellationToken);
        if (levelError != Error.None)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(levelError);
        }

        var before = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);
        PersonnelFileLanguage entity;
        try
        {
            entity = PersonnelFileLanguage.Create(
                command.Language.LanguageCode,
                command.Language.LevelCode,
                command.Language.Speaks,
                command.Language.Writes,
                command.Language.Reads);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddLanguage(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == entity.PublicId)
                ?? throw new InvalidOperationException("Personnel file language response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added language for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileLanguageResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileLanguageCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileLanguageCommand, PersonnelFileLanguageResponse>
{
    public async Task<Result<PersonnelFileLanguageResponse>> Handle(
        UpdatePersonnelFileLanguageCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Languages,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var languageError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Language.LanguageCode),
            PersonnelCurriculumCatalogCategories.Language,
            command.Language.LanguageCode,
            cancellationToken);
        if (languageError != Error.None)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(languageError);
        }

        var levelError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Language.LevelCode),
            PersonnelCurriculumCatalogCategories.LanguageLevel,
            command.Language.LevelCode,
            cancellationToken);
        if (levelError != Error.None)
        {
            return Result<PersonnelFileLanguageResponse>.Failure(levelError);
        }

        var before = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateLanguage(
                command.LanguagePublicId,
                command.Language.LanguageCode,
                command.Language.LevelCode,
                command.Language.Speaks,
                command.Language.Writes,
                command.Language.Reads);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.LanguagePublicId)
                ?? throw new InvalidOperationException("Personnel file language response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated language for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileLanguageResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileLanguageResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileLanguageCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileLanguageCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileLanguageCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Languages,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveLanguage(command.LanguagePublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetLanguagesAsync(personnelFile.PublicId, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted language for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Languages,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileTrainingCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileTrainingCommand, PersonnelFileTrainingResponse>
{
    public async Task<Result<PersonnelFileTrainingResponse>> Handle(
        AddPersonnelFileTrainingCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Trainings,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.TrainingTypeCode),
            PersonnelCurriculumCatalogCategories.TrainingType,
            command.Training.TrainingTypeCode,
            cancellationToken);
        if (typeError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(typeError);

        var countryError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.CountryCode),
            PersonnelCurriculumCatalogCategories.Country,
            command.Training.CountryCode,
            cancellationToken);
        if (countryError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(countryError);

        var durationUnitError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.DurationUnitCode),
            PersonnelCurriculumCatalogCategories.DurationUnit,
            command.Training.DurationUnitCode,
            cancellationToken);
        if (durationUnitError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(durationUnitError);

        var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.CostCurrencyCode),
            PersonnelCurriculumCatalogCategories.Currency,
            command.Training.CostCurrencyCode ?? string.Empty,
            cancellationToken);
        if (currencyError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(currencyError);

        var before = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);
        
        PersonnelFileTraining training;
        try
        {
            training = PersonnelFileTraining.Create(
                command.Training.TrainingName,
                command.Training.TrainingTypeCode,
                command.Training.Description,
                command.Training.Topic,
                command.Training.Institution,
                command.Training.Instructors,
                command.Training.Score,
                command.Training.StartDate,
                command.Training.EndDate,
                command.Training.IsInternal,
                command.Training.IsLocal,
                command.Training.CountryCode,
                command.Training.DurationValue,
                command.Training.DurationUnitCode,
                command.Training.CostAmount,
                command.Training.CostCurrencyCode);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddTraining(training);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == training.PublicId)
                ?? throw new InvalidOperationException("Personnel file training response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added training for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileTrainingResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileTrainingCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileTrainingCommand, PersonnelFileTrainingResponse>
{
    public async Task<Result<PersonnelFileTrainingResponse>> Handle(
        UpdatePersonnelFileTrainingCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Trainings,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.TrainingTypeCode),
            PersonnelCurriculumCatalogCategories.TrainingType,
            command.Training.TrainingTypeCode,
            cancellationToken);
        if (typeError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(typeError);

        var countryError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.CountryCode),
            PersonnelCurriculumCatalogCategories.Country,
            command.Training.CountryCode,
            cancellationToken);
        if (countryError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(countryError);

        var durationUnitError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.DurationUnitCode),
            PersonnelCurriculumCatalogCategories.DurationUnit,
            command.Training.DurationUnitCode,
            cancellationToken);
        if (durationUnitError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(durationUnitError);

        var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Training.CostCurrencyCode),
            PersonnelCurriculumCatalogCategories.Currency,
            command.Training.CostCurrencyCode ?? string.Empty,
            cancellationToken);
        if (currencyError != Error.None) return Result<PersonnelFileTrainingResponse>.Failure(currencyError);

        var before = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);
        if (!before.Any(i => i.Id == command.TrainingPublicId))
        {
            return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.NotFound);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            try
            {
                personnelFile.UpdateTraining(
                    command.TrainingPublicId,
                    command.Training.TrainingName,
                    command.Training.TrainingTypeCode,
                    command.Training.Description,
                    command.Training.Topic,
                    command.Training.Institution,
                    command.Training.Instructors,
                    command.Training.Score,
                    command.Training.StartDate,
                    command.Training.EndDate,
                    command.Training.IsInternal,
                    command.Training.IsLocal,
                    command.Training.CountryCode,
                    command.Training.DurationValue,
                    command.Training.DurationUnitCode,
                    command.Training.CostAmount,
                    command.Training.CostCurrencyCode);
            }
            catch (InvalidOperationException)
            {
                return Result<PersonnelFileTrainingResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.TrainingPublicId)
                ?? throw new InvalidOperationException("Personnel file training response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated training for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileTrainingResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileTrainingCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileTrainingCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileTrainingCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Trainings,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);
        if (!before.Any(i => i.Id == command.TrainingPublicId))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveTraining(command.TrainingPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetTrainingsAsync(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted training for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Trainings,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<bool>.Success(true);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFilePreviousEmploymentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFilePreviousEmploymentCommand, PersonnelFilePreviousEmploymentResponse>
{
    public async Task<Result<PersonnelFilePreviousEmploymentResponse>> Handle(
        AddPersonnelFilePreviousEmploymentCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.PreviousEmployments,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.PreviousEmployment.CurrencyCode),
            PersonnelCurriculumCatalogCategories.Currency,
            command.PreviousEmployment.CurrencyCode,
            cancellationToken);
        if (currencyError != Error.None) return Result<PersonnelFilePreviousEmploymentResponse>.Failure(currencyError);

        var before = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);

        PersonnelFilePreviousEmployment item;
        try
        {
            item = PersonnelFilePreviousEmployment.Create(
                command.PreviousEmployment.Institution,
                command.PreviousEmployment.Place,
                command.PreviousEmployment.LastPosition,
                command.PreviousEmployment.ManagerName,
                command.PreviousEmployment.EntryDate,
                command.PreviousEmployment.RetirementDate,
                command.PreviousEmployment.CompanyPhone,
                command.PreviousEmployment.ExitReason,
                command.PreviousEmployment.FirstSalaryAmount,
                command.PreviousEmployment.LastSalaryAmount,
                command.PreviousEmployment.AverageCommissionAmount,
                command.PreviousEmployment.CurrencyCode);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddPreviousEmployment(item);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(r => r.PublicId == item.PublicId)
                ?? throw new InvalidOperationException("Personnel file previous employment response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added previous employment for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFilePreviousEmploymentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFilePreviousEmploymentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFilePreviousEmploymentCommand, PersonnelFilePreviousEmploymentResponse>
{
    public async Task<Result<PersonnelFilePreviousEmploymentResponse>> Handle(
        UpdatePersonnelFilePreviousEmploymentCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.PreviousEmployments,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var currencyError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.PreviousEmployment.CurrencyCode),
            PersonnelCurriculumCatalogCategories.Currency,
            command.PreviousEmployment.CurrencyCode,
            cancellationToken);
        if (currencyError != Error.None) return Result<PersonnelFilePreviousEmploymentResponse>.Failure(currencyError);

        var before = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);
        if (!before.Any(i => i.PublicId == command.PreviousEmploymentPublicId))
        {
            return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.NotFound);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            try
            {
                personnelFile.UpdatePreviousEmployment(
                    command.PreviousEmploymentPublicId,
                    command.PreviousEmployment.Institution,
                    command.PreviousEmployment.Place,
                    command.PreviousEmployment.LastPosition,
                    command.PreviousEmployment.ManagerName,
                    command.PreviousEmployment.EntryDate,
                    command.PreviousEmployment.RetirementDate,
                    command.PreviousEmployment.CompanyPhone,
                    command.PreviousEmployment.ExitReason,
                    command.PreviousEmployment.FirstSalaryAmount,
                    command.PreviousEmployment.LastSalaryAmount,
                    command.PreviousEmployment.AverageCommissionAmount,
                    command.PreviousEmployment.CurrencyCode);
            }
            catch (InvalidOperationException)
            {
                return Result<PersonnelFilePreviousEmploymentResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(r => r.PublicId == command.PreviousEmploymentPublicId)
                ?? throw new InvalidOperationException("Personnel file previous employment response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated previous employment for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFilePreviousEmploymentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFilePreviousEmploymentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFilePreviousEmploymentCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFilePreviousEmploymentCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.PreviousEmployments,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);
        if (!before.Any(i => i.PublicId == command.PreviousEmploymentPublicId))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemovePreviousEmployment(command.PreviousEmploymentPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetPreviousEmploymentsAsync(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted previous employment for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PreviousEmployments,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileReferenceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileReferenceCommand, PersonnelFileReferenceResponse>
{
    public async Task<Result<PersonnelFileReferenceResponse>> Handle(
        AddPersonnelFileReferenceCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.References,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Reference.ReferenceTypeCode),
            PersonnelCurriculumCatalogCategories.ReferenceType,
            command.Reference.ReferenceTypeCode,
            cancellationToken);
        if (typeError != Error.None) return Result<PersonnelFileReferenceResponse>.Failure(typeError);

        var before = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);

        PersonnelFileReference reference;
        try
        {
            reference = PersonnelFileReference.Create(
                command.Reference.PersonName,
                command.Reference.Address,
                command.Reference.Phone,
                command.Reference.ReferenceTypeCode,
                command.Reference.Occupation,
                command.Reference.Workplace,
                command.Reference.WorkPhone,
                command.Reference.KnownTimeYears);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddReference(reference);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.PublicId == reference.PublicId)
                ?? throw new InvalidOperationException("Personnel file reference response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added reference for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileReferenceResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileReferenceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileReferenceCommand, PersonnelFileReferenceResponse>
{
    public async Task<Result<PersonnelFileReferenceResponse>> Handle(
        UpdatePersonnelFileReferenceCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.References,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var typeError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            repository,
            personnelFile.TenantId,
            nameof(command.Reference.ReferenceTypeCode),
            PersonnelCurriculumCatalogCategories.ReferenceType,
            command.Reference.ReferenceTypeCode,
            cancellationToken);
        if (typeError != Error.None) return Result<PersonnelFileReferenceResponse>.Failure(typeError);

        var before = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);
        if (!before.Any(i => i.PublicId == command.ReferencePublicId))
        {
            return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.NotFound);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            try
            {
                personnelFile.UpdateReference(
                    command.ReferencePublicId,
                    command.Reference.PersonName,
                    command.Reference.Address,
                    command.Reference.Phone,
                    command.Reference.ReferenceTypeCode,
                    command.Reference.Occupation,
                    command.Reference.Workplace,
                    command.Reference.WorkPhone,
                    command.Reference.KnownTimeYears);
            }
            catch (InvalidOperationException)
            {
                return Result<PersonnelFileReferenceResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.PublicId == command.ReferencePublicId)
                ?? throw new InvalidOperationException("Personnel file reference response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated reference for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileReferenceResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileReferenceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileReferenceCommand, bool>
{
    public async Task<Result<bool>> Handle(
        DeletePersonnelFileReferenceCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.References,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);
        if (!before.Any(i => i.PublicId == command.ReferencePublicId))
        {
            return Result<bool>.Failure(PersonnelFileErrors.NotFound);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveReference(command.ReferencePublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetReferencesAsync(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted reference for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.References,
                        data = after
                    }),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<bool>.Success(true);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivatePersonnelFileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivatePersonnelFileCommand, PersonnelFileShellResponse>
{
    private sealed record PersonnelFileLifecycleAuditSnapshot(
        Guid PublicId,
        Guid CompanyId,
        PersonnelFileRecordType RecordType,
        PersonnelFileLifecycleStatus LifecycleStatus,
        string FullName,
        string? PhotoUrl,
        bool IsActive,
        Guid? OrgUnitId,
        Guid? AssignedPositionSlotId,
        Guid? LinkedUserId,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    public async Task<Result<PersonnelFileShellResponse>> Handle(
        ActivatePersonnelFileCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileShellResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileShellResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForAccessCheckAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileShellResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileShellResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = CreateAuditSnapshot(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = CreateAuditSnapshot(personnelFile);
            var shellResponse = CreateShellResponse(personnelFile);

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
            return Result<PersonnelFileShellResponse>.Success(shellResponse);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static PersonnelFileShellResponse CreateShellResponse(PersonnelFile personnelFile) =>
        new(
            personnelFile.PublicId,
            personnelFile.TenantId,
            personnelFile.RecordType,
            personnelFile.LifecycleStatus,
            personnelFile.FullName,
            personnelFile.PhotoUrl,
            personnelFile.IsActive,
            personnelFile.OrgUnitPublicId,
            personnelFile.AssignedPositionSlotPublicId,
            personnelFile.LinkedUserPublicId,
            personnelFile.ConcurrencyToken,
            personnelFile.CreatedUtc,
            personnelFile.ModifiedUtc);

    private static PersonnelFileLifecycleAuditSnapshot CreateAuditSnapshot(PersonnelFile personnelFile) =>
        new(
            personnelFile.PublicId,
            personnelFile.TenantId,
            personnelFile.RecordType,
            personnelFile.LifecycleStatus,
            personnelFile.FullName,
            personnelFile.PhotoUrl,
            personnelFile.IsActive,
            personnelFile.OrgUnitPublicId,
            personnelFile.AssignedPositionSlotPublicId,
            personnelFile.LinkedUserPublicId,
            personnelFile.ConcurrencyToken,
            personnelFile.CreatedUtc,
            personnelFile.ModifiedUtc);
}

internal sealed class InactivatePersonnelFileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivatePersonnelFileCommand, PersonnelFileShellResponse>
{
    private sealed record PersonnelFileLifecycleAuditSnapshot(
        Guid PublicId,
        Guid CompanyId,
        PersonnelFileRecordType RecordType,
        PersonnelFileLifecycleStatus LifecycleStatus,
        string FullName,
        string? PhotoUrl,
        bool IsActive,
        Guid? OrgUnitId,
        Guid? AssignedPositionSlotId,
        Guid? LinkedUserId,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    public async Task<Result<PersonnelFileShellResponse>> Handle(
        InactivatePersonnelFileCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileShellResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileShellResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForAccessCheckAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileShellResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileShellResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = CreateAuditSnapshot(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = CreateAuditSnapshot(personnelFile);
            var shellResponse = CreateShellResponse(personnelFile);

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
            return Result<PersonnelFileShellResponse>.Success(shellResponse);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static PersonnelFileShellResponse CreateShellResponse(PersonnelFile personnelFile) =>
        new(
            personnelFile.PublicId,
            personnelFile.TenantId,
            personnelFile.RecordType,
            personnelFile.LifecycleStatus,
            personnelFile.FullName,
            personnelFile.PhotoUrl,
            personnelFile.IsActive,
            personnelFile.OrgUnitPublicId,
            personnelFile.AssignedPositionSlotPublicId,
            personnelFile.LinkedUserPublicId,
            personnelFile.ConcurrencyToken,
            personnelFile.CreatedUtc,
            personnelFile.ModifiedUtc);

    private static PersonnelFileLifecycleAuditSnapshot CreateAuditSnapshot(PersonnelFile personnelFile) =>
        new(
            personnelFile.PublicId,
            personnelFile.TenantId,
            personnelFile.RecordType,
            personnelFile.LifecycleStatus,
            personnelFile.FullName,
            personnelFile.PhotoUrl,
            personnelFile.IsActive,
            personnelFile.OrgUnitPublicId,
            personnelFile.AssignedPositionSlotPublicId,
            personnelFile.LinkedUserPublicId,
            personnelFile.ConcurrencyToken,
            personnelFile.CreatedUtc,
            personnelFile.ModifiedUtc);
}

internal sealed class UploadPersonnelFileDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IPersonnelFileDocumentStorageService documentStorageService,
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

        if (!documentStorageService.IsConfigured)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(PersonnelFileErrors.DocumentStorageNotConfigured);
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

        var personnelFile = await repository.GetForAccessCheckAsync(command.PersonnelFileId, cancellationToken);
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
        var documentId = Guid.NewGuid();

        PersonnelFileDocument document;
        PersonnelFileStoredDocumentArtifact? storedArtifact = null;
        try
        {
            storedArtifact = await documentStorageService.UploadAsync(
                personnelFile.TenantId,
                personnelFile.PublicId,
                documentId,
                command.FileName,
                command.ContentType,
                command.FileData,
                cancellationToken);

            document = PersonnelFileDocument.Create(
                documentId,
                command.DocumentType,
                command.Observations,
                command.DeliveryDate,
                command.LoanDate,
                command.ReturnDate,
                storedArtifact.BlobName,
                storedArtifact.BlobUrl,
                command.FileName,
                command.ContentType,
                command.FileData.Length,
                sha256);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileDocumentMetadataResponse>.Failure(PersonnelFileErrors.DocumentLoanDatesInvalid);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddDocument(document);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var uploaded = await repository.GetDocumentMetadataByIdAsync(document.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file document could not be resolved after upload.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileDocumentUploaded,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Uploaded document {uploaded.FileName} for personnel file {personnelFile.FullName}.",
                    After: uploaded),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileDocumentMetadataResponse>.Success(uploaded);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            if (storedArtifact is not null)
            {
                await documentStorageService.DeleteIfExistsAsync(storedArtifact.BlobName, cancellationToken);
            }
            throw;
        }
    }
}

internal sealed class ReplacePersonnelFileDocumentsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IPersonnelFileDocumentStorageService documentStorageService,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ReplacePersonnelFileSectionCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileDocumentsCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>> Handle(
        ReplacePersonnelFileDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForUpdateAsync<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            PersonnelFileTrackedSection.Documents,
            tenantContext,
            authorizationService,
            repository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var uploadsRequired = command.Documents.Any(static item => item.FileData is not null);
        if (uploadsRequired && !documentStorageService.IsConfigured)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(PersonnelFileErrors.DocumentStorageNotConfigured);
        }

        var items = command.Documents as IReadOnlyList<PersonnelFileDocumentInput> ?? command.Documents.ToArray();
        var duplicateDocumentIds = items
            .Where(static item => item.DocumentPublicId.HasValue)
            .GroupBy(static item => item.DocumentPublicId!.Value)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateDocumentIds.Length > 0)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["items"] = [$"Duplicate documentPublicId values are not allowed: {string.Join(", ", duplicateDocumentIds)}"]
                }));
        }

        var duplicateFileKeys = items
            .Where(static item => !string.IsNullOrWhiteSpace(item.FileKey))
            .GroupBy(static item => item.FileKey!, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateFileKeys.Length > 0)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["items"] = [$"Duplicate fileKey values are not allowed: {string.Join(", ", duplicateFileKeys)}"]
                }));
        }

        var existingDocumentsById = personnelFile!.Documents.ToDictionary(static item => item.PublicId);
        var uploadedArtifacts = new List<(string BlobName, string? PreviousBlobName)>();
        var referencedDocumentIds = new HashSet<Guid>();
        var persisted = false;
        try
        {
            foreach (var item in items)
            {
                if (item.FileData is { Length: > 0 } fileData &&
                    fileData.Length > PersonnelFileValidationRules.MaxDocumentFileSizeBytes)
                {
                    return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(PersonnelFileErrors.DocumentFileTooLarge);
                }

                if (item.DocumentPublicId.HasValue)
                {
                    referencedDocumentIds.Add(item.DocumentPublicId.Value);
                }

                if (!item.DocumentPublicId.HasValue)
                {
                    var newFileData = item.FileData
                        ?? throw new InvalidOperationException("FileData is required for new documents.");
                    var shaBytes = SHA256.HashData(newFileData);
                    var sha256 = Convert.ToHexString(shaBytes).ToLowerInvariant();
                    var documentId = Guid.NewGuid();
                    PersonnelFileStoredDocumentArtifact uploadedArtifact;
                    try
                    {
                        uploadedArtifact = await documentStorageService.UploadAsync(
                            personnelFile.TenantId,
                            personnelFile.PublicId,
                            documentId,
                            item.FileName!,
                            item.ContentType!,
                            newFileData,
                            cancellationToken);

                        var document = PersonnelFileDocument.Create(
                            documentId,
                            item.DocumentType,
                            item.Observations,
                            item.DeliveryDate,
                            item.LoanDate,
                            item.ReturnDate,
                            uploadedArtifact.BlobName,
                            uploadedArtifact.BlobUrl,
                            item.FileName!,
                            item.ContentType!,
                            newFileData.Length,
                            sha256);

                        personnelFile.AddDocument(document);
                        referencedDocumentIds.Add(documentId);
                        uploadedArtifacts.Add((uploadedArtifact.BlobName, null));
                    }
                    catch (InvalidOperationException)
                    {
                        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(PersonnelFileErrors.DocumentLoanDatesInvalid);
                    }

                    continue;
                }

                if (!existingDocumentsById.TryGetValue(item.DocumentPublicId.Value, out var existingDocument))
                {
                    var referencedDocument = await repository.GetDocumentByIdAsync(item.DocumentPublicId.Value, cancellationToken);
                    if (referencedDocument is null)
                    {
                        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(
                            await repository.DocumentExistsOutsideTenantAsync(item.DocumentPublicId.Value, cancellationToken)
                                ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                                : PersonnelFileErrors.DocumentNotFound);
                    }

                    return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(
                        ErrorCatalog.Validation(new Dictionary<string, string[]>
                        {
                            ["items"] = [$"DocumentPublicId '{item.DocumentPublicId.Value}' does not belong to personnel file '{personnelFile.PublicId}'."]
                        }));
                }

                try
                {
                    existingDocument.UpdateMetadata(
                        item.DocumentType,
                        item.Observations,
                        item.DeliveryDate,
                        item.LoanDate,
                        item.ReturnDate);
                }
                catch (InvalidOperationException)
                {
                    return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(PersonnelFileErrors.DocumentLoanDatesInvalid);
                }

                if (item.FileData is null)
                {
                    continue;
                }

                var replacementShaBytes = SHA256.HashData(item.FileData);
                var replacementSha256 = Convert.ToHexString(replacementShaBytes).ToLowerInvariant();
                var previousBlobName = existingDocument.BlobName;
                var storedArtifact = await documentStorageService.UploadAsync(
                    existingDocument.TenantId,
                    personnelFile.PublicId,
                    existingDocument.PublicId,
                    item.FileName!,
                    item.ContentType!,
                    item.FileData,
                    cancellationToken);

                existingDocument.ReplaceFile(
                    storedArtifact.BlobName,
                    storedArtifact.BlobUrl,
                    item.FileName!,
                    item.ContentType!,
                    item.FileData.Length,
                    replacementSha256);

                uploadedArtifacts.Add((storedArtifact.BlobName, string.Equals(previousBlobName, storedArtifact.BlobName, StringComparison.Ordinal)
                    ? null
                    : previousBlobName));
            }

            foreach (var existingDocument in personnelFile.Documents.Where(item => !referencedDocumentIds.Contains(item.PublicId) && item.IsActive))
            {
                existingDocument.Inactivate();
            }

            personnelFile.MarkDocumentsUpdated();

            var result = await PersistSectionAsync(
                personnelFile,
                PersonnelFilePrintSections.Documents,
                $"Updated personnel file {personnelFile.FullName} documents.",
                repository.GetDocumentsAsync,
                auditService,
                unitOfWork,
                AuditEventTypes.PersonnelFileUpdated,
                cancellationToken);
            persisted = true;
            return result;
        }
        catch
        {
            foreach (var uploadedArtifact in uploadedArtifacts)
            {
                await documentStorageService.DeleteIfExistsAsync(uploadedArtifact.BlobName, cancellationToken);
            }

            throw;
        }
        finally
        {
            if (persisted)
            {
                foreach (var previousBlobName in uploadedArtifacts
                             .Select(static item => item.PreviousBlobName)
                             .Where(static item => !string.IsNullOrWhiteSpace(item)))
                {
                    await documentStorageService.DeleteIfExistsAsync(previousBlobName!, cancellationToken);
                }
            }
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

        var personnelFile = await repository.GetForAccessCheckAsync(command.PersonnelFileId, cancellationToken);
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
