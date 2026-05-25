using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CLARIHR.Api.Contracts.PersonnelFiles;

public sealed class CreatePersonnelFileRequest
{
    public PersonnelFileRecordType RecordType { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public DateTime BirthDate { get; init; }
    public string? MaritalStatusCode { get; init; }
    public string? ProfessionCode { get; init; }
    public string? Nationality { get; init; }
    public string? PersonalEmail { get; init; }
    public string? InstitutionalEmail { get; init; }
    public string? PersonalPhone { get; init; }
    public string? InstitutionalPhone { get; init; }
    public string? BirthCountryCode { get; init; }
    public string? BirthDepartmentCode { get; init; }
    public string? BirthMunicipalityCode { get; init; }
    public Guid? PhotoFilePublicId { get; init; }
    public Guid? OrgUnitPublicId { get; init; }
    public Guid? AssignedPositionSlotPublicId { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }

    public bool HasLegacyItemsPayload() =>
        AdditionalProperties?.Keys.Any(static key => key.Equals("items", StringComparison.OrdinalIgnoreCase)) == true;
}

public sealed record UpdatePersonnelFileRequest(
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
    Guid? PhotoFilePublicId,
    Guid? OrgUnitPublicId,
    Guid? AssignedPositionSlotPublicId);

/// <summary>
/// JSON Patch (RFC 6902) target for <c>PATCH /personnel-files/{publicId}</c>. The patchable
/// members are the core personal-info fields plus <c>isActive</c>; the concurrency token is
/// supplied via the <c>If-Match</c> header, not the document body.
/// </summary>
public sealed class PatchPersonnelFileRequest
{
    public PersonnelFileRecordType RecordType { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public string? MaritalStatusCode { get; set; }
    public string? ProfessionCode { get; set; }
    public string? Nationality { get; set; }
    public string? PersonalEmail { get; set; }
    public string? InstitutionalEmail { get; set; }
    public string? PersonalPhone { get; set; }
    public string? InstitutionalPhone { get; set; }
    public string? BirthCountryCode { get; set; }
    public string? BirthDepartmentCode { get; set; }
    public string? BirthMunicipalityCode { get; set; }
    public Guid? PhotoFilePublicId { get; set; }
    public Guid? OrgUnitPublicId { get; set; }
    public Guid? AssignedPositionSlotPublicId { get; set; }
    public bool IsActive { get; set; }
}

public sealed record FinalizePersonnelFileRequest(Guid ConcurrencyToken, bool? CreateUserAccount);

public sealed record UpdatePersonnelFileEmployeeProfileRequest(
    string EmployeeCode,
    string EmploymentStatusCode,
    bool IsEmploymentActive,
    string ContractTypeCode,
    DateTime HireDate,
    string? RetirementCategoryCode,
    string? RetirementReasonCode,
    string? RetirementNotes,
    DateTime? RetirementDate,
    string? WorkdayCode,
    string? PayrollTypeCode,
    Guid? PositionSlotPublicId,
    Guid? JobProfilePublicId,
    Guid? OrgUnitPublicId,
    Guid? WorkCenterPublicId,
    Guid? CostCenterPublicId,
    DateTime? ContractStartDate,
    DateTime? ContractEndDate,
    string? VacationConfigurationJson,
    Guid ConcurrencyToken);

public sealed record AddEmploymentAssignmentRequest(
    string AssignmentTypeCode,
    Guid? PositionSlotPublicId,
    Guid? OrgUnitPublicId,
    Guid? WorkCenterPublicId,
    Guid? CostCenterPublicId,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPrimary,
    bool IsActive,
    string? Notes);

public sealed record UpdateEmploymentAssignmentRequest(
    string AssignmentTypeCode,
    Guid? PositionSlotPublicId,
    Guid? OrgUnitPublicId,
    Guid? WorkCenterPublicId,
    Guid? CostCenterPublicId,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPrimary,
    bool IsActive,
    string? Notes,
    Guid ConcurrencyToken);

public sealed record AddContractHistoryRequest(
    string ContractTypeCode,
    DateTime ContractDate,
    DateTime? ContractEndDate,
    Guid? PositionSlotPublicId,
    bool IsActive,
    string? Notes);

public sealed record UpdateContractHistoryRequest(
    string ContractTypeCode,
    DateTime ContractDate,
    DateTime? ContractEndDate,
    Guid? PositionSlotPublicId,
    bool IsActive,
    string? Notes,
    Guid ConcurrencyToken);

public sealed record AddAuthorizationSubstitutionRequest(
    string SubstitutionTypeCode,
    Guid SubstitutePersonnelFilePublicId,
    string? SubstitutePositionTitle,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes);

public sealed record UpdateAuthorizationSubstitutionRequest(
    string SubstitutionTypeCode,
    Guid SubstitutePersonnelFilePublicId,
    string? SubstitutePositionTitle,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes,
    Guid ConcurrencyToken);

public sealed record AddAssetAccessRequest(
    string AssetTypeCode,
    string AssetOrAccessName,
    string? AccessLevelCode,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    DateTime? DeliveryDateUtc,
    string? DeliveryStatusCode,
    bool IsActive,
    string? Notes);

public sealed record UpdateAssetAccessRequest(
    string AssetTypeCode,
    string AssetOrAccessName,
    string? AccessLevelCode,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    DateTime? DeliveryDateUtc,
    string? DeliveryStatusCode,
    bool IsActive,
    string? Notes,
    Guid ConcurrencyToken);

public sealed record AddPersonnelActionRequest(
    string ActionTypeCode,
    string ActionStatusCode,
    DateTime ActionDateUtc,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Description,
    string? Reference,
    decimal? Amount,
    string? CurrencyCode);

public sealed record InsuranceBeneficiaryItemRequest(
    string FullName,
    string? DocumentNumber,
    DateTime? BirthDate,
    string KinshipCode);

// ─── Atomic compensation request contracts ────────────────────────────────────

public sealed record AddSalaryItemRequest(
    string IncomeTypeCode,
    string SalaryRubricCode,
    string CurrencyCode,
    string PayPeriodCode,
    decimal Amount,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive);

public sealed record UpdateSalaryItemRequest(
    string IncomeTypeCode,
    string SalaryRubricCode,
    string CurrencyCode,
    string PayPeriodCode,
    decimal Amount,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    Guid ConcurrencyToken);

public sealed record AddAdditionalBenefitRequest(
    string BenefitTypeCode,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes);

public sealed record UpdateAdditionalBenefitRequest(
    string BenefitTypeCode,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes,
    Guid ConcurrencyToken);

public sealed record AddPaymentMethodRequest(
    string PaymentMethodCode,
    Guid? BankAccountPublicId,
    bool IsPrimary,
    bool IsActive,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes);

public sealed record UpdatePaymentMethodRequest(
    string PaymentMethodCode,
    Guid? BankAccountPublicId,
    bool IsPrimary,
    bool IsActive,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes,
    Guid ConcurrencyToken);

public sealed record AddPayrollTransactionRequest(
    string TransactionTypeCode,
    DateTime TransactionDateUtc,
    string PayrollPeriodCode,
    string? Description,
    decimal Amount,
    string CurrencyCode,
    bool IsDebit,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record AddInsuranceRequest(
    string InsuranceCode,
    decimal? EmployeeContribution,
    decimal? EmployerContribution,
    string? RangeCode,
    string? PolicyNumber,
    decimal? InsuredAmount,
    string? CurrencyCode,
    bool IsActive,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    IReadOnlyCollection<InsuranceBeneficiaryItemRequest> Beneficiaries);

public sealed record UpdateInsuranceRequest(
    string InsuranceCode,
    decimal? EmployeeContribution,
    decimal? EmployerContribution,
    string? RangeCode,
    string? PolicyNumber,
    decimal? InsuredAmount,
    string? CurrencyCode,
    bool IsActive,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    IReadOnlyCollection<InsuranceBeneficiaryItemRequest> Beneficiaries,
    Guid ConcurrencyToken);

public sealed record AddMedicalClaimRequest(
    Guid? InsurancePublicId,
    string? AccountNumber,
    string ClaimTypeCode,
    string? Diagnosis,
    decimal? ClaimAmount,
    string? CurrencyCode,
    decimal? PaidAmount,
    int? ResponseTimeDays,
    string? Notes,
    DateTime ClaimDateUtc,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record UpdateMedicalClaimRequest(
    Guid? InsurancePublicId,
    string? AccountNumber,
    string ClaimTypeCode,
    string? Diagnosis,
    decimal? ClaimAmount,
    string? CurrencyCode,
    decimal? PaidAmount,
    int? ResponseTimeDays,
    string? Notes,
    DateTime ClaimDateUtc,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    Guid ConcurrencyToken);

public sealed record AddPerformanceEvaluationRequest(
    string EvaluatorName,
    DateTime EvaluationDateUtc,
    decimal? Score,
    string? QualitativeScoreCode,
    string? Comment,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record UpdatePerformanceEvaluationRequest(
    string EvaluatorName,
    DateTime EvaluationDateUtc,
    decimal? Score,
    string? QualitativeScoreCode,
    string? Comment,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    Guid ConcurrencyToken);

public sealed record AddPositionCompetencyResultRequest(
    string CompetencyCode,
    string? DesiredBehaviors,
    decimal? ExpectedScore,
    decimal? AchievedScore,
    decimal? GapScore,
    DateTime? EvaluationDateUtc,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record UpdatePositionCompetencyResultRequest(
    string CompetencyCode,
    string? DesiredBehaviors,
    decimal? ExpectedScore,
    decimal? AchievedScore,
    decimal? GapScore,
    DateTime? EvaluationDateUtc,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    Guid ConcurrencyToken);

public sealed record AddSelectionContestRequest(
    string ContestCode,
    string ContestName,
    DateTime ContestDateUtc,
    string ResultCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record UpdateSelectionContestRequest(
    string ContestCode,
    string ContestName,
    DateTime ContestDateUtc,
    string ResultCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    Guid ConcurrencyToken);

public sealed record AddCurricularCompetencyRequest(
    string RequirementTypeCode,
    string RequirementName,
    string CompetencyDomain,
    decimal? ExperienceTimeValue,
    string? MetricCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record UpdateCurricularCompetencyRequest(
    string RequirementTypeCode,
    string RequirementName,
    string CompetencyDomain,
    decimal? ExperienceTimeValue,
    string? MetricCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    Guid ConcurrencyToken);

public sealed record AddIdentificationRequest(
    string IdentificationTypeCode,
    string IdentificationNumber,
    DateTime? IssuedDate,
    DateTime? ExpiryDate,
    string? Issuer,
    bool IsPrimary);

public sealed record UpdateIdentificationRequest(
    string IdentificationTypeCode,
    string IdentificationNumber,
    DateTime? IssuedDate,
    DateTime? ExpiryDate,
    string? Issuer,
    bool IsPrimary,
    Guid ConcurrencyToken);

public sealed record AddAddressRequest(
    string AddressLine,
    string? Country,
    string? Department,
    string? Municipality,
    string? PostalCode,
    bool IsCurrent);

public sealed record UpdateAddressRequest(
    string AddressLine,
    string? Country,
    string? Department,
    string? Municipality,
    string? PostalCode,
    bool IsCurrent,
    Guid ConcurrencyToken);

public sealed record AddEmergencyContactRequest(
    string Name,
    string Relationship,
    string Phone,
    string? Address,
    string? Workplace);

public sealed record UpdateEmergencyContactRequest(
    string Name,
    string Relationship,
    string Phone,
    string? Address,
    string? Workplace,
    Guid ConcurrencyToken);

public sealed record AddFamilyMemberRequest(
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

public sealed record UpdateFamilyMemberRequest(
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
    DateTime? DeceasedDate,
    Guid ConcurrencyToken);

public sealed record AddHobbyRequest(string HobbyName);

public sealed record UpdateHobbyRequest(string HobbyName, Guid ConcurrencyToken);

public sealed record AddEmployeeRelationRequest(
    Guid RelatedEmployeePublicId,
    string Relationship);

public sealed record UpdateEmployeeRelationRequest(
    Guid RelatedEmployeePublicId,
    string Relationship,
    Guid ConcurrencyToken);

public sealed record AddBankAccountRequest(
    Guid BankPublicId,
    string CurrencyCode,
    string AccountNumber,
    string AccountTypeCode,
    bool IsPrimary);

public sealed record UpdateBankAccountRequest(
    Guid BankPublicId,
    string CurrencyCode,
    string AccountNumber,
    string AccountTypeCode,
    bool IsPrimary,
    Guid ConcurrencyToken);

public sealed record AddAssociationRequest(
    string AssociationName,
    string? Role,
    DateTime? JoinedDate,
    DateTime? LeftDate,
    decimal? Payment);

public sealed record UpdateAssociationRequest(
    string AssociationName,
    string? Role,
    DateTime? JoinedDate,
    DateTime? LeftDate,
    decimal? Payment,
    Guid ConcurrencyToken);

public sealed record EducationItemRequest(
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

public sealed record AddEducationRequest(
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

public sealed record UpdateEducationRequest(
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
    int? ApprovedSubjects,
    Guid ConcurrencyToken);

public sealed record AddLanguageRequest(
    string LanguageCode,
    string LevelCode,
    bool Speaks,
    bool Writes,
    bool Reads);

public sealed record UpdateLanguageRequest(
    string LanguageCode,
    string LevelCode,
    bool Speaks,
    bool Writes,
    bool Reads,
    Guid ConcurrencyToken);

public sealed record AddTrainingRequest(
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

public sealed record UpdateTrainingRequest(
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
    string? CostCurrencyCode,
    Guid ConcurrencyToken);

public sealed record AddPreviousEmploymentRequest(
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

public sealed record UpdatePreviousEmploymentRequest(
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
    string CurrencyCode,
    Guid ConcurrencyToken);

public sealed record AddReferenceRequest(
    string PersonName,
    string? Address,
    string Phone,
    string ReferenceTypeCode,
    string? Occupation,
    string? Workplace,
    string? WorkPhone,
    decimal KnownTimeYears);

public sealed record UpdateReferenceRequest(
    string PersonName,
    string? Address,
    string Phone,
    string ReferenceTypeCode,
    string? Occupation,
    string? Workplace,
    string? WorkPhone,
    decimal KnownTimeYears,
    Guid ConcurrencyToken);

public sealed record DynamicPersonnelFileFilterRequest(
    string Field,
    string Operator,
    string? Value,
    string? ValueTo,
    IReadOnlyCollection<string>? Values);

public sealed record DynamicPersonnelFileSortRequest(
    string Field,
    PersonnelFileSortDirection Direction = PersonnelFileSortDirection.Asc);

public sealed record DynamicQueryPersonnelFilesRequest(
    IReadOnlyCollection<DynamicPersonnelFileFilterRequest>? Filters,
    IReadOnlyCollection<string>? GroupBy,
    IReadOnlyCollection<DynamicPersonnelFileSortRequest>? Sort,
    string? Q,
    int Page = 1,
    int PageSize = PersonnelFileValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false);

public sealed record AddPersonnelFileDocumentRequest(
    Guid FilePublicId,
    Guid DocumentTypeCatalogItemPublicId,
    string? Observations);

public sealed record UpdatePersonnelFileDocumentRequest(
    Guid DocumentTypeCatalogItemPublicId,
    string? Observations,
    Guid? FilePublicId,
    Guid ConcurrencyToken);

// ReplacePersonnelFileDocumentsRequest, ReplacePersonnelFileDocumentsManifestRequest,
// ReplacePersonnelFileDocumentItemRequest and ReplacePersonnelFileDocumentFileRequest removed
// — bulk-replace pattern replaced by atomic PUT /documents/{documentPublicId}.

public sealed record ConcurrencyRequest(Guid ConcurrencyToken);

public sealed record AddObservationRequest(string Note);


