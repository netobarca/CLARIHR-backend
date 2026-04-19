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

    Task<IReadOnlyCollection<PersonnelFileSalaryItemResponse>> ReplaceSalaryItemsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileSalaryItem> entities,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileSalaryItemResponse>> GetSalaryItemsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>> ReplaceAdditionalBenefitsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileAdditionalBenefit> entities,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>> GetAdditionalBenefitsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>> ReplacePaymentMethodsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFilePaymentMethod> entities,
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

    Task<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>> ReplacePayrollTransactionsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFilePayrollTransaction> entities,
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

    Task<IReadOnlyCollection<PersonnelFileInsuranceResponse>> ReplaceInsurancesAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileInsurance> entities,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileInsuranceResponse>> GetInsurancesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>> ReplaceMedicalClaimsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileMedicalClaim> entities,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>> GetMedicalClaimsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>> ReplacePerformanceEvaluationsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFilePerformanceEvaluation> entities,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>> GetPerformanceEvaluationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>> ReplacePositionCompetencyResultsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFilePositionCompetencyResult> entities,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>> GetPositionCompetencyResultsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileSelectionContestResponse>> ReplaceSelectionContestsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileSelectionContest> entities,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileSelectionContestResponse>> GetSelectionContestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>> ReplaceCurricularCompetenciesAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileCurricularCompetency> entities,
        CancellationToken cancellationToken);
}
