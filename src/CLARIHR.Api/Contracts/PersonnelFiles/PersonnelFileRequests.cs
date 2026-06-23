using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Common;
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
    Guid? OrgUnitPublicId);

/// <summary>
/// JSON Patch (RFC 6902) target for <c>PATCH /personnel-files/{publicId}</c>. The patchable
/// members are the core personal-info fields, <c>isActive</c>, and the rehire-block mark
/// (<c>isRehireBlocked</c>/<c>rehireBlockedReason</c>, typically set at retirement); the concurrency token is
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
    public bool IsActive { get; set; }

    /// <summary>Manual "not rehireable" mark (D-11), usually set together with <c>isActive=false</c> at retirement.</summary>
    public bool IsRehireBlocked { get; set; }

    /// <summary>Optional justification for the rehire block (max 500 chars).</summary>
    public string? RehireBlockedReason { get; set; }
}

public sealed record FinalizePersonnelFileRequest(bool? CreateUserAccount, Guid? PositionSlotPublicId);

/// <summary>
/// Body for <c>POST /personnel-files/{publicId}/rehire</c>. Opens a new employment period on the
/// retired employee's existing file. <c>concurrencyToken</c> travels in the <c>If-Match</c> header.
/// <c>newInstitutionalEmail</c> is only required when the previous email was reassigned (D-09);
/// <c>authorizationReason</c> is required when the file is marked "not rehireable" (D-04).
/// </summary>
public sealed record RehireEmployeeRequest(
    DateTime NewHireDate,
    string ContractTypeCode,
    DateTime ContractStartDate,
    DateTime? ContractEndDate,
    Guid PositionSlotPublicId,
    string AssignmentTypeCode,
    bool? CreateUserAccount,
    string? NewInstitutionalEmail,
    bool PriorPeriodClosureConfirmed,
    string? AuthorizationReason);

public sealed record UpdatePersonnelFileEmployeeProfileRequest(
    string EmployeeCode,
    string EmploymentStatusCode,
    DateTime HireDate,
    string? RetirementCategoryCode,
    string? RetirementReasonCode,
    string? RetirementNotes,
    DateTime? RetirementDate,
    // The institutional email is the employee's login. Supply it to change it (record + linked account);
    // omit/leave null to keep the current one — it cannot be cleared while a login account is linked.
    string? InstitutionalEmail = null);

public sealed record AddEmploymentAssignmentRequest(
    string AssignmentTypeCode,
    string? ContractTypeCode,
    string? WorkdayCode,
    string? PayrollTypeCode,
    Guid? PositionSlotPublicId,
    Guid? OrgUnitPublicId,
    Guid? WorkCenterPublicId,
    Guid? CostCenterPublicId,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPrimary,
    bool IsActive,
    string? Notes,
    string? PaymentMethodCode = null,
    Guid? PaymentBankAccountPublicId = null);

public sealed record UpdateEmploymentAssignmentRequest(
    string AssignmentTypeCode,
    string? ContractTypeCode,
    string? WorkdayCode,
    string? PayrollTypeCode,
    Guid? PositionSlotPublicId,
    Guid? OrgUnitPublicId,
    Guid? WorkCenterPublicId,
    Guid? CostCenterPublicId,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPrimary,
    string? Notes,
    string? PaymentMethodCode = null,
    Guid? PaymentBankAccountPublicId = null);

public sealed class PatchEmploymentAssignmentRequest
{
    public string AssignmentTypeCode { get; set; } = string.Empty;
    public string? ContractTypeCode { get; set; }
    public string? WorkdayCode { get; set; }
    public string? PayrollTypeCode { get; set; }
    public string? PaymentMethodCode { get; set; }
    public Guid? PaymentBankAccountPublicId { get; set; }
    public Guid? PositionSlotId { get; set; }
    public Guid? OrgUnitId { get; set; }
    public Guid? WorkCenterId { get; set; }
    public Guid? CostCenterId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

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
    string? Notes);

public sealed class PatchContractHistoryRequest
{
    public string ContractTypeCode { get; set; } = string.Empty;
    public DateTime ContractDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public Guid? PositionSlotId { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public sealed record AddAuthorizationSubstitutionRequest(
    string SubstitutionTypeCode,
    Guid SubstitutePersonnelFilePublicId,
    Guid SubstitutePositionSlotPublicId,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    string? Notes);

public sealed record UpdateAuthorizationSubstitutionRequest(
    string SubstitutionTypeCode,
    Guid SubstitutePersonnelFilePublicId,
    Guid SubstitutePositionSlotPublicId,
    DateTime StartDate,
    DateTime EndDate,
    string? Notes);

public sealed class PatchAuthorizationSubstitutionRequest
{
    public string SubstitutionTypeCode { get; set; } = string.Empty;
    public Guid SubstitutePersonnelFileId { get; set; }
    public Guid SubstitutePositionSlotId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

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
    string? Notes);

public sealed class PatchAssetAccessRequest
{
    public string AssetTypeCode { get; set; } = string.Empty;
    public string AssetOrAccessName { get; set; } = string.Empty;
    public string? AccessLevelCode { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public DateTime? DeliveryDateUtc { get; set; }
    public string? DeliveryStatusCode { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

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

// ─── Atomic compensation request contracts ────────────────────────────────────

public sealed record AddCompensationConceptRequest(
    Guid? AssignedPositionPublicId,
    CompensationNature Nature,
    string ConceptTypeCode,
    DeductionClass? DeductionClass,
    CompensationCalculationType CalculationType,
    decimal Value,
    string? CalculationBaseCode,
    decimal? EmployerRate,
    decimal? ContributionCap,
    string CurrencyCode,
    string PayPeriodCode,
    string? CounterpartyName,
    string? ExternalReference,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes);

public sealed record UpdateCompensationConceptRequest(
    Guid? AssignedPositionPublicId,
    CompensationNature Nature,
    string ConceptTypeCode,
    DeductionClass? DeductionClass,
    CompensationCalculationType CalculationType,
    decimal Value,
    string? CalculationBaseCode,
    decimal? EmployerRate,
    decimal? ContributionCap,
    string CurrencyCode,
    string PayPeriodCode,
    string? CounterpartyName,
    string? ExternalReference,
    DateTime StartDate,
    DateTime? EndDate,
    string? Notes);

public sealed class PatchCompensationConceptRequest
{
    public Guid? AssignedPositionPublicId { get; set; }
    public CompensationNature Nature { get; set; }
    public string ConceptTypeCode { get; set; } = string.Empty;
    public DeductionClass? DeductionClass { get; set; }
    public CompensationCalculationType CalculationType { get; set; }
    public decimal Value { get; set; }
    public string? CalculationBaseCode { get; set; }
    public decimal? EmployerRate { get; set; }
    public decimal? ContributionCap { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string PayPeriodCode { get; set; } = string.Empty;
    public string? CounterpartyName { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

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
    string? Notes);

public sealed class PatchAdditionalBenefitRequest
{
    public string BenefitTypeCode { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

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

public sealed class PatchPayrollTransactionRequest
{
    public bool IsActive { get; set; }
}

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
    DateTime? EndDateUtc);

public sealed record UpdateInsuranceRequest(
    string InsuranceCode,
    decimal? EmployeeContribution,
    decimal? EmployerContribution,
    string? RangeCode,
    string? PolicyNumber,
    decimal? InsuredAmount,
    string? CurrencyCode,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc);

public sealed class PatchInsuranceRequest
{
    public string InsuranceCode { get; set; } = string.Empty;
    public decimal? EmployeeContribution { get; set; }
    public decimal? EmployerContribution { get; set; }
    public string? RangeCode { get; set; }
    public string? PolicyNumber { get; set; }
    public decimal? InsuredAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public bool IsActive { get; set; }
}

public sealed record AddInsuranceBeneficiaryRequest(
    string FullName,
    string? DocumentNumber,
    string? DocumentTypeCode,
    DateTime? BirthDate,
    string KinshipCode,
    decimal? AllocationPercentage,
    string? BeneficiaryType);

public sealed record UpdateInsuranceBeneficiaryRequest(
    string FullName,
    string? DocumentNumber,
    string? DocumentTypeCode,
    DateTime? BirthDate,
    string KinshipCode,
    decimal? AllocationPercentage,
    string? BeneficiaryType);

public sealed class PatchInsuranceBeneficiaryRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public string? DocumentTypeCode { get; set; }
    public DateTime? BirthDate { get; set; }
    public string KinshipCode { get; set; } = string.Empty;
    public decimal? AllocationPercentage { get; set; }
    public string? BeneficiaryType { get; set; }
    public bool IsActive { get; set; }
}

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
    DateTime? SourceSyncedUtc);

public sealed class PatchMedicalClaimRequest
{
    public Guid? InsurancePublicId { get; set; }
    public string? AccountNumber { get; set; }
    public string ClaimTypeCode { get; set; } = string.Empty;
    public string? Diagnosis { get; set; }
    public decimal? ClaimAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? PaidAmount { get; set; }
    public int? ResponseTimeDays { get; set; }
    public string? Notes { get; set; }
    public DateTime ClaimDateUtc { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceReference { get; set; }
    public DateTime? SourceSyncedUtc { get; set; }
    public bool IsActive { get; set; }
}

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
    DateTime? SourceSyncedUtc);

public sealed class PatchPerformanceEvaluationRequest
{
    public string EvaluatorName { get; set; } = string.Empty;
    public DateTime EvaluationDateUtc { get; set; }
    public decimal? Score { get; set; }
    public string? QualitativeScoreCode { get; set; }
    public string? Comment { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceReference { get; set; }
    public DateTime? SourceSyncedUtc { get; set; }
}

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
    DateTime? SourceSyncedUtc);

public sealed class PatchPositionCompetencyResultRequest
{
    public string CompetencyCode { get; set; } = string.Empty;
    public string? DesiredBehaviors { get; set; }
    public decimal? ExpectedScore { get; set; }
    public decimal? AchievedScore { get; set; }
    public decimal? GapScore { get; set; }
    public DateTime? EvaluationDateUtc { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceReference { get; set; }
    public DateTime? SourceSyncedUtc { get; set; }
}

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
    DateTime? SourceSyncedUtc);

public sealed class PatchSelectionContestRequest
{
    public string ContestCode { get; set; } = string.Empty;
    public string ContestName { get; set; } = string.Empty;
    public DateTime ContestDateUtc { get; set; }
    public string ResultCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceReference { get; set; }
    public DateTime? SourceSyncedUtc { get; set; }
}

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
    DateTime? SourceSyncedUtc);

public sealed class PatchCurricularCompetencyRequest
{
    public string RequirementTypeCode { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;
    public string CompetencyDomain { get; set; } = string.Empty;
    public decimal? ExperienceTimeValue { get; set; }
    public string? MetricCode { get; set; }
    public string? Notes { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceReference { get; set; }
    public DateTime? SourceSyncedUtc { get; set; }
}

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
    bool IsPrimary);

public sealed class PatchIdentificationRequest
{
    public string IdentificationTypeCode { get; set; } = string.Empty;
    public string IdentificationNumber { get; set; } = string.Empty;
    public DateTime? IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Issuer { get; set; }
    public bool IsPrimary { get; set; }
}

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
    bool IsCurrent);

public sealed class PatchAddressRequest
{
    public string AddressLine { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Department { get; set; }
    public string? Municipality { get; set; }
    public string? PostalCode { get; set; }
    public bool IsCurrent { get; set; }
}

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
    string? Workplace);

public sealed class PatchEmergencyContactRequest
{
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Workplace { get; set; }
}

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
    DateTime? DeceasedDate);

public sealed class PatchFamilyMemberRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string KinshipCode { get; set; } = string.Empty;
    public string? Nationality { get; set; }
    public DateTime? BirthDate { get; set; }
    public PersonnelFamilyMemberSex Sex { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Occupation { get; set; }
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Phone { get; set; }
    public bool IsStudying { get; set; }
    public string? StudyPlace { get; set; }
    public string? AcademicLevel { get; set; }
    public bool IsBeneficiary { get; set; }
    public bool IsWorking { get; set; }
    public string? Workplace { get; set; }
    public string? JobTitle { get; set; }
    public string? WorkPhone { get; set; }
    public decimal? Salary { get; set; }
    public bool IsDeceased { get; set; }
    public DateTime? DeceasedDate { get; set; }
}

public sealed record AddHobbyRequest(string HobbyName);

public sealed record UpdateHobbyRequest(string HobbyName);

public sealed class PatchHobbyRequest
{
    public string HobbyName { get; set; } = string.Empty;
}

public sealed record AddEmployeeRelationRequest(
    Guid RelatedEmployeePublicId,
    string Relationship);

public sealed record UpdateEmployeeRelationRequest(
    Guid RelatedEmployeePublicId,
    string Relationship);

public sealed class PatchEmployeeRelationRequest
{
    public Guid RelatedEmployeePublicId { get; set; }
    public string Relationship { get; set; } = string.Empty;
}

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
    bool IsPrimary);

public sealed class PatchBankAccountRequest
{
    public Guid BankPublicId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountTypeCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

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
    decimal? Payment);

public sealed class PatchAssociationRequest
{
    public string AssociationName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public DateTime? JoinedDate { get; set; }
    public DateTime? LeftDate { get; set; }
    public decimal? Payment { get; set; }
}

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
    int? ApprovedSubjects);

/// <summary>
/// Shape of the JSON Patch document body for <c>PATCH /personnel-files/{publicId}/educations/{educationPublicId}</c>.
/// Only used to generate the OpenAPI schema; the operations are applied server-side by the
/// education patch applier. Concurrency is carried by the <c>If-Match</c> header, not the body.
/// </summary>
public sealed class PatchEducationRequest
{
    public Guid StatusPublicId { get; set; }
    public string? DegreeTitle { get; set; }
    public Guid StudyTypePublicId { get; set; }
    public Guid CareerPublicId { get; set; }
    public string Institution { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public bool IsCurrentlyStudying { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? ShiftPublicId { get; set; }
    public Guid? ModalityPublicId { get; set; }
    public int? TotalSubjects { get; set; }
    public int? ApprovedSubjects { get; set; }
}

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
    bool Reads);

public sealed class PatchLanguageRequest
{
    public string LanguageCode { get; set; } = string.Empty;
    public string LevelCode { get; set; } = string.Empty;
    public bool Speaks { get; set; }
    public bool Writes { get; set; }
    public bool Reads { get; set; }
}

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
    string? CostCurrencyCode);

public sealed class PatchTrainingRequest
{
    public string TrainingName { get; set; } = string.Empty;
    public string TrainingTypeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Topic { get; set; }
    public string? Institution { get; set; }
    public string? Instructors { get; set; }
    public decimal? Score { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsInternal { get; set; }
    public bool IsLocal { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public decimal DurationValue { get; set; }
    public string DurationUnitCode { get; set; } = string.Empty;
    public decimal? CostAmount { get; set; }
    public string? CostCurrencyCode { get; set; }
}

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
    string CurrencyCode);

public sealed class PatchPreviousEmploymentRequest
{
    public string Institution { get; set; } = string.Empty;
    public string? Place { get; set; }
    public string? LastPosition { get; set; }
    public string? ManagerName { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime? RetirementDate { get; set; }
    public string? CompanyPhone { get; set; }
    public string? ExitReason { get; set; }
    public decimal? FirstSalaryAmount { get; set; }
    public decimal? LastSalaryAmount { get; set; }
    public decimal? AverageCommissionAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}

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
    decimal KnownTimeYears);

public sealed class PatchReferenceRequest
{
    public string PersonName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string ReferenceTypeCode { get; set; } = string.Empty;
    public string? Occupation { get; set; }
    public string? Workplace { get; set; }
    public string? WorkPhone { get; set; }
    public decimal KnownTimeYears { get; set; }
}

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
    Guid? FilePublicId);

// ReplacePersonnelFileDocumentsRequest, ReplacePersonnelFileDocumentsManifestRequest,
// ReplacePersonnelFileDocumentItemRequest and ReplacePersonnelFileDocumentFileRequest removed
// — bulk-replace pattern replaced by atomic PUT /documents/{documentPublicId}.

/// <summary>
/// JSON Patch (RFC 6902) target for
/// <c>PATCH /personnel-files/{publicId}/documents/{documentPublicId}</c>. The patchable members are
/// the document's metadata fields; the file content itself is replaced via the <c>PUT</c> endpoint.
/// </summary>
public sealed class PatchDocumentRequest
{
    public Guid DocumentTypeCatalogItemPublicId { get; set; }
    public string? Observations { get; set; }
}

public sealed record AddObservationRequest(string Note);


