using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileEmployeeProfileResponse(
    Guid Id,
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
    Guid? PositionSlotId,
    Guid? JobProfileId,
    Guid? OrgUnitId,
    Guid? WorkCenterId,
    Guid? CostCenterId,
    DateTime? ContractStartDate,
    DateTime? ContractEndDate,
    string? VacationConfigurationJson,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PersonnelFileEmploymentAssignmentResponse(
    Guid Id,
    string AssignmentTypeCode,
    Guid? PositionSlotId,
    Guid? OrgUnitId,
    Guid? WorkCenterId,
    Guid? CostCenterId,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPrimary,
    bool IsActive,
    string? Notes);

public sealed record PersonnelFileContractHistoryResponse(
    Guid Id,
    string ContractTypeCode,
    DateTime ContractDate,
    DateTime? ContractEndDate,
    Guid? PositionSlotId,
    string? Notes);

public sealed record PersonnelFilePositionHierarchyResponse(
    Guid PersonnelFileId,
    Guid? OrgUnitId,
    Guid? ImmediateSupervisorPersonnelFileId,
    string? ImmediateSupervisorName,
    IReadOnlyCollection<PersonnelFilePositionHierarchySubordinateResponse> Subordinates);

public sealed record PersonnelFilePositionHierarchySubordinateResponse(
    Guid PersonnelFileId,
    string FullName,
    Guid? OrgUnitId);

public sealed record PersonnelFileSalaryItemResponse(
    Guid Id,
    string IncomeTypeCode,
    string SalaryRubricCode,
    string CurrencyCode,
    string PayPeriodCode,
    decimal Amount,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive);

public sealed record PersonnelFileAdditionalBenefitResponse(
    Guid Id,
    string BenefitTypeCode,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes);

public sealed record PersonnelFilePaymentMethodResponse(
    Guid Id,
    string PaymentMethodCode,
    Guid? BankAccountId,
    bool IsPrimary,
    bool IsActive,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes);

public sealed record PersonnelFileAuthorizationSubstitutionResponse(
    Guid Id,
    string SubstitutionTypeCode,
    Guid SubstitutePersonnelFileId,
    string? SubstitutePositionTitle,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes);

public sealed record PersonnelFilePersonnelActionResponse(
    Guid Id,
    string ActionTypeCode,
    string ActionStatusCode,
    DateTime ActionDateUtc,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Description,
    string? Reference,
    decimal? Amount,
    string? CurrencyCode,
    bool IsSystemGenerated,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PersonnelFilePersonnelActionExportRow(
    Guid Id,
    string ActionTypeCode,
    string ActionStatusCode,
    DateTime ActionDateUtc,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Description,
    string? Reference,
    decimal? Amount,
    string? CurrencyCode,
    bool IsSystemGenerated,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PersonnelFilePayrollTransactionResponse(
    Guid Id,
    string TransactionTypeCode,
    DateTime TransactionDateUtc,
    string PayrollPeriodCode,
    string? Description,
    decimal Amount,
    string CurrencyCode,
    bool IsDebit,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PersonnelFilePayrollTransactionExportRow(
    Guid Id,
    string TransactionTypeCode,
    DateTime TransactionDateUtc,
    string PayrollPeriodCode,
    string? Description,
    decimal Amount,
    string CurrencyCode,
    bool IsDebit,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PersonnelFileAssetAccessResponse(
    Guid Id,
    string AssetTypeCode,
    string AssetOrAccessName,
    string? AccessLevelCode,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    DateTime? DeliveryDateUtc,
    string? DeliveryStatusCode,
    bool IsActive,
    string? Notes);

public sealed record PersonnelFileInsuranceBeneficiaryResponse(
    Guid Id,
    string FullName,
    string? DocumentNumber,
    DateTime? BirthDate,
    string KinshipCode,
    bool IsActive);

public sealed record PersonnelFileInsuranceResponse(
    Guid Id,
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
    IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryResponse> Beneficiaries);

public sealed record PersonnelFileMedicalClaimResponse(
    Guid Id,
    Guid? InsuranceId,
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

public sealed record PersonnelFilePerformanceEvaluationResponse(
    Guid Id,
    string EvaluatorName,
    DateTime EvaluationDateUtc,
    decimal? Score,
    string? QualitativeScoreCode,
    string? Comment,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record PersonnelFilePositionCompetencyResultResponse(
    Guid Id,
    string CompetencyCode,
    string? DesiredBehaviors,
    decimal? ExpectedScore,
    decimal? AchievedScore,
    decimal? GapScore,
    DateTime? EvaluationDateUtc,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record PersonnelFileSelectionContestResponse(
    Guid Id,
    string ContestCode,
    string ContestName,
    DateTime ContestDateUtc,
    string ResultCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record PersonnelFileCurricularCompetencyResponse(
    Guid Id,
    string RequirementTypeCode,
    string RequirementName,
    string CompetencyDomain,
    decimal? ExperienceTimeValue,
    string? MetricCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record UpdatePersonnelFileEmployeeProfileCommand(
    Guid PersonnelFileId,
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
    Guid? PositionSlotId,
    Guid? JobProfileId,
    Guid? OrgUnitId,
    Guid? WorkCenterId,
    Guid? CostCenterId,
    DateTime? ContractStartDate,
    DateTime? ContractEndDate,
    string? VacationConfigurationJson,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<PersonnelFileEmployeeProfileResponse>>;

public sealed record GetPersonnelFileEmployeeProfileQuery(Guid PersonnelFileId)
    : IQuery<PersonnelFileEmployeeProfileResponse>;

public sealed record EmploymentAssignmentInput(
    string AssignmentTypeCode,
    Guid? PositionSlotId,
    Guid? OrgUnitId,
    Guid? WorkCenterId,
    Guid? CostCenterId,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPrimary,
    bool IsActive,
    string? Notes);

public sealed record AddPersonnelFileEmploymentAssignmentCommand(
    Guid PersonnelFileId,
    EmploymentAssignmentInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>;

public sealed record UpdatePersonnelFileEmploymentAssignmentCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    EmploymentAssignmentInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<PersonnelFileEmploymentAssignmentResponse>>;

public sealed record DeactivatePersonnelFileEmploymentAssignmentCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult>;

public sealed record GetPersonnelFileEmploymentAssignmentsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>;

public sealed record ContractHistoryInput(
    string ContractTypeCode,
    DateTime ContractDate,
    DateTime? ContractEndDate,
    Guid? PositionSlotId,
    bool IsActive,
    string? Notes);

public sealed record AddPersonnelFileContractHistoryCommand(
    Guid PersonnelFileId,
    ContractHistoryInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>;

public sealed record UpdatePersonnelFileContractHistoryCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    ContractHistoryInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<PersonnelFileContractHistoryResponse>>;

public sealed record DeactivatePersonnelFileContractHistoryCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult>;

public sealed record GetPersonnelFileContractHistoryQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>;

public sealed record GetPersonnelFilePositionHierarchyQuery(Guid PersonnelFileId)
    : IQuery<PersonnelFilePositionHierarchyResponse>;

public sealed record SalaryItemInput(
    string IncomeTypeCode,
    string SalaryRubricCode,
    string CurrencyCode,
    string PayPeriodCode,
    decimal Amount,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive);

public sealed record AddPersonnelFileSalaryItemCommand(
    Guid PersonnelFileId,
    SalaryItemInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>;

public sealed record UpdatePersonnelFileSalaryItemCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    SalaryItemInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>;

public sealed record DeactivatePersonnelFileSalaryItemCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>;

public sealed record GetPersonnelFileSalaryItemsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>;

public sealed record AdditionalBenefitInput(
    string BenefitTypeCode,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes);

public sealed record AddPersonnelFileAdditionalBenefitCommand(
    Guid PersonnelFileId,
    AdditionalBenefitInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>;

public sealed record UpdatePersonnelFileAdditionalBenefitCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    AdditionalBenefitInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>;

public sealed record DeactivatePersonnelFileAdditionalBenefitCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>;

public sealed record GetPersonnelFileAdditionalBenefitsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>;

public sealed record PaymentMethodInput(
    string PaymentMethodCode,
    Guid? BankAccountPublicId,
    bool IsPrimary,
    bool IsActive,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes);

public sealed record AddPersonnelFilePaymentMethodCommand(
    Guid PersonnelFileId,
    PaymentMethodInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>;

public sealed record UpdatePersonnelFilePaymentMethodCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    PaymentMethodInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>;

public sealed record DeactivatePersonnelFilePaymentMethodCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>;

public sealed record GetPersonnelFilePaymentMethodsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>;

public sealed record AuthorizationSubstitutionInput(
    string SubstitutionTypeCode,
    Guid SubstitutePersonnelFileId,
    string? SubstitutePositionTitle,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes);

public sealed record AddPersonnelFileAuthorizationSubstitutionCommand(
    Guid PersonnelFileId,
    AuthorizationSubstitutionInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>;

public sealed record UpdatePersonnelFileAuthorizationSubstitutionCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    AuthorizationSubstitutionInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<PersonnelFileAuthorizationSubstitutionResponse>>;

public sealed record DeactivatePersonnelFileAuthorizationSubstitutionCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult>;

public sealed record GetPersonnelFileAuthorizationSubstitutionsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>;

public sealed record AddPersonnelFilePersonnelActionCommand(
    Guid PersonnelFileId,
    string ActionTypeCode,
    string ActionStatusCode,
    DateTime ActionDateUtc,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Description,
    string? Reference,
    decimal? Amount,
    string? CurrencyCode,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFilePersonnelActionResponse>;

public sealed record SearchPersonnelFilePersonnelActionsQuery(
    Guid PersonnelFileId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Type,
    string? Status,
    string? Search,
    string? SortBy = null,
    PersonnelFileSortDirection SortDirection = PersonnelFileSortDirection.Desc,
    int PageNumber = 1,
    int PageSize = PersonnelFileValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PersonnelFilePersonnelActionResponse>>;

public sealed record ExportPersonnelFilePersonnelActionsQuery(
    Guid PersonnelFileId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Type,
    string? Status,
    string? Search,
    string? SortBy = null,
    PersonnelFileSortDirection SortDirection = PersonnelFileSortDirection.Desc,
    int? MaxRows = null)
    : IQuery<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>;

public sealed record PayrollTransactionInput(
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

public sealed record AddPersonnelFilePayrollTransactionCommand(
    Guid PersonnelFileId,
    PayrollTransactionInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFilePayrollTransactionResponse>;

public sealed record DeactivatePersonnelFilePayrollTransactionCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>;

public sealed record SearchPersonnelFilePayrollTransactionsQuery(
    Guid PersonnelFileId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Type,
    string? Status,
    string? Search,
    string? SortBy = null,
    PersonnelFileSortDirection SortDirection = PersonnelFileSortDirection.Desc,
    int PageNumber = 1,
    int PageSize = PersonnelFileValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PersonnelFilePayrollTransactionResponse>>;

public sealed record ExportPersonnelFilePayrollTransactionsQuery(
    Guid PersonnelFileId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Type,
    string? Status,
    string? Search,
    string? SortBy = null,
    PersonnelFileSortDirection SortDirection = PersonnelFileSortDirection.Desc,
    int? MaxRows = null)
    : IQuery<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>;

public sealed record AssetAccessInput(
    string AssetTypeCode,
    string AssetOrAccessName,
    string? AccessLevelCode,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    DateTime? DeliveryDateUtc,
    string? DeliveryStatusCode,
    bool IsActive,
    string? Notes);

public sealed record AddPersonnelFileAssetAccessCommand(
    Guid PersonnelFileId,
    AssetAccessInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>;

public sealed record UpdatePersonnelFileAssetAccessCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    AssetAccessInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<PersonnelFileAssetAccessResponse>>;

public sealed record DeactivatePersonnelFileAssetAccessCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult>;

public sealed record GetPersonnelFileAssetsAccessesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>;

public sealed record InsuranceBeneficiaryInput(
    string FullName,
    string? DocumentNumber,
    DateTime? BirthDate,
    string KinshipCode);

public sealed record InsuranceInput(
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
    IReadOnlyCollection<InsuranceBeneficiaryInput> Beneficiaries);

public sealed record AddPersonnelFileInsuranceCommand(
    Guid PersonnelFileId,
    InsuranceInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>;

public sealed record UpdatePersonnelFileInsuranceCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    InsuranceInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>;

public sealed record DeactivatePersonnelFileInsuranceCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>;

public sealed record GetPersonnelFileInsurancesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileInsuranceResponse>>;

public sealed record MedicalClaimInput(
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

public sealed record AddPersonnelFileMedicalClaimCommand(
    Guid PersonnelFileId,
    MedicalClaimInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>;

public sealed record UpdatePersonnelFileMedicalClaimCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    MedicalClaimInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>;

public sealed record DeactivatePersonnelFileMedicalClaimCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>;

public sealed record GetPersonnelFileMedicalClaimsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>;

public sealed record PerformanceEvaluationInput(
    string EvaluatorName,
    DateTime EvaluationDateUtc,
    decimal? Score,
    string? QualitativeScoreCode,
    string? Comment,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record AddPersonnelFilePerformanceEvaluationCommand(
    Guid PersonnelFileId,
    PerformanceEvaluationInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>;

public sealed record UpdatePersonnelFilePerformanceEvaluationCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    PerformanceEvaluationInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>;

public sealed record GetPersonnelFilePerformanceEvaluationsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>;

public sealed record PositionCompetencyResultInput(
    string CompetencyCode,
    string? DesiredBehaviors,
    decimal? ExpectedScore,
    decimal? AchievedScore,
    decimal? GapScore,
    DateTime? EvaluationDateUtc,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record AddPersonnelFilePositionCompetencyResultCommand(
    Guid PersonnelFileId,
    PositionCompetencyResultInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>;

public sealed record UpdatePersonnelFilePositionCompetencyResultCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    PositionCompetencyResultInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>;

public sealed record GetPersonnelFilePositionCompetencyResultsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>;

public sealed record SelectionContestInput(
    string ContestCode,
    string ContestName,
    DateTime ContestDateUtc,
    string ResultCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record AddPersonnelFileSelectionContestCommand(
    Guid PersonnelFileId,
    SelectionContestInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>;

public sealed record UpdatePersonnelFileSelectionContestCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    SelectionContestInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>;

public sealed record GetPersonnelFileSelectionContestsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>;

public sealed record CurricularCompetencyInput(
    string RequirementTypeCode,
    string RequirementName,
    string CompetencyDomain,
    decimal? ExperienceTimeValue,
    string? MetricCode,
    string? Notes,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record AddPersonnelFileCurricularCompetencyCommand(
    Guid PersonnelFileId,
    CurricularCompetencyInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>;

public sealed record UpdatePersonnelFileCurricularCompetencyCommand(
    Guid PersonnelFileId,
    Guid ItemPublicId,
    CurricularCompetencyInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>;

public sealed record GetPersonnelFileCurricularCompetenciesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>;

internal sealed class UpdatePersonnelFileEmployeeProfileCommandValidator : AbstractValidator<UpdatePersonnelFileEmployeeProfileCommand>
{
    public UpdatePersonnelFileEmployeeProfileCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EmployeeCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.EmploymentStatusCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ContractTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEmployeeProfileQueryValidator : AbstractValidator<GetPersonnelFileEmployeeProfileQuery>
{
    public GetPersonnelFileEmployeeProfileQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal abstract class PersonnelFileEmployeeCommandValidatorBase<TCommand, TItem> : AbstractValidator<TCommand>
{
    protected void Configure(GuidAccessor<TCommand> idAccessor, GuidAccessor<TCommand> concurrencyAccessor, CollectionAccessor<TCommand, TItem> itemsAccessor, IValidator<TItem> itemValidator)
    {
        RuleFor(idAccessor.Accessor).NotEmpty();
        RuleFor(concurrencyAccessor.Accessor).NotEmpty();
        RuleFor(itemsAccessor.Accessor).NotNull();
        RuleForEach(itemsAccessor.Accessor).SetValidator(itemValidator);
    }

    protected readonly record struct GuidAccessor<T>(global::System.Linq.Expressions.Expression<Func<T, Guid>> Accessor);
    protected readonly record struct CollectionAccessor<T, TCollectionItem>(global::System.Linq.Expressions.Expression<Func<T, IEnumerable<TCollectionItem>>> Accessor);
}

internal sealed class EmploymentAssignmentInputValidator : AbstractValidator<EmploymentAssignmentInput>
{
    public EmploymentAssignmentInputValidator()
    {
        RuleFor(input => input.AssignmentTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.StartDate).LessThanOrEqualTo(input => input.EndDate!.Value).When(input => input.EndDate.HasValue);
    }
}

internal sealed class ContractHistoryInputValidator : AbstractValidator<ContractHistoryInput>
{
    public ContractHistoryInputValidator()
    {
        RuleFor(input => input.ContractTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.ContractDate).LessThanOrEqualTo(input => input.ContractEndDate!.Value).When(input => input.ContractEndDate.HasValue);
    }
}

internal sealed class SalaryItemInputValidator : AbstractValidator<SalaryItemInput>
{
    public SalaryItemInputValidator()
    {
        RuleFor(input => input.IncomeTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.SalaryRubricCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.CurrencyCode).NotEmpty().MaximumLength(40);
        RuleFor(input => input.PayPeriodCode).NotEmpty().MaximumLength(40);
        RuleFor(input => input.Amount).GreaterThanOrEqualTo(0);
    }
}

internal sealed class AdditionalBenefitInputValidator : AbstractValidator<AdditionalBenefitInput>
{
    public AdditionalBenefitInputValidator()
    {
        RuleFor(input => input.BenefitTypeCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class PaymentMethodInputValidator : AbstractValidator<PaymentMethodInput>
{
    public PaymentMethodInputValidator()
    {
        RuleFor(input => input.PaymentMethodCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.EffectiveFromUtc).LessThanOrEqualTo(input => input.EffectiveToUtc!.Value).When(input => input.EffectiveToUtc.HasValue);
    }
}

internal sealed class AuthorizationSubstitutionInputValidator : AbstractValidator<AuthorizationSubstitutionInput>
{
    public AuthorizationSubstitutionInputValidator()
    {
        RuleFor(input => input.SubstitutionTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.SubstitutePersonnelFileId).NotEmpty();
        RuleFor(input => input.StartDate).LessThanOrEqualTo(input => input.EndDate!.Value).When(input => input.EndDate.HasValue);
    }
}

internal sealed class PersonnelActionInputValidator : AbstractValidator<AddPersonnelFilePersonnelActionCommand>
{
    public PersonnelActionInputValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ActionTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ActionStatusCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.ActionDateUtc).LessThanOrEqualTo(command => command.EffectiveToUtc!.Value).When(command => command.EffectiveToUtc.HasValue);
    }
}

internal sealed class PayrollTransactionInputValidator : AbstractValidator<PayrollTransactionInput>
{
    public PayrollTransactionInputValidator()
    {
        RuleFor(input => input.TransactionTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.PayrollPeriodCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.CurrencyCode).NotEmpty().MaximumLength(40);
    }
}

internal sealed class AssetAccessInputValidator : AbstractValidator<AssetAccessInput>
{
    public AssetAccessInputValidator()
    {
        RuleFor(input => input.AssetTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.AssetOrAccessName).NotEmpty().MaximumLength(200);
    }
}

internal sealed class InsuranceBeneficiaryInputValidator : AbstractValidator<InsuranceBeneficiaryInput>
{
    public InsuranceBeneficiaryInputValidator()
    {
        RuleFor(input => input.FullName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.KinshipCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class InsuranceInputValidator : AbstractValidator<InsuranceInput>
{
    public InsuranceInputValidator()
    {
        RuleFor(input => input.InsuranceCode).NotEmpty().MaximumLength(80);
        RuleForEach(input => input.Beneficiaries).SetValidator(new InsuranceBeneficiaryInputValidator());
    }
}

internal sealed class MedicalClaimInputValidator : AbstractValidator<MedicalClaimInput>
{
    public MedicalClaimInputValidator()
    {
        RuleFor(input => input.ClaimTypeCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class PerformanceEvaluationInputValidator : AbstractValidator<PerformanceEvaluationInput>
{
    public PerformanceEvaluationInputValidator()
    {
        RuleFor(input => input.EvaluatorName).NotEmpty().MaximumLength(200);
    }
}

internal sealed class PositionCompetencyResultInputValidator : AbstractValidator<PositionCompetencyResultInput>
{
    public PositionCompetencyResultInputValidator()
    {
        RuleFor(input => input.CompetencyCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class SelectionContestInputValidator : AbstractValidator<SelectionContestInput>
{
    public SelectionContestInputValidator()
    {
        RuleFor(input => input.ContestCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.ContestName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.ResultCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class CurricularCompetencyInputValidator : AbstractValidator<CurricularCompetencyInput>
{
    public CurricularCompetencyInputValidator()
    {
        RuleFor(input => input.RequirementTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.RequirementName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.CompetencyDomain).NotEmpty().MaximumLength(120);
    }
}

internal sealed class AddPersonnelFileEmploymentAssignmentCommandValidator : AbstractValidator<AddPersonnelFileEmploymentAssignmentCommand>
{
    public AddPersonnelFileEmploymentAssignmentCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new EmploymentAssignmentInputValidator());
    }
}

internal sealed class UpdatePersonnelFileEmploymentAssignmentCommandValidator : AbstractValidator<UpdatePersonnelFileEmploymentAssignmentCommand>
{
    public UpdatePersonnelFileEmploymentAssignmentCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new EmploymentAssignmentInputValidator());
    }
}

internal sealed class DeactivatePersonnelFileEmploymentAssignmentCommandValidator : AbstractValidator<DeactivatePersonnelFileEmploymentAssignmentCommand>
{
    public DeactivatePersonnelFileEmploymentAssignmentCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEmploymentAssignmentsQueryValidator : AbstractValidator<GetPersonnelFileEmploymentAssignmentsQuery>
{
    public GetPersonnelFileEmploymentAssignmentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileContractHistoryCommandValidator : AbstractValidator<AddPersonnelFileContractHistoryCommand>
{
    public AddPersonnelFileContractHistoryCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new ContractHistoryInputValidator());
    }
}

internal sealed class UpdatePersonnelFileContractHistoryCommandValidator : AbstractValidator<UpdatePersonnelFileContractHistoryCommand>
{
    public UpdatePersonnelFileContractHistoryCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new ContractHistoryInputValidator());
    }
}

internal sealed class DeactivatePersonnelFileContractHistoryCommandValidator : AbstractValidator<DeactivatePersonnelFileContractHistoryCommand>
{
    public DeactivatePersonnelFileContractHistoryCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileContractHistoryQueryValidator : AbstractValidator<GetPersonnelFileContractHistoryQuery>
{
    public GetPersonnelFileContractHistoryQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileSalaryItemCommandValidator : AbstractValidator<AddPersonnelFileSalaryItemCommand>
{
    public AddPersonnelFileSalaryItemCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new SalaryItemInputValidator());
    }
}

internal sealed class UpdatePersonnelFileSalaryItemCommandValidator : AbstractValidator<UpdatePersonnelFileSalaryItemCommand>
{
    public UpdatePersonnelFileSalaryItemCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new SalaryItemInputValidator());
    }
}

internal sealed class DeactivatePersonnelFileSalaryItemCommandValidator : AbstractValidator<DeactivatePersonnelFileSalaryItemCommand>
{
    public DeactivatePersonnelFileSalaryItemCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileSalaryItemsQueryValidator : AbstractValidator<GetPersonnelFileSalaryItemsQuery>
{
    public GetPersonnelFileSalaryItemsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileAdditionalBenefitCommandValidator : AbstractValidator<AddPersonnelFileAdditionalBenefitCommand>
{
    public AddPersonnelFileAdditionalBenefitCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AdditionalBenefitInputValidator());
    }
}

internal sealed class UpdatePersonnelFileAdditionalBenefitCommandValidator : AbstractValidator<UpdatePersonnelFileAdditionalBenefitCommand>
{
    public UpdatePersonnelFileAdditionalBenefitCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AdditionalBenefitInputValidator());
    }
}

internal sealed class DeactivatePersonnelFileAdditionalBenefitCommandValidator : AbstractValidator<DeactivatePersonnelFileAdditionalBenefitCommand>
{
    public DeactivatePersonnelFileAdditionalBenefitCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileAdditionalBenefitsQueryValidator : AbstractValidator<GetPersonnelFileAdditionalBenefitsQuery>
{
    public GetPersonnelFileAdditionalBenefitsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class AddPersonnelFilePaymentMethodCommandValidator : AbstractValidator<AddPersonnelFilePaymentMethodCommand>
{
    public AddPersonnelFilePaymentMethodCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PaymentMethodInputValidator());
    }
}

internal sealed class UpdatePersonnelFilePaymentMethodCommandValidator : AbstractValidator<UpdatePersonnelFilePaymentMethodCommand>
{
    public UpdatePersonnelFilePaymentMethodCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PaymentMethodInputValidator());
    }
}

internal sealed class DeactivatePersonnelFilePaymentMethodCommandValidator : AbstractValidator<DeactivatePersonnelFilePaymentMethodCommand>
{
    public DeactivatePersonnelFilePaymentMethodCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFilePaymentMethodsQueryValidator : AbstractValidator<GetPersonnelFilePaymentMethodsQuery>
{
    public GetPersonnelFilePaymentMethodsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileAuthorizationSubstitutionCommandValidator : AbstractValidator<AddPersonnelFileAuthorizationSubstitutionCommand>
{
    public AddPersonnelFileAuthorizationSubstitutionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AuthorizationSubstitutionInputValidator());
    }
}

internal sealed class UpdatePersonnelFileAuthorizationSubstitutionCommandValidator : AbstractValidator<UpdatePersonnelFileAuthorizationSubstitutionCommand>
{
    public UpdatePersonnelFileAuthorizationSubstitutionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AuthorizationSubstitutionInputValidator());
    }
}

internal sealed class DeactivatePersonnelFileAuthorizationSubstitutionCommandValidator : AbstractValidator<DeactivatePersonnelFileAuthorizationSubstitutionCommand>
{
    public DeactivatePersonnelFileAuthorizationSubstitutionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileAuthorizationSubstitutionsQueryValidator : AbstractValidator<GetPersonnelFileAuthorizationSubstitutionsQuery>
{
    public GetPersonnelFileAuthorizationSubstitutionsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class AddPersonnelFilePayrollTransactionCommandValidator : AbstractValidator<AddPersonnelFilePayrollTransactionCommand>
{
    public AddPersonnelFilePayrollTransactionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PayrollTransactionInputValidator());
    }
}

internal sealed class DeactivatePersonnelFilePayrollTransactionCommandValidator : AbstractValidator<DeactivatePersonnelFilePayrollTransactionCommand>
{
    public DeactivatePersonnelFilePayrollTransactionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileAssetAccessCommandValidator : AbstractValidator<AddPersonnelFileAssetAccessCommand>
{
    public AddPersonnelFileAssetAccessCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AssetAccessInputValidator());
    }
}

internal sealed class UpdatePersonnelFileAssetAccessCommandValidator : AbstractValidator<UpdatePersonnelFileAssetAccessCommand>
{
    public UpdatePersonnelFileAssetAccessCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AssetAccessInputValidator());
    }
}

internal sealed class DeactivatePersonnelFileAssetAccessCommandValidator : AbstractValidator<DeactivatePersonnelFileAssetAccessCommand>
{
    public DeactivatePersonnelFileAssetAccessCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileAssetsAccessesQueryValidator : AbstractValidator<GetPersonnelFileAssetsAccessesQuery>
{
    public GetPersonnelFileAssetsAccessesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileInsuranceCommandValidator : AbstractValidator<AddPersonnelFileInsuranceCommand>
{
    public AddPersonnelFileInsuranceCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new InsuranceInputValidator());
    }
}

internal sealed class UpdatePersonnelFileInsuranceCommandValidator : AbstractValidator<UpdatePersonnelFileInsuranceCommand>
{
    public UpdatePersonnelFileInsuranceCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new InsuranceInputValidator());
    }
}

internal sealed class DeactivatePersonnelFileInsuranceCommandValidator : AbstractValidator<DeactivatePersonnelFileInsuranceCommand>
{
    public DeactivatePersonnelFileInsuranceCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileInsurancesQueryValidator : AbstractValidator<GetPersonnelFileInsurancesQuery>
{
    public GetPersonnelFileInsurancesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileMedicalClaimCommandValidator : AbstractValidator<AddPersonnelFileMedicalClaimCommand>
{
    public AddPersonnelFileMedicalClaimCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new MedicalClaimInputValidator());
    }
}

internal sealed class UpdatePersonnelFileMedicalClaimCommandValidator : AbstractValidator<UpdatePersonnelFileMedicalClaimCommand>
{
    public UpdatePersonnelFileMedicalClaimCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new MedicalClaimInputValidator());
    }
}

internal sealed class DeactivatePersonnelFileMedicalClaimCommandValidator : AbstractValidator<DeactivatePersonnelFileMedicalClaimCommand>
{
    public DeactivatePersonnelFileMedicalClaimCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileMedicalClaimsQueryValidator : AbstractValidator<GetPersonnelFileMedicalClaimsQuery>
{
    public GetPersonnelFileMedicalClaimsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class AddPersonnelFilePerformanceEvaluationCommandValidator : AbstractValidator<AddPersonnelFilePerformanceEvaluationCommand>
{
    public AddPersonnelFilePerformanceEvaluationCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PerformanceEvaluationInputValidator());
    }
}

internal sealed class UpdatePersonnelFilePerformanceEvaluationCommandValidator : AbstractValidator<UpdatePersonnelFilePerformanceEvaluationCommand>
{
    public UpdatePersonnelFilePerformanceEvaluationCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PerformanceEvaluationInputValidator());
    }
}

internal sealed class AddPersonnelFilePositionCompetencyResultCommandValidator : AbstractValidator<AddPersonnelFilePositionCompetencyResultCommand>
{
    public AddPersonnelFilePositionCompetencyResultCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PositionCompetencyResultInputValidator());
    }
}

internal sealed class UpdatePersonnelFilePositionCompetencyResultCommandValidator : AbstractValidator<UpdatePersonnelFilePositionCompetencyResultCommand>
{
    public UpdatePersonnelFilePositionCompetencyResultCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PositionCompetencyResultInputValidator());
    }
}

internal sealed class AddPersonnelFileSelectionContestCommandValidator : AbstractValidator<AddPersonnelFileSelectionContestCommand>
{
    public AddPersonnelFileSelectionContestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new SelectionContestInputValidator());
    }
}

internal sealed class UpdatePersonnelFileSelectionContestCommandValidator : AbstractValidator<UpdatePersonnelFileSelectionContestCommand>
{
    public UpdatePersonnelFileSelectionContestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new SelectionContestInputValidator());
    }
}

internal sealed class AddPersonnelFileCurricularCompetencyCommandValidator : AbstractValidator<AddPersonnelFileCurricularCompetencyCommand>
{
    public AddPersonnelFileCurricularCompetencyCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new CurricularCompetencyInputValidator());
    }
}

internal sealed class UpdatePersonnelFileCurricularCompetencyCommandValidator : AbstractValidator<UpdatePersonnelFileCurricularCompetencyCommand>
{
    public UpdatePersonnelFileCurricularCompetencyCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new CurricularCompetencyInputValidator());
    }
}

internal sealed class GetPersonnelFileCurricularCompetenciesQueryValidator : AbstractValidator<GetPersonnelFileCurricularCompetenciesQuery>
{
    public GetPersonnelFileCurricularCompetenciesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal static class PersonnelFileEmployeeAudits
{
    public static async Task LogUpdateAsync(
        IAuditService auditService,
        PersonnelFile personnelFile,
        string summary,
        object? after,
        CancellationToken cancellationToken) =>
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.PersonnelFileUpdated,
                AuditEntityTypes.PersonnelFile,
                personnelFile.PublicId,
                personnelFile.FullName,
                AuditActions.Update,
                summary,
                Before: null,
                After: after),
            cancellationToken);
}

internal abstract class PersonnelFileEmployeeCommandHandlerBase
{
    protected static PersonnelFileSectionResult<TSection> CreateSectionResult<TSection>(
        PersonnelFile personnelFile,
        TSection data) =>
        new(data, personnelFile.ConcurrencyToken, personnelFile.ModifiedUtc);

    protected static PersonnelFileSectionResult CreateSectionResult(PersonnelFile personnelFile) =>
        new(personnelFile.ConcurrencyToken, personnelFile.ModifiedUtc);

    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        return (null, personnelFile);
    }

    protected static void EnsureEmployeeRecordType(PersonnelFile personnelFile)
    {
        if (!personnelFile.IsCompletedEmployee)
        {
            throw new InvalidOperationException("Personnel file must be a completed employee record.");
        }
    }

    protected static void TouchPersonnelFile(PersonnelFile personnelFile)
    {
        personnelFile.UpdatePersonalInfo(
            personnelFile.RecordType,
            personnelFile.FirstName,
            personnelFile.LastName,
            personnelFile.BirthDate,
            personnelFile.MaritalStatus,
            personnelFile.Profession,
            personnelFile.Nationality,
            personnelFile.PersonalEmail,
            personnelFile.InstitutionalEmail,
            personnelFile.PersonalPhone,
            personnelFile.InstitutionalPhone,
            personnelFile.BirthCountry,
            personnelFile.BirthDepartment,
            personnelFile.BirthMunicipality,
            personnelFile.PhotoUrl,
            personnelFile.OrgUnitPublicId,
            personnelFile.AssignedPositionSlotPublicId);
    }
}

internal abstract class PersonnelFileEmployeeReadQueryHandlerBase : PersonnelFileEmployeeCommandHandlerBase
{
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<TResponse>(
            personnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return (failure, null);
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }
}

internal sealed class UpdatePersonnelFileEmployeeProfileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileEmployeeProfileCommand, PersonnelFileSectionResult<PersonnelFileEmployeeProfileResponse>>
{
    public async Task<Result<PersonnelFileSectionResult<PersonnelFileEmployeeProfileResponse>>> Handle(
        UpdatePersonnelFileEmployeeProfileCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<PersonnelFileEmployeeProfileResponse>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<PersonnelFileEmployeeProfileResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFileEmployeeProfile.Create(
            command.EmployeeCode,
            command.EmploymentStatusCode,
            command.IsEmploymentActive,
            command.ContractTypeCode,
            command.HireDate,
            command.RetirementCategoryCode,
            command.RetirementReasonCode,
            command.RetirementNotes,
            command.RetirementDate,
            command.WorkdayCode,
            command.PayrollTypeCode,
            command.PositionSlotId,
            command.JobProfileId,
            command.OrgUnitId,
            command.WorkCenterId,
            command.CostCenterId,
            command.ContractStartDate,
            command.ContractEndDate,
            command.VacationConfigurationJson);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.UpsertEmployeeProfileAsync(entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService,
                personnelFile,
                $"Updated employee profile for {personnelFile.FullName}.",
                response,
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<PersonnelFileEmployeeProfileResponse>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class GetPersonnelFileEmployeeProfileQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEmployeeProfileQuery, PersonnelFileEmployeeProfileResponse>
{
    public async Task<Result<PersonnelFileEmployeeProfileResponse>> Handle(
        GetPersonnelFileEmployeeProfileQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<PersonnelFileEmployeeProfileResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetEmployeeProfileAsync(personnelFile!.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Employee profile could not be resolved for a completed employee personnel file.");

        return Result<PersonnelFileEmployeeProfileResponse>.Success(response);
    }
}

internal sealed class AddPersonnelFileEmploymentAssignmentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileEmploymentAssignmentCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>> Handle(
        AddPersonnelFileEmploymentAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);

        var entity = PersonnelFileEmploymentAssignment.Create(
            command.Item.AssignmentTypeCode,
            command.Item.PositionSlotId,
            command.Item.OrgUnitId,
            command.Item.WorkCenterId,
            command.Item.CostCenterId,
            command.Item.StartDate,
            command.Item.EndDate,
            command.Item.IsPrimary,
            command.Item.IsActive,
            command.Item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddEmploymentAssignmentAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added employment assignment to {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFileEmploymentAssignmentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileEmploymentAssignmentCommand, PersonnelFileSectionResult<PersonnelFileEmploymentAssignmentResponse>>
{
    public async Task<Result<PersonnelFileSectionResult<PersonnelFileEmploymentAssignmentResponse>>> Handle(
        UpdatePersonnelFileEmploymentAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<PersonnelFileEmploymentAssignmentResponse>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult<PersonnelFileEmploymentAssignmentResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);

        var response = await employeeRepository.UpdateEmploymentAssignmentAsync(
            command.ItemPublicId,
            personnelFile.TenantId,
            command.Item.AssignmentTypeCode,
            command.Item.PositionSlotId,
            command.Item.OrgUnitId,
            command.Item.WorkCenterId,
            command.Item.CostCenterId,
            command.Item.StartDate,
            command.Item.EndDate,
            command.Item.IsPrimary,
            command.Item.IsActive,
            command.Item.Notes,
            cancellationToken);

        if (response is null)
            return Result<PersonnelFileSectionResult<PersonnelFileEmploymentAssignmentResponse>>.Failure(PersonnelFileErrors.NotFound);

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated employment assignment for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<PersonnelFileEmploymentAssignmentResponse>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class DeactivatePersonnelFileEmploymentAssignmentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeactivatePersonnelFileEmploymentAssignmentCommand, PersonnelFileSectionResult>
{
    public async Task<Result<PersonnelFileSectionResult>> Handle(
        DeactivatePersonnelFileEmploymentAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult>.Failure(PersonnelFileErrors.StateRuleViolation);

        var deactivated = await employeeRepository.DeactivateEmploymentAssignmentAsync(command.ItemPublicId, personnelFile.TenantId, cancellationToken);
        if (!deactivated)
            return Result<PersonnelFileSectionResult>.Failure(PersonnelFileErrors.NotFound);

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated employment assignment for {personnelFile.FullName}.", null, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult>.Success(CreateSectionResult(personnelFile));
    }
}

internal sealed class GetPersonnelFileEmploymentAssignmentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEmploymentAssignmentsQuery, IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>> Handle(
        GetPersonnelFileEmploymentAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetEmploymentAssignmentsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>.Success(response);
    }
}

internal sealed class AddPersonnelFileContractHistoryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileContractHistoryCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>> Handle(
        AddPersonnelFileContractHistoryCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);

        var entity = PersonnelFileContractHistory.Create(
            command.Item.ContractTypeCode,
            command.Item.ContractDate,
            command.Item.ContractEndDate,
            command.Item.PositionSlotId,
            command.Item.IsActive,
            command.Item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddContractHistoryAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added contract history to {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFileContractHistoryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileContractHistoryCommand, PersonnelFileSectionResult<PersonnelFileContractHistoryResponse>>
{
    public async Task<Result<PersonnelFileSectionResult<PersonnelFileContractHistoryResponse>>> Handle(
        UpdatePersonnelFileContractHistoryCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<PersonnelFileContractHistoryResponse>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult<PersonnelFileContractHistoryResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);

        var response = await employeeRepository.UpdateContractHistoryAsync(
            command.ItemPublicId,
            personnelFile.TenantId,
            command.Item.ContractTypeCode,
            command.Item.ContractDate,
            command.Item.ContractEndDate,
            command.Item.PositionSlotId,
            command.Item.IsActive,
            command.Item.Notes,
            cancellationToken);

        if (response is null)
            return Result<PersonnelFileSectionResult<PersonnelFileContractHistoryResponse>>.Failure(PersonnelFileErrors.NotFound);

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated contract history for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<PersonnelFileContractHistoryResponse>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class DeactivatePersonnelFileContractHistoryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeactivatePersonnelFileContractHistoryCommand, PersonnelFileSectionResult>
{
    public async Task<Result<PersonnelFileSectionResult>> Handle(
        DeactivatePersonnelFileContractHistoryCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult>.Failure(PersonnelFileErrors.StateRuleViolation);

        var deactivated = await employeeRepository.DeactivateContractHistoryAsync(command.ItemPublicId, personnelFile.TenantId, cancellationToken);
        if (!deactivated)
            return Result<PersonnelFileSectionResult>.Failure(PersonnelFileErrors.NotFound);

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated contract history for {personnelFile.FullName}.", null, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult>.Success(CreateSectionResult(personnelFile));
    }
}

internal sealed class GetPersonnelFileContractHistoryQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileContractHistoryQuery, IReadOnlyCollection<PersonnelFileContractHistoryResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>> Handle(
        GetPersonnelFileContractHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetContractHistoryAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFilePositionHierarchyQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFilePositionHierarchyQuery, PersonnelFilePositionHierarchyResponse>
{
    public async Task<Result<PersonnelFilePositionHierarchyResponse>> Handle(
        GetPersonnelFilePositionHierarchyQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<PersonnelFilePositionHierarchyResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFilePositionHierarchyResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.GetPositionHierarchyAsync(personnelFile!.PublicId, cancellationToken);
        return Result<PersonnelFilePositionHierarchyResponse>.Success(response);
    }
}

internal sealed class AddPersonnelFileSalaryItemCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileSalaryItemCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>> Handle(
        AddPersonnelFileSalaryItemCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFileSalaryItem.Create(
            command.Item.IncomeTypeCode,
            command.Item.SalaryRubricCode,
            command.Item.CurrencyCode,
            command.Item.PayPeriodCode,
            command.Item.Amount,
            command.Item.StartDate,
            command.Item.EndDate,
            command.Item.IsActive);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddSalaryItemAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added salary item for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFileSalaryItemCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileSalaryItemCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>> Handle(
        UpdatePersonnelFileSalaryItemCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.UpdateSalaryItemAsync(
            command.ItemPublicId,
            personnelFile.TenantId,
            command.Item.IncomeTypeCode,
            command.Item.SalaryRubricCode,
            command.Item.CurrencyCode,
            command.Item.PayPeriodCode,
            command.Item.Amount,
            command.Item.StartDate,
            command.Item.EndDate,
            command.Item.IsActive,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated salary item for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var allItems = await employeeRepository.GetSalaryItemsAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class DeactivatePersonnelFileSalaryItemCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeactivatePersonnelFileSalaryItemCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>> Handle(
        DeactivatePersonnelFileSalaryItemCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var found = await employeeRepository.DeactivateSalaryItemAsync(command.ItemPublicId, personnelFile.TenantId, cancellationToken);
        if (!found)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated salary item for {personnelFile.FullName}.", null, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var allItems = await employeeRepository.GetSalaryItemsAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class GetPersonnelFileSalaryItemsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileSalaryItemsQuery, IReadOnlyCollection<PersonnelFileSalaryItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>> Handle(
        GetPersonnelFileSalaryItemsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetSalaryItemsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>.Success(response);
    }
}

internal sealed class AddPersonnelFileAdditionalBenefitCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileAdditionalBenefitCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>> Handle(
        AddPersonnelFileAdditionalBenefitCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFileAdditionalBenefit.Create(command.Item.BenefitTypeCode, command.Item.StartDate, command.Item.EndDate, command.Item.IsActive, command.Item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddAdditionalBenefitAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added additional benefit for {personnelFile.FullName}.", response, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFileAdditionalBenefitCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileAdditionalBenefitCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>> Handle(
        UpdatePersonnelFileAdditionalBenefitCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var found = await employeeRepository.UpdateAdditionalBenefitAsync(command.ItemPublicId, personnelFile.TenantId, command.Item.BenefitTypeCode, command.Item.StartDate, command.Item.EndDate, command.Item.IsActive, command.Item.Notes, cancellationToken);
        if (!found) { return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>.Failure(PersonnelFileErrors.ItemNotFound); }

        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated additional benefit for {personnelFile.FullName}.", null, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        var allItems = await employeeRepository.GetAdditionalBenefitsAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class DeactivatePersonnelFileAdditionalBenefitCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeactivatePersonnelFileAdditionalBenefitCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>> Handle(
        DeactivatePersonnelFileAdditionalBenefitCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var found = await employeeRepository.DeactivateAdditionalBenefitAsync(command.ItemPublicId, personnelFile.TenantId, cancellationToken);
        if (!found) { return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>.Failure(PersonnelFileErrors.ItemNotFound); }

        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated additional benefit for {personnelFile.FullName}.", null, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        var allItems = await employeeRepository.GetAdditionalBenefitsAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class GetPersonnelFileAdditionalBenefitsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAdditionalBenefitsQuery, IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>> Handle(
        GetPersonnelFileAdditionalBenefitsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetAdditionalBenefitsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>.Success(response);
    }
}

internal sealed class AddPersonnelFilePaymentMethodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFilePaymentMethodCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>> Handle(
        AddPersonnelFilePaymentMethodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (command.Item.BankAccountPublicId.HasValue)
        {
            var bankAccountIds = (await personnelFileRepository.GetBankAccountIdsAsync(personnelFile.PublicId, cancellationToken)).ToHashSet();
            if (!bankAccountIds.Contains(command.Item.BankAccountPublicId.Value))
            {
                return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]> { ["bankAccountPublicId"] = ["Bank account does not exist in this personnel file."] }));
            }
        }

        var entity = PersonnelFilePaymentMethod.Create(
            command.Item.PaymentMethodCode,
            command.Item.BankAccountPublicId,
            command.Item.IsPrimary,
            command.Item.IsActive,
            command.Item.EffectiveFromUtc,
            command.Item.EffectiveToUtc,
            command.Item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddPaymentMethodAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added payment method for {personnelFile.FullName}.", response, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFilePaymentMethodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFilePaymentMethodCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>> Handle(
        UpdatePersonnelFilePaymentMethodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (command.Item.BankAccountPublicId.HasValue)
        {
            var bankAccountIds = (await personnelFileRepository.GetBankAccountIdsAsync(personnelFile.PublicId, cancellationToken)).ToHashSet();
            if (!bankAccountIds.Contains(command.Item.BankAccountPublicId.Value))
            {
                return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]> { ["bankAccountPublicId"] = ["Bank account does not exist in this personnel file."] }));
            }
        }

        var found = await employeeRepository.UpdatePaymentMethodAsync(
            command.ItemPublicId, personnelFile.TenantId,
            command.Item.PaymentMethodCode, command.Item.BankAccountPublicId,
            command.Item.IsPrimary, command.Item.IsActive,
            command.Item.EffectiveFromUtc, command.Item.EffectiveToUtc, command.Item.Notes,
            cancellationToken);
        if (!found) { return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Failure(PersonnelFileErrors.ItemNotFound); }

        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated payment method for {personnelFile.FullName}.", null, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        var allItems = await employeeRepository.GetPaymentMethodsAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class DeactivatePersonnelFilePaymentMethodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeactivatePersonnelFilePaymentMethodCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>> Handle(
        DeactivatePersonnelFilePaymentMethodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var found = await employeeRepository.DeactivatePaymentMethodAsync(command.ItemPublicId, personnelFile.TenantId, cancellationToken);
        if (!found) { return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Failure(PersonnelFileErrors.ItemNotFound); }

        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated payment method for {personnelFile.FullName}.", null, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        var allItems = await employeeRepository.GetPaymentMethodsAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class GetPersonnelFilePaymentMethodsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFilePaymentMethodsQuery, IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>> Handle(
        GetPersonnelFilePaymentMethodsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetPaymentMethodsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>.Success(response);
    }
}

internal sealed class AddPersonnelFileAuthorizationSubstitutionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileAuthorizationSubstitutionCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>> Handle(
        AddPersonnelFileAuthorizationSubstitutionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);

        if (command.Item.SubstitutePersonnelFileId == command.PersonnelFileId)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["substitutePersonnelFileId"] = ["Self-substitution is not allowed."]
                }));
        }

        var entity = PersonnelFileAuthorizationSubstitution.Create(
            command.Item.SubstitutionTypeCode,
            command.Item.SubstitutePersonnelFileId,
            command.Item.SubstitutePositionTitle,
            command.Item.StartDate,
            command.Item.EndDate,
            command.Item.IsActive,
            command.Item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddAuthorizationSubstitutionAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added authorization substitution to {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFileAuthorizationSubstitutionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileAuthorizationSubstitutionCommand, PersonnelFileSectionResult<PersonnelFileAuthorizationSubstitutionResponse>>
{
    public async Task<Result<PersonnelFileSectionResult<PersonnelFileAuthorizationSubstitutionResponse>>> Handle(
        UpdatePersonnelFileAuthorizationSubstitutionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<PersonnelFileAuthorizationSubstitutionResponse>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult<PersonnelFileAuthorizationSubstitutionResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);

        if (command.Item.SubstitutePersonnelFileId == command.PersonnelFileId)
        {
            return Result<PersonnelFileSectionResult<PersonnelFileAuthorizationSubstitutionResponse>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["substitutePersonnelFileId"] = ["Self-substitution is not allowed."]
                }));
        }

        var response = await employeeRepository.UpdateAuthorizationSubstitutionAsync(
            command.ItemPublicId,
            personnelFile.TenantId,
            command.Item.SubstitutionTypeCode,
            command.Item.SubstitutePersonnelFileId,
            command.Item.SubstitutePositionTitle,
            command.Item.StartDate,
            command.Item.EndDate,
            command.Item.IsActive,
            command.Item.Notes,
            cancellationToken);

        if (response is null)
            return Result<PersonnelFileSectionResult<PersonnelFileAuthorizationSubstitutionResponse>>.Failure(PersonnelFileErrors.NotFound);

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated authorization substitution for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<PersonnelFileAuthorizationSubstitutionResponse>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class DeactivatePersonnelFileAuthorizationSubstitutionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeactivatePersonnelFileAuthorizationSubstitutionCommand, PersonnelFileSectionResult>
{
    public async Task<Result<PersonnelFileSectionResult>> Handle(
        DeactivatePersonnelFileAuthorizationSubstitutionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult>.Failure(PersonnelFileErrors.StateRuleViolation);

        var deactivated = await employeeRepository.DeactivateAuthorizationSubstitutionAsync(command.ItemPublicId, personnelFile.TenantId, cancellationToken);
        if (!deactivated)
            return Result<PersonnelFileSectionResult>.Failure(PersonnelFileErrors.NotFound);

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated authorization substitution for {personnelFile.FullName}.", null, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult>.Success(CreateSectionResult(personnelFile));
    }
}

internal sealed class GetPersonnelFileAuthorizationSubstitutionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAuthorizationSubstitutionsQuery, IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>> Handle(
        GetPersonnelFileAuthorizationSubstitutionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetAuthorizationSubstitutionsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>.Success(response);
    }
}

internal sealed class AddPersonnelFilePersonnelActionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFilePersonnelActionCommand, PersonnelFilePersonnelActionResponse>
{
    public async Task<Result<PersonnelFilePersonnelActionResponse>> Handle(
        AddPersonnelFilePersonnelActionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFilePersonnelActionResponse>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFilePersonnelActionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFilePersonnelAction.Create(
            command.ActionTypeCode,
            command.ActionStatusCode,
            command.ActionDateUtc,
            command.EffectiveFromUtc,
            command.EffectiveToUtc,
            command.Description,
            command.Reference,
            command.Amount,
            command.CurrencyCode,
            isSystemGenerated: false);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddPersonnelActionAsync(entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added personnel action for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFilePersonnelActionResponse>.Success(response);
    }
}

internal sealed class SearchPersonnelFilePersonnelActionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<SearchPersonnelFilePersonnelActionsQuery, PagedResponse<PersonnelFilePersonnelActionResponse>>
{
    public async Task<Result<PagedResponse<PersonnelFilePersonnelActionResponse>>> Handle(
        SearchPersonnelFilePersonnelActionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<PagedResponse<PersonnelFilePersonnelActionResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PagedResponse<PersonnelFilePersonnelActionResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.SearchPersonnelActionsAsync(
            personnelFile!.PublicId,
            query.FromUtc,
            query.ToUtc,
            query.Type,
            query.Status,
            query.Search,
            query.SortBy,
            query.SortDirection,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        return Result<PagedResponse<PersonnelFilePersonnelActionResponse>>.Success(response);
    }
}

internal sealed class ExportPersonnelFilePersonnelActionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<ExportPersonnelFilePersonnelActionsQuery, IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>> Handle(
        ExportPersonnelFilePersonnelActionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.ExportPersonnelActionsAsync(
            personnelFile!.PublicId,
            query.FromUtc,
            query.ToUtc,
            query.Type,
            query.Status,
            query.Search,
            query.SortBy,
            query.SortDirection,
            query.MaxRows,
            cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>.Success(response);
    }
}

internal sealed class AddPersonnelFilePayrollTransactionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFilePayrollTransactionCommand, PersonnelFilePayrollTransactionResponse>
{
    public async Task<Result<PersonnelFilePayrollTransactionResponse>> Handle(
        AddPersonnelFilePayrollTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFilePayrollTransactionResponse>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFilePayrollTransactionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFilePayrollTransaction.Create(
            command.Item.TransactionTypeCode,
            command.Item.TransactionDateUtc,
            command.Item.PayrollPeriodCode,
            command.Item.Description,
            command.Item.Amount,
            command.Item.CurrencyCode,
            command.Item.IsDebit,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddPayrollTransactionAsync(entity, cancellationToken);
        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added payroll transaction for {personnelFile.FullName}.", response, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        return Result<PersonnelFilePayrollTransactionResponse>.Success(response);
    }
}

internal sealed class DeactivatePersonnelFilePayrollTransactionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeactivatePersonnelFilePayrollTransactionCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>> Handle(
        DeactivatePersonnelFilePayrollTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var found = await employeeRepository.DeactivatePayrollTransactionAsync(command.ItemPublicId, personnelFile.TenantId, cancellationToken);
        if (!found) { return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>.Failure(PersonnelFileErrors.ItemNotFound); }

        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated payroll transaction for {personnelFile.FullName}.", null, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        // Return current search results for consistency — caller should use Search endpoint for full listing
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>.Success(
            new PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>(
                Array.Empty<PersonnelFilePayrollTransactionResponse>(), personnelFile.ConcurrencyToken, personnelFile.ModifiedUtc));
    }
}

internal sealed class SearchPersonnelFilePayrollTransactionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<SearchPersonnelFilePayrollTransactionsQuery, PagedResponse<PersonnelFilePayrollTransactionResponse>>
{
    public async Task<Result<PagedResponse<PersonnelFilePayrollTransactionResponse>>> Handle(
        SearchPersonnelFilePayrollTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<PagedResponse<PersonnelFilePayrollTransactionResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PagedResponse<PersonnelFilePayrollTransactionResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.SearchPayrollTransactionsAsync(
            personnelFile!.PublicId,
            query.FromUtc,
            query.ToUtc,
            query.Type,
            query.Status,
            query.Search,
            query.SortBy,
            query.SortDirection,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        return Result<PagedResponse<PersonnelFilePayrollTransactionResponse>>.Success(response);
    }
}

internal sealed class ExportPersonnelFilePayrollTransactionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<ExportPersonnelFilePayrollTransactionsQuery, IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>> Handle(
        ExportPersonnelFilePayrollTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.ExportPayrollTransactionsAsync(
            personnelFile!.PublicId,
            query.FromUtc,
            query.ToUtc,
            query.Type,
            query.Status,
            query.Search,
            query.SortBy,
            query.SortDirection,
            query.MaxRows,
            cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>.Success(response);
    }
}

internal sealed class AddPersonnelFileAssetAccessCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileAssetAccessCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>> Handle(
        AddPersonnelFileAssetAccessCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);

        var entity = PersonnelFileAssetAccess.Create(
            command.Item.AssetTypeCode,
            command.Item.AssetOrAccessName,
            command.Item.AccessLevelCode,
            command.Item.StartDateUtc,
            command.Item.EndDateUtc,
            command.Item.DeliveryDateUtc,
            command.Item.DeliveryStatusCode,
            command.Item.IsActive,
            command.Item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddAssetAccessAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added asset/access to {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFileAssetAccessCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileAssetAccessCommand, PersonnelFileSectionResult<PersonnelFileAssetAccessResponse>>
{
    public async Task<Result<PersonnelFileSectionResult<PersonnelFileAssetAccessResponse>>> Handle(
        UpdatePersonnelFileAssetAccessCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<PersonnelFileAssetAccessResponse>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult<PersonnelFileAssetAccessResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);

        var response = await employeeRepository.UpdateAssetAccessAsync(
            command.ItemPublicId,
            personnelFile.TenantId,
            command.Item.AssetTypeCode,
            command.Item.AssetOrAccessName,
            command.Item.AccessLevelCode,
            command.Item.StartDateUtc,
            command.Item.EndDateUtc,
            command.Item.DeliveryDateUtc,
            command.Item.DeliveryStatusCode,
            command.Item.IsActive,
            command.Item.Notes,
            cancellationToken);

        if (response is null)
            return Result<PersonnelFileSectionResult<PersonnelFileAssetAccessResponse>>.Failure(PersonnelFileErrors.NotFound);

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated asset/access for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<PersonnelFileAssetAccessResponse>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class DeactivatePersonnelFileAssetAccessCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeactivatePersonnelFileAssetAccessCommand, PersonnelFileSectionResult>
{
    public async Task<Result<PersonnelFileSectionResult>> Handle(
        DeactivatePersonnelFileAssetAccessCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null) return failure;

        if (!personnelFile!.IsCompletedEmployee)
            return Result<PersonnelFileSectionResult>.Failure(PersonnelFileErrors.StateRuleViolation);

        var deactivated = await employeeRepository.DeactivateAssetAccessAsync(command.ItemPublicId, personnelFile.TenantId, cancellationToken);
        if (!deactivated)
            return Result<PersonnelFileSectionResult>.Failure(PersonnelFileErrors.NotFound);

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated asset/access for {personnelFile.FullName}.", null, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult>.Success(CreateSectionResult(personnelFile));
    }
}

internal sealed class GetPersonnelFileAssetsAccessesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAssetsAccessesQuery, IReadOnlyCollection<PersonnelFileAssetAccessResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>> Handle(
        GetPersonnelFileAssetsAccessesQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetAssetsAccessesAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>.Success(response);
    }
}

internal sealed class AddPersonnelFileInsuranceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileInsuranceCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>> Handle(
        AddPersonnelFileInsuranceCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>(
            command.PersonnelFileId, command.ConcurrencyToken, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var beneficiaries = command.Item.Beneficiaries.ToArray();
        for (var i = 0; i < beneficiaries.Length; i++)
        {
            var kinshipValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
                personnelFileRepository, personnelFile.TenantId,
                $"item.beneficiaries[{i}].kinshipCode", beneficiaries[i].KinshipCode, cancellationToken);
            if (kinshipValidation != Error.None)
            {
                return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Failure(kinshipValidation);
            }
        }

        var insurance = PersonnelFileInsurance.Create(
            command.Item.InsuranceCode, command.Item.EmployeeContribution, command.Item.EmployerContribution,
            command.Item.RangeCode, command.Item.PolicyNumber, command.Item.InsuredAmount,
            command.Item.CurrencyCode, command.Item.IsActive, command.Item.StartDateUtc, command.Item.EndDateUtc);
        insurance.BindToPersonnelFile(personnelFile.Id);
        insurance.SetTenantId(personnelFile.TenantId);
        insurance.ReplaceBeneficiaries(beneficiaries.Select(b =>
        {
            var e = PersonnelFileInsuranceBeneficiary.Create(b.FullName, b.DocumentNumber, b.BirthDate, b.KinshipCode);
            e.SetTenantId(personnelFile.TenantId);
            return e;
        }).ToArray());

        var response = await employeeRepository.AddInsuranceAsync(personnelFile.Id, personnelFile.TenantId, insurance, cancellationToken);
        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added insurance for {personnelFile.FullName}.", response, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFileInsuranceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileInsuranceCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>> Handle(
        UpdatePersonnelFileInsuranceCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>(
            command.PersonnelFileId, command.ConcurrencyToken, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var beneficiaries = command.Item.Beneficiaries.ToArray();
        for (var i = 0; i < beneficiaries.Length; i++)
        {
            var kinshipValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
                personnelFileRepository, personnelFile.TenantId,
                $"item.beneficiaries[{i}].kinshipCode", beneficiaries[i].KinshipCode, cancellationToken);
            if (kinshipValidation != Error.None)
            {
                return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Failure(kinshipValidation);
            }
        }

        var found = await employeeRepository.UpdateInsuranceAsync(
            command.ItemPublicId, personnelFile.TenantId,
            command.Item.InsuranceCode, command.Item.EmployeeContribution, command.Item.EmployerContribution,
            command.Item.RangeCode, command.Item.PolicyNumber, command.Item.InsuredAmount, command.Item.CurrencyCode,
            command.Item.IsActive, command.Item.StartDateUtc, command.Item.EndDateUtc,
            command.Item.Beneficiaries, cancellationToken);
        if (!found) { return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Failure(PersonnelFileErrors.ItemNotFound); }

        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated insurance for {personnelFile.FullName}.", null, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        var allItems = await employeeRepository.GetInsurancesAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class DeactivatePersonnelFileInsuranceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeactivatePersonnelFileInsuranceCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>> Handle(
        DeactivatePersonnelFileInsuranceCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>(
            command.PersonnelFileId, command.ConcurrencyToken, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var found = await employeeRepository.DeactivateInsuranceAsync(command.ItemPublicId, personnelFile.TenantId, cancellationToken);
        if (!found) { return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Failure(PersonnelFileErrors.ItemNotFound); }

        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated insurance for {personnelFile.FullName}.", null, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        var allItems = await employeeRepository.GetInsurancesAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class GetPersonnelFileInsurancesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileInsurancesQuery, IReadOnlyCollection<PersonnelFileInsuranceResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileInsuranceResponse>>> Handle(
        GetPersonnelFileInsurancesQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFileInsuranceResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetInsurancesAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileInsuranceResponse>>.Success(response);
    }
}

internal sealed class AddPersonnelFileMedicalClaimCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileMedicalClaimCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>> Handle(
        AddPersonnelFileMedicalClaimCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>(
            command.PersonnelFileId, command.ConcurrencyToken, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFileMedicalClaim.Create(
            command.Item.InsurancePublicId, command.Item.AccountNumber, command.Item.ClaimTypeCode,
            command.Item.Diagnosis, command.Item.ClaimAmount, command.Item.CurrencyCode,
            command.Item.PaidAmount, command.Item.ResponseTimeDays, command.Item.Notes,
            command.Item.ClaimDateUtc, command.Item.SourceSystem, command.Item.SourceReference, command.Item.SourceSyncedUtc);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddMedicalClaimAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added medical claim for {personnelFile.FullName}.", response, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFileMedicalClaimCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileMedicalClaimCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>> Handle(
        UpdatePersonnelFileMedicalClaimCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>(
            command.PersonnelFileId, command.ConcurrencyToken, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var found = await employeeRepository.UpdateMedicalClaimAsync(
            command.ItemPublicId, personnelFile.TenantId,
            command.Item.InsurancePublicId, command.Item.AccountNumber, command.Item.ClaimTypeCode,
            command.Item.Diagnosis, command.Item.ClaimAmount, command.Item.CurrencyCode,
            command.Item.PaidAmount, command.Item.ResponseTimeDays, command.Item.Notes,
            command.Item.ClaimDateUtc, command.Item.SourceSystem, command.Item.SourceReference, command.Item.SourceSyncedUtc,
            cancellationToken);
        if (!found) { return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>.Failure(PersonnelFileErrors.ItemNotFound); }

        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated medical claim for {personnelFile.FullName}.", null, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        var allItems = await employeeRepository.GetMedicalClaimsAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class DeactivatePersonnelFileMedicalClaimCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeactivatePersonnelFileMedicalClaimCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>> Handle(
        DeactivatePersonnelFileMedicalClaimCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>(
            command.PersonnelFileId, command.ConcurrencyToken, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null) { return failure; }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var found = await employeeRepository.DeactivateMedicalClaimAsync(command.ItemPublicId, personnelFile.TenantId, cancellationToken);
        if (!found) { return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>.Failure(PersonnelFileErrors.ItemNotFound); }

        TouchPersonnelFile(personnelFile);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try { _ = await unitOfWork.SaveChangesAsync(cancellationToken); await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated medical claim for {personnelFile.FullName}.", null, cancellationToken); _ = await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }

        var allItems = await employeeRepository.GetMedicalClaimsAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class GetPersonnelFileMedicalClaimsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileMedicalClaimsQuery, IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>> Handle(
        GetPersonnelFileMedicalClaimsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetMedicalClaimsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>.Success(response);
    }
}

internal sealed class AddPersonnelFilePerformanceEvaluationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFilePerformanceEvaluationCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>> Handle(
        AddPersonnelFilePerformanceEvaluationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFilePerformanceEvaluation.Create(
            command.Item.EvaluatorName,
            command.Item.EvaluationDateUtc,
            command.Item.Score,
            command.Item.QualitativeScoreCode,
            command.Item.Comment,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddPerformanceEvaluationAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added performance evaluation for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFilePerformanceEvaluationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFilePerformanceEvaluationCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>> Handle(
        UpdatePersonnelFilePerformanceEvaluationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.UpdatePerformanceEvaluationAsync(
            command.ItemPublicId,
            personnelFile.TenantId,
            command.Item.EvaluatorName,
            command.Item.EvaluationDateUtc,
            command.Item.Score,
            command.Item.QualitativeScoreCode,
            command.Item.Comment,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated performance evaluation for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var allItems = await employeeRepository.GetPerformanceEvaluationsAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class GetPersonnelFilePerformanceEvaluationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFilePerformanceEvaluationsQuery, IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>> Handle(
        GetPersonnelFilePerformanceEvaluationsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.GetPerformanceEvaluationsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>.Success(response);
    }
}

internal sealed class AddPersonnelFilePositionCompetencyResultCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFilePositionCompetencyResultCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>> Handle(
        AddPersonnelFilePositionCompetencyResultCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFilePositionCompetencyResult.Create(
            command.Item.CompetencyCode,
            command.Item.DesiredBehaviors,
            command.Item.ExpectedScore,
            command.Item.AchievedScore,
            command.Item.GapScore,
            command.Item.EvaluationDateUtc,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddPositionCompetencyResultAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added position competency result for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFilePositionCompetencyResultCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFilePositionCompetencyResultCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>> Handle(
        UpdatePersonnelFilePositionCompetencyResultCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.UpdatePositionCompetencyResultAsync(
            command.ItemPublicId,
            personnelFile.TenantId,
            command.Item.CompetencyCode,
            command.Item.DesiredBehaviors,
            command.Item.ExpectedScore,
            command.Item.AchievedScore,
            command.Item.GapScore,
            command.Item.EvaluationDateUtc,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated position competency result for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var allItems = await employeeRepository.GetPositionCompetencyResultsAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class GetPersonnelFilePositionCompetencyResultsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFilePositionCompetencyResultsQuery, IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>> Handle(
        GetPersonnelFilePositionCompetencyResultsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.GetPositionCompetencyResultsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>.Success(response);
    }
}

internal sealed class AddPersonnelFileSelectionContestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileSelectionContestCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>> Handle(
        AddPersonnelFileSelectionContestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFileSelectionContest.Create(
            command.Item.ContestCode,
            command.Item.ContestName,
            command.Item.ContestDateUtc,
            command.Item.ResultCode,
            command.Item.Notes,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddSelectionContestAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added selection contest for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFileSelectionContestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileSelectionContestCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>> Handle(
        UpdatePersonnelFileSelectionContestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.UpdateSelectionContestAsync(
            command.ItemPublicId,
            personnelFile.TenantId,
            command.Item.ContestCode,
            command.Item.ContestName,
            command.Item.ContestDateUtc,
            command.Item.ResultCode,
            command.Item.Notes,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated selection contest for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var allItems = await employeeRepository.GetSelectionContestsAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class GetPersonnelFileSelectionContestsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFileSelectionContestsQuery, IReadOnlyCollection<PersonnelFileSelectionContestResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>> Handle(
        GetPersonnelFileSelectionContestsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.GetSelectionContestsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>.Success(response);
    }
}

internal sealed class AddPersonnelFileCurricularCompetencyCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileCurricularCompetencyCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>> Handle(
        AddPersonnelFileCurricularCompetencyCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = PersonnelFileCurricularCompetency.Create(
            command.Item.RequirementTypeCode,
            command.Item.RequirementName,
            command.Item.CompetencyDomain,
            command.Item.ExperienceTimeValue,
            command.Item.MetricCode,
            command.Item.Notes,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.AddCurricularCompetencyAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added curricular competency for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>.Success(CreateSectionResult(personnelFile, response));
    }
}

internal sealed class UpdatePersonnelFileCurricularCompetencyCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileCurricularCompetencyCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>> Handle(
        UpdatePersonnelFileCurricularCompetencyCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>(
            command.PersonnelFileId,
            command.ConcurrencyToken,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.UpdateCurricularCompetencyAsync(
            command.ItemPublicId,
            personnelFile.TenantId,
            command.Item.RequirementTypeCode,
            command.Item.RequirementName,
            command.Item.CompetencyDomain,
            command.Item.ExperienceTimeValue,
            command.Item.MetricCode,
            command.Item.Notes,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated curricular competency for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var allItems = await employeeRepository.GetCurricularCompetenciesAsync(personnelFile.PublicId, cancellationToken);
        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>.Success(CreateSectionResult(personnelFile, allItems));
    }
}

internal sealed class GetPersonnelFileCurricularCompetenciesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFileCurricularCompetenciesQuery, IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>> Handle(
        GetPersonnelFileCurricularCompetenciesQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.GetCurricularCompetenciesAsync(personnelFile.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>.Success(response);
    }
}
