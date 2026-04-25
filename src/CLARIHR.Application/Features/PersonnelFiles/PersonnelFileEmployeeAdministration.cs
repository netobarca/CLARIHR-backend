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

public sealed record ReplacePersonnelFileEmploymentAssignmentsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<EmploymentAssignmentInput> Items,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>;

public sealed record GetPersonnelFileEmploymentAssignmentsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>;

public sealed record ContractHistoryInput(
    string ContractTypeCode,
    DateTime ContractDate,
    DateTime? ContractEndDate,
    Guid? PositionSlotId,
    string? Notes);

public sealed record ReplacePersonnelFileContractHistoryCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<ContractHistoryInput> Items,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>;

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

public sealed record ReplacePersonnelFileSalaryItemsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<SalaryItemInput> Items,
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

public sealed record ReplacePersonnelFileAdditionalBenefitsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<AdditionalBenefitInput> Items,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>;

public sealed record GetPersonnelFileAdditionalBenefitsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>;

public sealed record PaymentMethodInput(
    string PaymentMethodCode,
    Guid? BankAccountId,
    bool IsPrimary,
    bool IsActive,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes);

public sealed record ReplacePersonnelFilePaymentMethodsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<PaymentMethodInput> Items,
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

public sealed record ReplacePersonnelFileAuthorizationSubstitutionsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<AuthorizationSubstitutionInput> Items,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>;

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

public sealed record ReplacePersonnelFilePayrollTransactionsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<PayrollTransactionInput> Items,
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

public sealed record ReplacePersonnelFileAssetsAccessesCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<AssetAccessInput> Items,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>;

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

public sealed record ReplacePersonnelFileInsurancesCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<InsuranceInput> Items,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>;

public sealed record GetPersonnelFileInsurancesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileInsuranceResponse>>;

public sealed record MedicalClaimInput(
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

public sealed record ReplacePersonnelFileMedicalClaimsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<MedicalClaimInput> Items,
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

public sealed record ReplacePersonnelFilePerformanceEvaluationsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<PerformanceEvaluationInput> Items,
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

public sealed record ReplacePersonnelFilePositionCompetencyResultsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<PositionCompetencyResultInput> Items,
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

public sealed record ReplacePersonnelFileSelectionContestsCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<SelectionContestInput> Items,
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

public sealed record ReplacePersonnelFileCurricularCompetenciesCommand(
    Guid PersonnelFileId,
    IReadOnlyCollection<CurricularCompetencyInput> Items,
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

internal sealed class ReplacePersonnelFileEmploymentAssignmentsCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFileEmploymentAssignmentsCommand, EmploymentAssignmentInput>
{
    public ReplacePersonnelFileEmploymentAssignmentsCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFileEmploymentAssignmentsCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFileEmploymentAssignmentsCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFileEmploymentAssignmentsCommand, EmploymentAssignmentInput>(command => command.Items),
            new EmploymentAssignmentInputValidator());
}

internal sealed class GetPersonnelFileEmploymentAssignmentsQueryValidator : AbstractValidator<GetPersonnelFileEmploymentAssignmentsQuery>
{
    public GetPersonnelFileEmploymentAssignmentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class ReplacePersonnelFileContractHistoryCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFileContractHistoryCommand, ContractHistoryInput>
{
    public ReplacePersonnelFileContractHistoryCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFileContractHistoryCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFileContractHistoryCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFileContractHistoryCommand, ContractHistoryInput>(command => command.Items),
            new ContractHistoryInputValidator());
}

internal sealed class GetPersonnelFileContractHistoryQueryValidator : AbstractValidator<GetPersonnelFileContractHistoryQuery>
{
    public GetPersonnelFileContractHistoryQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class ReplacePersonnelFileSalaryItemsCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFileSalaryItemsCommand, SalaryItemInput>
{
    public ReplacePersonnelFileSalaryItemsCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFileSalaryItemsCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFileSalaryItemsCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFileSalaryItemsCommand, SalaryItemInput>(command => command.Items),
            new SalaryItemInputValidator());
}

internal sealed class GetPersonnelFileSalaryItemsQueryValidator : AbstractValidator<GetPersonnelFileSalaryItemsQuery>
{
    public GetPersonnelFileSalaryItemsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class ReplacePersonnelFileAdditionalBenefitsCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFileAdditionalBenefitsCommand, AdditionalBenefitInput>
{
    public ReplacePersonnelFileAdditionalBenefitsCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFileAdditionalBenefitsCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFileAdditionalBenefitsCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFileAdditionalBenefitsCommand, AdditionalBenefitInput>(command => command.Items),
            new AdditionalBenefitInputValidator());
}

internal sealed class GetPersonnelFileAdditionalBenefitsQueryValidator : AbstractValidator<GetPersonnelFileAdditionalBenefitsQuery>
{
    public GetPersonnelFileAdditionalBenefitsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class ReplacePersonnelFilePaymentMethodsCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFilePaymentMethodsCommand, PaymentMethodInput>
{
    public ReplacePersonnelFilePaymentMethodsCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFilePaymentMethodsCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFilePaymentMethodsCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFilePaymentMethodsCommand, PaymentMethodInput>(command => command.Items),
            new PaymentMethodInputValidator());
}

internal sealed class GetPersonnelFilePaymentMethodsQueryValidator : AbstractValidator<GetPersonnelFilePaymentMethodsQuery>
{
    public GetPersonnelFilePaymentMethodsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class ReplacePersonnelFileAuthorizationSubstitutionsCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFileAuthorizationSubstitutionsCommand, AuthorizationSubstitutionInput>
{
    public ReplacePersonnelFileAuthorizationSubstitutionsCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFileAuthorizationSubstitutionsCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFileAuthorizationSubstitutionsCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFileAuthorizationSubstitutionsCommand, AuthorizationSubstitutionInput>(command => command.Items),
            new AuthorizationSubstitutionInputValidator());
}

internal sealed class GetPersonnelFileAuthorizationSubstitutionsQueryValidator : AbstractValidator<GetPersonnelFileAuthorizationSubstitutionsQuery>
{
    public GetPersonnelFileAuthorizationSubstitutionsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class ReplacePersonnelFilePayrollTransactionsCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFilePayrollTransactionsCommand, PayrollTransactionInput>
{
    public ReplacePersonnelFilePayrollTransactionsCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFilePayrollTransactionsCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFilePayrollTransactionsCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFilePayrollTransactionsCommand, PayrollTransactionInput>(command => command.Items),
            new PayrollTransactionInputValidator());
}

internal sealed class ReplacePersonnelFileAssetsAccessesCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFileAssetsAccessesCommand, AssetAccessInput>
{
    public ReplacePersonnelFileAssetsAccessesCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFileAssetsAccessesCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFileAssetsAccessesCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFileAssetsAccessesCommand, AssetAccessInput>(command => command.Items),
            new AssetAccessInputValidator());
}

internal sealed class GetPersonnelFileAssetsAccessesQueryValidator : AbstractValidator<GetPersonnelFileAssetsAccessesQuery>
{
    public GetPersonnelFileAssetsAccessesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class ReplacePersonnelFileInsurancesCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFileInsurancesCommand, InsuranceInput>
{
    public ReplacePersonnelFileInsurancesCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFileInsurancesCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFileInsurancesCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFileInsurancesCommand, InsuranceInput>(command => command.Items),
            new InsuranceInputValidator());
}

internal sealed class GetPersonnelFileInsurancesQueryValidator : AbstractValidator<GetPersonnelFileInsurancesQuery>
{
    public GetPersonnelFileInsurancesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class ReplacePersonnelFileMedicalClaimsCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFileMedicalClaimsCommand, MedicalClaimInput>
{
    public ReplacePersonnelFileMedicalClaimsCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFileMedicalClaimsCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFileMedicalClaimsCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFileMedicalClaimsCommand, MedicalClaimInput>(command => command.Items),
            new MedicalClaimInputValidator());
}

internal sealed class GetPersonnelFileMedicalClaimsQueryValidator : AbstractValidator<GetPersonnelFileMedicalClaimsQuery>
{
    public GetPersonnelFileMedicalClaimsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class ReplacePersonnelFilePerformanceEvaluationsCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFilePerformanceEvaluationsCommand, PerformanceEvaluationInput>
{
    public ReplacePersonnelFilePerformanceEvaluationsCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFilePerformanceEvaluationsCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFilePerformanceEvaluationsCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFilePerformanceEvaluationsCommand, PerformanceEvaluationInput>(command => command.Items),
            new PerformanceEvaluationInputValidator());
}

internal sealed class ReplacePersonnelFilePositionCompetencyResultsCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFilePositionCompetencyResultsCommand, PositionCompetencyResultInput>
{
    public ReplacePersonnelFilePositionCompetencyResultsCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFilePositionCompetencyResultsCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFilePositionCompetencyResultsCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFilePositionCompetencyResultsCommand, PositionCompetencyResultInput>(command => command.Items),
            new PositionCompetencyResultInputValidator());
}

internal sealed class ReplacePersonnelFileSelectionContestsCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFileSelectionContestsCommand, SelectionContestInput>
{
    public ReplacePersonnelFileSelectionContestsCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFileSelectionContestsCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFileSelectionContestsCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFileSelectionContestsCommand, SelectionContestInput>(command => command.Items),
            new SelectionContestInputValidator());
}

internal sealed class ReplacePersonnelFileCurricularCompetenciesCommandValidator
    : PersonnelFileEmployeeCommandValidatorBase<ReplacePersonnelFileCurricularCompetenciesCommand, CurricularCompetencyInput>
{
    public ReplacePersonnelFileCurricularCompetenciesCommandValidator() =>
        Configure(
            new GuidAccessor<ReplacePersonnelFileCurricularCompetenciesCommand>(command => command.PersonnelFileId),
            new GuidAccessor<ReplacePersonnelFileCurricularCompetenciesCommand>(command => command.ConcurrencyToken),
            new CollectionAccessor<ReplacePersonnelFileCurricularCompetenciesCommand, CurricularCompetencyInput>(command => command.Items),
            new CurricularCompetencyInputValidator());
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
            personnelFile.AssignedPositionSlotPublicId,
            personnelFile.CustomDataJson);
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

internal sealed class ReplacePersonnelFileEmploymentAssignmentsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileEmploymentAssignmentsCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>> Handle(
        ReplacePersonnelFileEmploymentAssignmentsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>(
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
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFileEmploymentAssignment.Create(
                item.AssignmentTypeCode,
                item.PositionSlotId,
                item.OrgUnitId,
                item.WorkCenterId,
                item.CostCenterId,
                item.StartDate,
                item.EndDate,
                item.IsPrimary,
                item.IsActive,
                item.Notes);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplaceEmploymentAssignmentsAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated employment assignments for {personnelFile.FullName}.", response, cancellationToken);
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

internal sealed class ReplacePersonnelFileContractHistoryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileContractHistoryCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>> Handle(
        ReplacePersonnelFileContractHistoryCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>(
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
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFileContractHistory.Create(
                item.ContractTypeCode,
                item.ContractDate,
                item.ContractEndDate,
                item.PositionSlotId,
                item.Notes);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplaceContractHistoryAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
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

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>.Success(CreateSectionResult(personnelFile, response));
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

internal sealed class ReplacePersonnelFileSalaryItemsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileSalaryItemsCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>> Handle(
        ReplacePersonnelFileSalaryItemsCommand command,
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

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFileSalaryItem.Create(
                item.IncomeTypeCode,
                item.SalaryRubricCode,
                item.CurrencyCode,
                item.PayPeriodCode,
                item.Amount,
                item.StartDate,
                item.EndDate,
                item.IsActive);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplaceSalaryItemsAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated salary items for {personnelFile.FullName}.", response, cancellationToken);
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

internal sealed class ReplacePersonnelFileAdditionalBenefitsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileAdditionalBenefitsCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>> Handle(
        ReplacePersonnelFileAdditionalBenefitsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>(
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
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFileAdditionalBenefit.Create(item.BenefitTypeCode, item.StartDate, item.EndDate, item.IsActive, item.Notes);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplaceAdditionalBenefitsAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated additional benefits for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>.Success(CreateSectionResult(personnelFile, response));
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

internal sealed class ReplacePersonnelFilePaymentMethodsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFilePaymentMethodsCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>> Handle(
        ReplacePersonnelFilePaymentMethodsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>(
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
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var bankAccountIds = (await personnelFileRepository.GetBankAccountIdsAsync(personnelFile.PublicId, cancellationToken)).ToHashSet();
        if (command.Items.Any(item => item.BankAccountId.HasValue && !bankAccountIds.Contains(item.BankAccountId.Value)))
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["bankAccountId"] = ["Bank account does not exist in this personnel file."]
                }));
        }

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFilePaymentMethod.Create(
                item.PaymentMethodCode,
                item.BankAccountId,
                item.IsPrimary,
                item.IsActive,
                item.EffectiveFromUtc,
                item.EffectiveToUtc,
                item.Notes);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplacePaymentMethodsAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated payment methods for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>.Success(CreateSectionResult(personnelFile, response));
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

internal sealed class ReplacePersonnelFileAuthorizationSubstitutionsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileAuthorizationSubstitutionsCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>> Handle(
        ReplacePersonnelFileAuthorizationSubstitutionsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>(
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
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (command.Items.Any(item => item.SubstitutePersonnelFileId == command.PersonnelFileId))
        {
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["substitutePersonnelFileId"] = ["Self-substitution is not allowed."]
                }));
        }

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFileAuthorizationSubstitution.Create(
                item.SubstitutionTypeCode,
                item.SubstitutePersonnelFileId,
                item.SubstitutePositionTitle,
                item.StartDate,
                item.EndDate,
                item.IsActive,
                item.Notes);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplaceAuthorizationSubstitutionsAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated authorization substitutions for {personnelFile.FullName}.", response, cancellationToken);
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

internal sealed class ReplacePersonnelFilePayrollTransactionsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFilePayrollTransactionsCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>> Handle(
        ReplacePersonnelFilePayrollTransactionsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>(
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
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFilePayrollTransaction.Create(
                item.TransactionTypeCode,
                item.TransactionDateUtc,
                item.PayrollPeriodCode,
                item.Description,
                item.Amount,
                item.CurrencyCode,
                item.IsDebit,
                item.SourceSystem,
                item.SourceReference,
                item.SourceSyncedUtc);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplacePayrollTransactionsAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated payroll transactions for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>.Success(CreateSectionResult(personnelFile, response));
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

internal sealed class ReplacePersonnelFileAssetsAccessesCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileAssetsAccessesCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>> Handle(
        ReplacePersonnelFileAssetsAccessesCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>(
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
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFileAssetAccess.Create(
                item.AssetTypeCode,
                item.AssetOrAccessName,
                item.AccessLevelCode,
                item.StartDateUtc,
                item.EndDateUtc,
                item.DeliveryDateUtc,
                item.DeliveryStatusCode,
                item.IsActive,
                item.Notes);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplaceAssetsAccessesAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated assets and accesses for {personnelFile.FullName}.", response, cancellationToken);
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

internal sealed class ReplacePersonnelFileInsurancesCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileInsurancesCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>> Handle(
        ReplacePersonnelFileInsurancesCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>(
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
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var insuranceInputs = command.Items.ToArray();
        for (var insuranceIndex = 0; insuranceIndex < insuranceInputs.Length; insuranceIndex++)
        {
            var beneficiaries = insuranceInputs[insuranceIndex].Beneficiaries.ToArray();
            for (var beneficiaryIndex = 0; beneficiaryIndex < beneficiaries.Length; beneficiaryIndex++)
            {
                var kinshipCodeValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
                    personnelFileRepository,
                    personnelFile.TenantId,
                    $"items[{insuranceIndex}].beneficiaries[{beneficiaryIndex}].kinshipCode",
                    beneficiaries[beneficiaryIndex].KinshipCode,
                    cancellationToken);
                if (kinshipCodeValidation != Error.None)
                {
                    return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Failure(kinshipCodeValidation);
                }
            }
        }

        var entities = insuranceInputs.Select(item =>
        {
            var insurance = PersonnelFileInsurance.Create(
                item.InsuranceCode,
                item.EmployeeContribution,
                item.EmployerContribution,
                item.RangeCode,
                item.PolicyNumber,
                item.InsuredAmount,
                item.CurrencyCode,
                item.IsActive,
                item.StartDateUtc,
                item.EndDateUtc);
            insurance.BindToPersonnelFile(personnelFile.Id);
            insurance.SetTenantId(personnelFile.TenantId);
            insurance.ReplaceBeneficiaries(item.Beneficiaries.Select(beneficiary =>
            {
                var entity = PersonnelFileInsuranceBeneficiary.Create(
                    beneficiary.FullName,
                    beneficiary.DocumentNumber,
                    beneficiary.BirthDate,
                    beneficiary.KinshipCode);
                entity.SetTenantId(personnelFile.TenantId);
                return entity;
            }).ToArray());
            return insurance;
        }).ToArray();

        var response = await employeeRepository.ReplaceInsurancesAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated insurances for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>.Success(CreateSectionResult(personnelFile, response));
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

internal sealed class ReplacePersonnelFileMedicalClaimsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileMedicalClaimsCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>> Handle(
        ReplacePersonnelFileMedicalClaimsCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>(
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
            return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFileMedicalClaim.Create(
                item.InsuranceId,
                item.AccountNumber,
                item.ClaimTypeCode,
                item.Diagnosis,
                item.ClaimAmount,
                item.CurrencyCode,
                item.PaidAmount,
                item.ResponseTimeDays,
                item.Notes,
                item.ClaimDateUtc,
                item.SourceSystem,
                item.SourceReference,
                item.SourceSyncedUtc);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplaceMedicalClaimsAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated medical claims for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>.Success(CreateSectionResult(personnelFile, response));
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

internal sealed class ReplacePersonnelFilePerformanceEvaluationsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFilePerformanceEvaluationsCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>> Handle(
        ReplacePersonnelFilePerformanceEvaluationsCommand command,
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

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFilePerformanceEvaluation.Create(
                item.EvaluatorName,
                item.EvaluationDateUtc,
                item.Score,
                item.QualitativeScoreCode,
                item.Comment,
                item.SourceSystem,
                item.SourceReference,
                item.SourceSyncedUtc);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplacePerformanceEvaluationsAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated performance evaluations for {personnelFile.FullName}.", response, cancellationToken);
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

internal sealed class ReplacePersonnelFilePositionCompetencyResultsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFilePositionCompetencyResultsCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>> Handle(
        ReplacePersonnelFilePositionCompetencyResultsCommand command,
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

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFilePositionCompetencyResult.Create(
                item.CompetencyCode,
                item.DesiredBehaviors,
                item.ExpectedScore,
                item.AchievedScore,
                item.GapScore,
                item.EvaluationDateUtc,
                item.SourceSystem,
                item.SourceReference,
                item.SourceSyncedUtc);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplacePositionCompetencyResultsAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated position competency results for {personnelFile.FullName}.", response, cancellationToken);
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

internal sealed class ReplacePersonnelFileSelectionContestsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileSelectionContestsCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>> Handle(
        ReplacePersonnelFileSelectionContestsCommand command,
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

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFileSelectionContest.Create(
                item.ContestCode,
                item.ContestName,
                item.ContestDateUtc,
                item.ResultCode,
                item.Notes,
                item.SourceSystem,
                item.SourceReference,
                item.SourceSyncedUtc);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplaceSelectionContestsAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated selection contests for {personnelFile.FullName}.", response, cancellationToken);
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

internal sealed class ReplacePersonnelFileCurricularCompetenciesCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ReplacePersonnelFileCurricularCompetenciesCommand, PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>
{
    public async Task<Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>> Handle(
        ReplacePersonnelFileCurricularCompetenciesCommand command,
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

        var entities = command.Items.Select(item =>
        {
            var entity = PersonnelFileCurricularCompetency.Create(
                item.RequirementTypeCode,
                item.RequirementName,
                item.CompetencyDomain,
                item.ExperienceTimeValue,
                item.MetricCode,
                item.Notes,
                item.SourceSystem,
                item.SourceReference,
                item.SourceSyncedUtc);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(personnelFile.TenantId);
            return entity;
        }).ToArray();

        var response = await employeeRepository.ReplaceCurricularCompetenciesAsync(personnelFile.Id, personnelFile.TenantId, entities, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated curricular competencies for {personnelFile.FullName}.", response, cancellationToken);
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
