using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

public interface IPersonnelFileEmployeeRepository
{
    Task<PersonnelFileEmployeeProfileResponse> UpsertEmployeeProfileAsync(
        PersonnelFileEmployeeProfile profile,
        CancellationToken cancellationToken);

    Task<PersonnelFileEmployeeProfileResponse?> GetEmployeeProfileAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>> ReplaceEmploymentAssignmentsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileEmploymentAssignment> entities,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>> GetEmploymentAssignmentsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileContractHistoryResponse>> ReplaceContractHistoryAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileContractHistory> entities,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileContractHistoryResponse>> GetContractHistoryAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFilePositionHierarchyResponse> GetPositionHierarchyAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileSalaryItemResponse>> AddSalaryItemAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileSalaryItem entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileSalaryItemResponse?> UpdateSalaryItemAsync(
        Guid itemPublicId,
        Guid tenantId,
        string incomeTypeCode,
        string salaryRubricCode,
        string currencyCode,
        string payPeriodCode,
        decimal amount,
        DateTime startDate,
        DateTime? endDate,
        bool isActive,
        CancellationToken cancellationToken);

    Task<bool> DeactivateSalaryItemAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileSalaryItemResponse>> GetSalaryItemsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>> AddAdditionalBenefitAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileAdditionalBenefit entity,
        CancellationToken cancellationToken);

    Task<bool> UpdateAdditionalBenefitAsync(
        Guid itemPublicId,
        Guid tenantId,
        string benefitTypeCode,
        DateTime? startDate,
        DateTime? endDate,
        bool isActive,
        string? notes,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAdditionalBenefitAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>> GetAdditionalBenefitsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>> AddPaymentMethodAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFilePaymentMethod entity,
        CancellationToken cancellationToken);

    Task<bool> UpdatePaymentMethodAsync(
        Guid itemPublicId,
        Guid tenantId,
        string paymentMethodCode,
        Guid? bankAccountPublicId,
        bool isPrimary,
        bool isActive,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes,
        CancellationToken cancellationToken);

    Task<bool> DeactivatePaymentMethodAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>> GetPaymentMethodsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>> ReplaceAuthorizationSubstitutionsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileAuthorizationSubstitution> entities,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>> GetAuthorizationSubstitutionsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFilePersonnelActionResponse> AddPersonnelActionAsync(
        PersonnelFilePersonnelAction entity,
        CancellationToken cancellationToken);

    Task<PagedResponse<PersonnelFilePersonnelActionResponse>> SearchPersonnelActionsAsync(
        Guid personnelFileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? type,
        string? status,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>> ExportPersonnelActionsAsync(
        Guid personnelFileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? type,
        string? status,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int? maxRows,
        CancellationToken cancellationToken);

    Task<PersonnelFilePayrollTransactionResponse> AddPayrollTransactionAsync(
        PersonnelFilePayrollTransaction entity,
        CancellationToken cancellationToken);

    Task<bool> DeactivatePayrollTransactionAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<PagedResponse<PersonnelFilePayrollTransactionResponse>> SearchPayrollTransactionsAsync(
        Guid personnelFileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? type,
        string? status,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>> ExportPayrollTransactionsAsync(
        Guid personnelFileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? type,
        string? status,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int? maxRows,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAssetAccessResponse>> ReplaceAssetsAccessesAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileAssetAccess> entities,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAssetAccessResponse>> GetAssetsAccessesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileInsuranceResponse>> AddInsuranceAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileInsurance entity,
        CancellationToken cancellationToken);

    Task<bool> UpdateInsuranceAsync(
        Guid itemPublicId,
        Guid tenantId,
        string insuranceCode,
        decimal? employeeContribution,
        decimal? employerContribution,
        string? rangeCode,
        string? policyNumber,
        decimal? insuredAmount,
        string? currencyCode,
        bool isActive,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        IReadOnlyCollection<InsuranceBeneficiaryInput> beneficiaries,
        CancellationToken cancellationToken);

    Task<bool> DeactivateInsuranceAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileInsuranceResponse>> GetInsurancesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>> AddMedicalClaimAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileMedicalClaim entity,
        CancellationToken cancellationToken);

    Task<bool> UpdateMedicalClaimAsync(
        Guid itemPublicId,
        Guid tenantId,
        Guid? insurancePublicId,
        string? accountNumber,
        string claimTypeCode,
        string? diagnosis,
        decimal? claimAmount,
        string? currencyCode,
        decimal? paidAmount,
        int? responseTimeDays,
        string? notes,
        DateTime claimDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        CancellationToken cancellationToken);

    Task<bool> DeactivateMedicalClaimAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>> GetMedicalClaimsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>> AddPerformanceEvaluationAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFilePerformanceEvaluation entity,
        CancellationToken cancellationToken);

    Task<PersonnelFilePerformanceEvaluationResponse?> UpdatePerformanceEvaluationAsync(
        Guid itemPublicId,
        Guid tenantId,
        string evaluatorName,
        DateTime evaluationDateUtc,
        decimal? score,
        string? qualitativeScoreCode,
        string? comment,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>> GetPerformanceEvaluationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>> AddPositionCompetencyResultAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFilePositionCompetencyResult entity,
        CancellationToken cancellationToken);

    Task<PersonnelFilePositionCompetencyResultResponse?> UpdatePositionCompetencyResultAsync(
        Guid itemPublicId,
        Guid tenantId,
        string competencyCode,
        string? desiredBehaviors,
        decimal? expectedScore,
        decimal? achievedScore,
        decimal? gapScore,
        DateTime? evaluationDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>> GetPositionCompetencyResultsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileSelectionContestResponse>> AddSelectionContestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileSelectionContest entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileSelectionContestResponse?> UpdateSelectionContestAsync(
        Guid itemPublicId,
        Guid tenantId,
        string contestCode,
        string contestName,
        DateTime contestDateUtc,
        string resultCode,
        string? notes,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileSelectionContestResponse>> GetSelectionContestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>> AddCurricularCompetencyAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileCurricularCompetency entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileCurricularCompetencyResponse?> UpdateCurricularCompetencyAsync(
        Guid itemPublicId,
        Guid tenantId,
        string requirementTypeCode,
        string requirementName,
        string competencyDomain,
        decimal? experienceTimeValue,
        string? metricCode,
        string? notes,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>> GetCurricularCompetenciesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);
}
