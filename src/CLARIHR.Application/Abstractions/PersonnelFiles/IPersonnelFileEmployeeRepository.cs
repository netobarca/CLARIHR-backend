using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Common;
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

    Task<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>> AddEmploymentAssignmentAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileEmploymentAssignment entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileEmploymentAssignmentResponse?> UpdateEmploymentAssignmentAsync(
        Guid employmentAssignmentPublicId,
        Guid tenantId,
        string assignmentTypeCode,
        string? contractTypeCode,
        string? workdayCode,
        string? payrollTypeCode,
        Guid? positionSlotPublicId,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime startDate,
        DateTime? endDate,
        bool isPrimary,
        string? notes,
        string? paymentMethodCode,
        Guid? paymentBankAccountPublicId,
        CancellationToken cancellationToken);

    Task<PersonnelFileEmploymentAssignmentResponse?> PatchEmploymentAssignmentAsync(
        Guid employmentAssignmentPublicId,
        Guid tenantId,
        string assignmentTypeCode,
        string? contractTypeCode,
        string? workdayCode,
        string? payrollTypeCode,
        Guid? positionSlotPublicId,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime startDate,
        DateTime? endDate,
        bool isPrimary,
        string? notes,
        string? paymentMethodCode,
        Guid? paymentBankAccountPublicId,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken);

    Task<bool> DeleteEmploymentAssignmentAsync(
        Guid employmentAssignmentPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>> GetEmploymentAssignmentsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileEmploymentAssignmentResponse?> GetEmploymentAssignmentAsync(
        Guid personnelFileId,
        Guid employmentAssignmentPublicId,
        CancellationToken cancellationToken);

    Task<int> CountOverlappingActiveAssignmentsForSlotAsync(
        Guid tenantId,
        Guid positionSlotPublicId,
        DateTime startDate,
        DateTime? endDate,
        Guid? excludeAssignmentPublicId,
        CancellationToken cancellationToken);

    Task DemoteEmploymentAssignmentsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> assignmentPublicIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Closes every active employment assignment of the file (ends + deactivates) so the prior
    /// employment period is preserved as derived history before a rehire opens a new one (RF-004).
    /// </summary>
    Task CloseActiveEmploymentAssignmentsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        DateTime endDateUtc,
        CancellationToken cancellationToken);

    /// <summary>Closes every active contract of the file before a rehire opens a new one (RF-004).</summary>
    Task CloseActiveContractHistoriesAsync(
        long personnelFileInternalId,
        Guid tenantId,
        DateTime endDateUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileContractHistoryResponse>> AddContractHistoryAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileContractHistory entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileContractHistoryResponse?> UpdateContractHistoryAsync(
        Guid contractHistoryPublicId,
        Guid tenantId,
        string contractTypeCode,
        DateTime contractDate,
        DateTime? contractEndDate,
        Guid? positionSlotPublicId,
        string? notes,
        CancellationToken cancellationToken);

    Task<PersonnelFileContractHistoryResponse?> PatchContractHistoryAsync(
        Guid contractHistoryPublicId,
        Guid tenantId,
        string contractTypeCode,
        DateTime contractDate,
        DateTime? contractEndDate,
        Guid? positionSlotPublicId,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileContractHistoryResponse>> GetContractHistoryAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileContractHistoryResponse?> GetContractHistoryAsync(
        Guid personnelFileId,
        Guid contractHistoryPublicId,
        CancellationToken cancellationToken);

    Task<PersonnelFilePositionHierarchyResponse> GetPositionHierarchyAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileCompensationConceptResponse>> AddCompensationConceptAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileCompensationConcept entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileCompensationConceptResponse?> UpdateCompensationConceptAsync(
        Guid compensationConceptPublicId,
        Guid tenantId,
        Guid? assignedPositionPublicId,
        CompensationNature nature,
        string conceptTypeCode,
        DeductionClass? deductionClass,
        CompensationCalculationType calculationType,
        decimal value,
        string? calculationBaseCode,
        decimal? employerRate,
        decimal? contributionCap,
        string currencyCode,
        string payPeriodCode,
        string? counterpartyName,
        string? externalReference,
        DateTime startDate,
        DateTime? endDate,
        string? notes,
        CancellationToken cancellationToken);

    Task<PersonnelFileCompensationConceptResponse?> PatchCompensationConceptAsync(
        Guid compensationConceptPublicId,
        Guid tenantId,
        Guid? assignedPositionPublicId,
        CompensationNature nature,
        string conceptTypeCode,
        DeductionClass? deductionClass,
        CompensationCalculationType calculationType,
        decimal value,
        string? calculationBaseCode,
        decimal? employerRate,
        decimal? contributionCap,
        string currencyCode,
        string payPeriodCode,
        string? counterpartyName,
        string? externalReference,
        DateTime startDate,
        DateTime? endDate,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken);

    Task<bool> DeleteCompensationConceptAsync(
        Guid compensationConceptPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileCompensationConceptResponse>> GetCompensationConceptsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileCompensationConceptResponse?> GetCompensationConceptAsync(
        Guid personnelFileId,
        Guid compensationConceptPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>> AddAdditionalBenefitAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileAdditionalBenefit entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileAdditionalBenefitResponse?> UpdateAdditionalBenefitAsync(
        Guid additionalBenefitPublicId,
        Guid tenantId,
        string benefitTypeCode,
        DateTime? startDate,
        DateTime? endDate,
        string? notes,
        CancellationToken cancellationToken);

    Task<PersonnelFileAdditionalBenefitResponse?> PatchAdditionalBenefitAsync(
        Guid additionalBenefitPublicId,
        Guid tenantId,
        string benefitTypeCode,
        DateTime? startDate,
        DateTime? endDate,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken);

    Task<bool> DeleteAdditionalBenefitAsync(
        Guid additionalBenefitPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>> GetAdditionalBenefitsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileAdditionalBenefitResponse?> GetAdditionalBenefitAsync(
        Guid personnelFileId,
        Guid additionalBenefitPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>> AddAuthorizationSubstitutionAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileAuthorizationSubstitution entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileAuthorizationSubstitutionResponse?> UpdateAuthorizationSubstitutionAsync(
        Guid authorizationSubstitutionPublicId,
        Guid tenantId,
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        Guid substitutePositionSlotPublicId,
        string? substitutePositionTitleSnapshot,
        DateTime startDate,
        DateTime endDate,
        string? notes,
        CancellationToken cancellationToken);

    Task<PersonnelFileAuthorizationSubstitutionResponse?> PatchAuthorizationSubstitutionAsync(
        Guid authorizationSubstitutionPublicId,
        Guid tenantId,
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        Guid substitutePositionSlotPublicId,
        string? substitutePositionTitleSnapshot,
        DateTime startDate,
        DateTime endDate,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken);

    Task<bool> DeleteAuthorizationSubstitutionAsync(
        Guid authorizationSubstitutionPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>> GetAuthorizationSubstitutionsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileAuthorizationSubstitutionResponse?> GetAuthorizationSubstitutionAsync(
        Guid personnelFileId,
        Guid authorizationSubstitutionPublicId,
        CancellationToken cancellationToken);

    Task<PersonnelFilePersonnelActionResponse> AddPersonnelActionAsync(
        PersonnelFilePersonnelAction entity,
        CancellationToken cancellationToken);

    Task<PersonnelFilePersonnelActionResponse?> GetPersonnelActionAsync(
        Guid personnelFileId,
        Guid personnelActionPublicId,
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

    Task<PersonnelFilePayrollTransactionResponse?> GetPayrollTransactionAsync(
        Guid personnelFileId,
        Guid payrollTransactionPublicId,
        CancellationToken cancellationToken);

    Task<PersonnelFilePayrollTransactionResponse?> PatchPayrollTransactionAsync(
        Guid payrollTransactionPublicId,
        Guid tenantId,
        bool isActive,
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

    Task<IReadOnlyCollection<PersonnelFileAssetAccessResponse>> AddAssetAccessAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileAssetAccess entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileAssetAccessResponse?> UpdateAssetAccessAsync(
        Guid assetAccessPublicId,
        Guid tenantId,
        string assetTypeCode,
        string assetOrAccessName,
        string? accessLevelCode,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        DateTime? deliveryDateUtc,
        string? deliveryStatusCode,
        string? notes,
        CancellationToken cancellationToken);

    Task<PersonnelFileAssetAccessResponse?> PatchAssetAccessAsync(
        Guid assetAccessPublicId,
        Guid tenantId,
        string assetTypeCode,
        string assetOrAccessName,
        string? accessLevelCode,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        DateTime? deliveryDateUtc,
        string? deliveryStatusCode,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken);

    Task<bool> DeleteAssetAccessAsync(
        Guid assetAccessPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileAssetAccessResponse>> GetAssetsAccessesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileAssetAccessResponse?> GetAssetAccessAsync(
        Guid personnelFileId,
        Guid assetAccessPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileInsuranceResponse>> AddInsuranceAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileInsurance entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileInsuranceResponse?> UpdateInsuranceAsync(
        Guid insurancePublicId,
        Guid tenantId,
        string insuranceCode,
        decimal? employeeContribution,
        decimal? employerContribution,
        string? rangeCode,
        string? policyNumber,
        decimal? insuredAmount,
        string? currencyCode,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        CancellationToken cancellationToken);

    Task<PersonnelFileInsuranceResponse?> PatchInsuranceAsync(
        Guid insurancePublicId,
        Guid tenantId,
        string insuranceCode,
        decimal? employeeContribution,
        decimal? employerContribution,
        string? rangeCode,
        string? policyNumber,
        decimal? insuredAmount,
        string? currencyCode,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken);

    Task<bool> DeleteInsuranceAsync(
        Guid insurancePublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileInsuranceResponse>> GetInsurancesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileInsuranceResponse?> GetInsuranceAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        CancellationToken cancellationToken);

    Task<PersonnelFileInsuranceBeneficiaryResponse?> AddInsuranceBeneficiaryAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        Guid tenantId,
        InsuranceBeneficiaryInput item,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryResponse>> GetInsuranceBeneficiariesAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        CancellationToken cancellationToken);

    Task<PersonnelFileInsuranceBeneficiaryResponse?> GetInsuranceBeneficiaryAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        CancellationToken cancellationToken);

    Task<PersonnelFileInsuranceBeneficiaryResponse?> UpdateInsuranceBeneficiaryAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        Guid tenantId,
        string fullName,
        string? documentNumber,
        DateTime? birthDate,
        string kinshipCode,
        string? documentTypeCode,
        decimal? allocationPercentage,
        string? beneficiaryType,
        CancellationToken cancellationToken);

    Task<PersonnelFileInsuranceBeneficiaryResponse?> PatchInsuranceBeneficiaryAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        Guid tenantId,
        string fullName,
        string? documentNumber,
        DateTime? birthDate,
        string kinshipCode,
        string? documentTypeCode,
        decimal? allocationPercentage,
        string? beneficiaryType,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken);

    Task<bool> DeleteInsuranceBeneficiaryAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>> AddMedicalClaimAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileMedicalClaim entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileMedicalClaimResponse?> UpdateMedicalClaimAsync(
        Guid medicalClaimPublicId,
        Guid tenantId,
        MedicalClaimInput input,
        string? insuranceNameSnapshot,
        string? patientNameSnapshot,
        string? kinshipCodeSnapshot,
        CancellationToken cancellationToken);

    Task<PersonnelFileMedicalClaimResponse?> PatchMedicalClaimAsync(
        Guid medicalClaimPublicId,
        Guid tenantId,
        MedicalClaimInput input,
        string? insuranceNameSnapshot,
        string? patientNameSnapshot,
        string? kinshipCodeSnapshot,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken);

    Task<bool> DeleteMedicalClaimAsync(
        Guid medicalClaimPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>> GetMedicalClaimsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileMedicalClaimResponse?> GetMedicalClaimAsync(
        Guid personnelFileId,
        Guid medicalClaimPublicId,
        CancellationToken cancellationToken);

    Task<long?> GetMedicalClaimInternalIdAsync(
        Guid personnelFileId,
        Guid medicalClaimPublicId,
        CancellationToken cancellationToken);

    Task AddMedicalClaimDocumentAsync(
        MedicalClaimDocument entity,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MedicalClaimDocumentResponse>> GetMedicalClaimDocumentsAsync(
        Guid medicalClaimPublicId,
        CancellationToken cancellationToken);

    Task<MedicalClaimDocumentResponse?> GetMedicalClaimDocumentAsync(
        Guid medicalClaimPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken);

    Task<MedicalClaimDocument?> GetMedicalClaimDocumentEntityAsync(
        Guid medicalClaimPublicId,
        Guid documentPublicId,
        Guid tenantId,
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

    Task<bool> DeletePerformanceEvaluationAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>> GetPerformanceEvaluationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFilePerformanceEvaluationResponse?> GetPerformanceEvaluationAsync(
        Guid personnelFileId,
        Guid evaluationPublicId,
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

    Task<bool> DeletePositionCompetencyResultAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>> GetPositionCompetencyResultsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFilePositionCompetencyResultResponse?> GetPositionCompetencyResultAsync(
        Guid personnelFileId,
        Guid positionCompetencyResultPublicId,
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

    Task<bool> DeleteSelectionContestAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileSelectionContestResponse>> GetSelectionContestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileSelectionContestResponse?> GetSelectionContestAsync(
        Guid personnelFileId,
        Guid selectionContestPublicId,
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

    Task<bool> DeleteCurricularCompetencyAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>> GetCurricularCompetenciesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileCurricularCompetencyResponse?> GetCurricularCompetencyAsync(
        Guid personnelFileId,
        Guid curricularCompetencyPublicId,
        CancellationToken cancellationToken);
}
