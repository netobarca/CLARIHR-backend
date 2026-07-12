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
        int? restDayOfWeek,
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
        int? restDayOfWeek,
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

    Task<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>> AddOffPayrollTransactionAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileOffPayrollTransaction entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileOffPayrollTransactionResponse?> UpdateOffPayrollTransactionAsync(
        Guid offPayrollTransactionPublicId,
        Guid tenantId,
        OffPayrollTransactionInput input,
        string currencyCode,
        string? transactionTypeNameSnapshot,
        string? assetNameSnapshot,
        CancellationToken cancellationToken);

    Task<PersonnelFileOffPayrollTransactionResponse?> PatchOffPayrollTransactionAsync(
        Guid offPayrollTransactionPublicId,
        Guid tenantId,
        OffPayrollTransactionInput input,
        string currencyCode,
        string? transactionTypeNameSnapshot,
        string? assetNameSnapshot,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken);

    Task<bool> SoftDeleteOffPayrollTransactionAsync(
        Guid offPayrollTransactionPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>> GetOffPayrollTransactionsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileOffPayrollTransactionResponse?> GetOffPayrollTransactionAsync(
        Guid personnelFileId,
        Guid offPayrollTransactionPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>> GetOffPayrollTransactionTotalsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<long?> GetOffPayrollTransactionInternalIdAsync(
        Guid personnelFileId,
        Guid offPayrollTransactionPublicId,
        CancellationToken cancellationToken);

    Task AddOffPayrollTransactionDocumentAsync(
        OffPayrollTransactionDocument entity,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<OffPayrollTransactionDocumentResponse>> GetOffPayrollTransactionDocumentsAsync(
        Guid offPayrollTransactionPublicId,
        CancellationToken cancellationToken);

    Task<OffPayrollTransactionDocumentResponse?> GetOffPayrollTransactionDocumentAsync(
        Guid offPayrollTransactionPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken);

    Task<OffPayrollTransactionDocument?> GetOffPayrollTransactionDocumentEntityAsync(
        Guid offPayrollTransactionPublicId,
        Guid documentPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileEconomicAidRequestResponse>> AddEconomicAidRequestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileEconomicAidRequest entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileEconomicAidRequestResponse?> UpdateEconomicAidRequestAsync(
        Guid economicAidRequestPublicId,
        Guid tenantId,
        EconomicAidRequestInput input,
        string currencyCode,
        string? typeNameSnapshot,
        CancellationToken cancellationToken);

    Task<bool> SoftDeleteEconomicAidRequestAsync(
        Guid economicAidRequestPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileEconomicAidRequestResponse>> GetEconomicAidRequestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileEconomicAidRequestResponse?> GetEconomicAidRequestAsync(
        Guid personnelFileId,
        Guid economicAidRequestPublicId,
        CancellationToken cancellationToken);

    // Validation actions (resolve/disburse/cancel — D-03/D-09/D-11): load the tracked entity, apply the domain
    // transition (handlers pre-validate so the domain guards never throw), and return the refreshed response.
    Task<PersonnelFileEconomicAidRequestResponse?> ResolveEconomicAidRequestAsync(
        Guid economicAidRequestPublicId,
        Guid tenantId,
        string targetStatusCode,
        decimal? approvedAmount,
        Guid decidedByUserId,
        DateTime decidedAtUtc,
        string? notes,
        CancellationToken cancellationToken);

    Task<PersonnelFileEconomicAidRequestResponse?> DisburseEconomicAidRequestAsync(
        Guid economicAidRequestPublicId,
        Guid tenantId,
        decimal disbursedAmount,
        DateTime disbursementDateUtc,
        string? paymentMethodCode,
        CancellationToken cancellationToken);

    Task<PersonnelFileEconomicAidRequestResponse?> CancelEconomicAidRequestAsync(
        Guid economicAidRequestPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<long?> GetEconomicAidRequestInternalIdAsync(
        Guid personnelFileId,
        Guid economicAidRequestPublicId,
        CancellationToken cancellationToken);

    // ── Retirement requests ("retiro definitivo") — D-01…D-19 ───────────────────────────────────────────
    Task<IReadOnlyCollection<PersonnelFileRetirementRequestResponse>> AddRetirementRequestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileRetirementRequest entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileRetirementRequestResponse?> UpdateRetirementRequestAsync(
        Guid retirementRequestPublicId,
        Guid tenantId,
        RetirementRequestInput input,
        string requesterNameSnapshot,
        string? categoryNameSnapshot,
        string? reasonNameSnapshot,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileRetirementRequestResponse>> GetRetirementRequestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileRetirementRequestResponse?> GetRetirementRequestAsync(
        Guid personnelFileId,
        Guid retirementRequestPublicId,
        CancellationToken cancellationToken);

    /// <summary>Tracked entity for the action handlers (cancel/resolve/execute/revert) — tenant + file fenced.</summary>
    Task<PersonnelFileRetirementRequest?> GetRetirementRequestEntityAsync(
        Guid personnelFileId,
        Guid retirementRequestPublicId,
        Guid tenantId,
        bool includeClosedRecords,
        CancellationToken cancellationToken);

    /// <summary>RN-001.2: whether the employee already has an open (SOLICITADA/AUTORIZADA) active request.</summary>
    Task<bool> HasOpenRetirementRequestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>Requester lookup (D-02/D-13): display name + linked login of a personnel file of the company.</summary>
    Task<RetirementRequesterLookup?> GetRetirementRequesterLookupAsync(
        Guid requesterFilePublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>Tracked 1:1 employee profile entity for the retirement execution/reversal orchestration.</summary>
    Task<PersonnelFileEmployeeProfile?> GetEmployeeProfileEntityAsync(
        long personnelFileInternalId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// R-T5 guard input: start dates of the employee's ACTIVE assignments + contracts, to reject an execution
    /// whose retirement date precedes any of them (end-after-start check constraints).
    /// </summary>
    Task<IReadOnlyCollection<DateTime>> GetActiveRowStartDatesAsync(
        long personnelFileInternalId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Same closing semantics as <see cref="CloseActiveEmploymentAssignmentsAsync"/> but CAPTURING each closed
    /// row's public id + previous end date for the reversal snapshot (D-11).
    /// </summary>
    Task<IReadOnlyCollection<RetirementClosedRowCapture>> CloseActiveEmploymentAssignmentsCapturingAsync(
        long personnelFileInternalId,
        Guid tenantId,
        DateTime endDateUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Same closing semantics as <see cref="CloseActiveContractHistoriesAsync"/> but CAPTURING each closed
    /// row's public id + previous contract end date for the reversal snapshot (D-11).
    /// </summary>
    Task<IReadOnlyCollection<RetirementClosedRowCapture>> CloseActiveContractHistoriesCapturingAsync(
        long personnelFileInternalId,
        Guid tenantId,
        DateTime endDateUtc,
        CancellationToken cancellationToken);

    /// <summary>Company-wide retirement bandeja (RF-002): filters + paging + per-status counts.</summary>
    Task<RetirementRequestBandejaResponse> QueryRetirementRequestsAsync(
        QueryRetirementRequestsQuery query,
        CancellationToken cancellationToken);

    /// <summary>Export rows for the retirement bandeja (RF-002), same filters, capped at MaxRows.</summary>
    Task<IReadOnlyCollection<RetirementRequestExportRow>> GetRetirementRequestExportRowsAsync(
        ExportRetirementRequestsQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Interview tray (RF-008): employees whose retirement is AUTORIZADA/EJECUTADA with the derived state of
    /// their exit interview (no active form / pending / draft / submitted).
    /// </summary>
    Task<IReadOnlyCollection<RetirementInterviewTrayItemResponse>> GetRetirementInterviewTrayAsync(
        GetRetirementInterviewTrayQuery query,
        CancellationToken cancellationToken);

    /// <summary>D-10 signal: whether a personnel action of the given type was journaled after a moment.</summary>
    Task<bool> HasPersonnelActionSinceAsync(
        long personnelFileInternalId,
        Guid tenantId,
        string actionTypeCode,
        DateTime sinceUtc,
        CancellationToken cancellationToken);

    /// <summary>RN-012.3: whether the employee has another retirement request executed after the given moment.</summary>
    Task<bool> HasLaterExecutedRetirementRequestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        Guid excludingRequestPublicId,
        DateTime executionDateUtc,
        CancellationToken cancellationToken);

    /// <summary>Tracked assignment rows by public id (reversal reopen, D-11).</summary>
    Task<IReadOnlyCollection<PersonnelFileEmploymentAssignment>> GetEmploymentAssignmentsByPublicIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> publicIds,
        CancellationToken cancellationToken);

    /// <summary>Tracked contract rows by public id (reversal reopen, D-11).</summary>
    Task<IReadOnlyCollection<PersonnelFileContractHistory>> GetContractHistoriesByPublicIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> publicIds,
        CancellationToken cancellationToken);

    // ── Certificate requests ("constancias") — D-02/D-04 ─────────────────────────────────────────────────
    Task<IReadOnlyCollection<PersonnelFileCertificateRequestResponse>> AddCertificateRequestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileCertificateRequest entity,
        CancellationToken cancellationToken);

    Task<PersonnelFileCertificateRequestResponse?> UpdateCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        CertificateRequestInput input,
        string? typeNameSnapshot,
        CancellationToken cancellationToken);

    Task<bool> SoftDeleteCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileCertificateRequestResponse>> GetCertificateRequestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken);

    Task<PersonnelFileCertificateRequestResponse?> GetCertificateRequestAsync(
        Guid personnelFileId,
        Guid certificateRequestPublicId,
        CancellationToken cancellationToken);

    // Lifecycle actions (process/issue/deliver/reject/cancel — D-04): load the tracked entity, apply the domain
    // transition (handlers pre-validate so the domain guards never throw), and return the refreshed response.
    Task<PersonnelFileCertificateRequestResponse?> ProcessCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<PersonnelFileCertificateRequestResponse?> IssueCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        Guid issuedByUserId,
        DateTime issuedAtUtc,
        string? notes,
        CancellationToken cancellationToken);

    Task<PersonnelFileCertificateRequestResponse?> DeliverCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        DateTime deliveredAtUtc,
        CancellationToken cancellationToken);

    Task<PersonnelFileCertificateRequestResponse?> RejectCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        string? notes,
        CancellationToken cancellationToken);

    Task<PersonnelFileCertificateRequestResponse?> CancelCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<long?> GetCertificateRequestInternalIdAsync(
        Guid personnelFileId,
        Guid certificateRequestPublicId,
        CancellationToken cancellationToken);

    Task AddCertificateRequestDocumentAsync(
        CertificateRequestDocument entity,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CertificateRequestDocumentResponse>> GetCertificateRequestDocumentsAsync(
        Guid certificateRequestPublicId,
        CancellationToken cancellationToken);

    Task<CertificateRequestDocumentResponse?> GetCertificateRequestDocumentAsync(
        Guid certificateRequestPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken);

    Task<CertificateRequestDocument?> GetCertificateRequestDocumentEntityAsync(
        Guid certificateRequestPublicId,
        Guid documentPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    // Company-wide certificate bandeja + export (D-08).
    Task<CertificateRequestBandejaResponse> QueryCertificateRequestsAsync(
        Guid companyId,
        string? typeCode,
        string? statusCode,
        string? purposeCode,
        Guid? employeeId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CertificateRequestExportRow>> GetCertificateRequestExportRowsAsync(
        Guid companyId,
        string? typeCode,
        string? statusCode,
        string? purposeCode,
        Guid? employeeId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? search,
        int? maxRows,
        CancellationToken cancellationToken);

    Task AddEconomicAidRequestDocumentAsync(
        EconomicAidRequestDocument entity,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EconomicAidRequestDocumentResponse>> GetEconomicAidRequestDocumentsAsync(
        Guid economicAidRequestPublicId,
        CancellationToken cancellationToken);

    Task<EconomicAidRequestDocumentResponse?> GetEconomicAidRequestDocumentAsync(
        Guid economicAidRequestPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken);

    Task<EconomicAidRequestDocument?> GetEconomicAidRequestDocumentEntityAsync(
        Guid economicAidRequestPublicId,
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

    void AddPositionCompetencyResult(PersonnelFilePositionCompetencyResult entity);

    Task<bool> UpdatePositionCompetencyResultAsync(
        Guid itemPublicId,
        Guid tenantId,
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long? jobProfileCompetencyExpectationId,
        decimal? expectedScore,
        decimal achievedScore,
        DateTime evaluationDateUtc,
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

    /// <summary>
    /// Resolves the internal id of the job profile of the employee's active primary employment assignment
    /// (active &amp; primary assignment → position slot → job profile). Null when the employee has no resolvable
    /// active position. Used to validate that a scored competency belongs to the position's matrix (RF-011) and
    /// to derive the expected competencies (RF-002).
    /// </summary>
    Task<long?> GetActiveAssignedJobProfileInternalIdAsync(
        Guid personnelFilePublicId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the public id of the position slot of the employee's active primary employment assignment
    /// (active &amp; primary assignment → position slot). Null when the employee has no resolvable active
    /// primary plaza. Used by finalize to treat the already-assigned primary plaza as satisfying the
    /// position-slot requirement without the caller re-sending it (single-active-primary invariant — RN-03).
    /// </summary>
    Task<Guid?> GetActivePrimaryPositionSlotPublicIdAsync(
        Guid personnelFilePublicId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds the "Competencias del puesto" consultation (RF-002): the competency matrix of the employee's
    /// active assigned position combined with the employee's achieved results (latest + history), gap computed,
    /// grouped by competency type. Returns an empty result with <c>HasAssignedPosition = false</c> when the
    /// employee has no resolvable active position.
    /// </summary>
    Task<EmployeePositionCompetenciesResponse> GetEmployeePositionCompetenciesAsync(
        Guid personnelFilePublicId,
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

    // ── Recurring incomes (REQ-005) ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes the balance-mutating writes (installment application / annulment) of a single recurring
    /// income with a transaction-scoped PostgreSQL advisory lock keyed on its public id, closing the
    /// check-then-act TOCTOU on the strict installment sequence (RF-006/RF-008, aclaración №9). Must run inside
    /// an open transaction (the handler opens one); the lock releases on commit/rollback. The default is a no-op
    /// (test fakes have no PostgreSQL); the EF repository takes the real advisory lock.
    /// </summary>
    Task AcquireRecurringIncomeMutationLockAsync(Guid recurringIncomePublicId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Adds a recurring income (idioma post-fix, aclaración №3): stages the entity and returns the file's
    /// recurring incomes including the just-added (not-yet-saved) one, so the create handler can pick the created
    /// record for its response. The unit of work commits afterwards.
    /// </summary>
    Task<IReadOnlyCollection<RecurringIncomeResponse>> AddRecurringIncomeAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileRecurringIncome entity,
        CancellationToken cancellationToken);

    /// <summary>Returns every recurring income of the personnel file (most recent registration first).</summary>
    Task<IReadOnlyCollection<RecurringIncomeResponse>> GetRecurringIncomesAsync(
        Guid personnelFilePublicId,
        CancellationToken cancellationToken);

    /// <summary>Returns a single recurring income by public id (or null when it is not on the file).</summary>
    Task<RecurringIncomeResponse?> GetRecurringIncomeAsync(
        Guid personnelFilePublicId,
        Guid recurringIncomePublicId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads the TRACKED recurring income aggregate (for a domain mutation): the write handlers apply the domain
    /// transition (pre-validated so the guards never throw) and the unit of work commits. Returns null when the
    /// income is not on the file / tenant.
    /// </summary>
    Task<PersonnelFileRecurringIncome?> GetRecurringIncomeEntityAsync(
        Guid personnelFilePublicId,
        Guid recurringIncomePublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the plaza + cost center of a recurring income (P-15): when <paramref name="assignedPositionPublicId"/>
    /// is supplied it must be an assignment of the employee; otherwise the principal plaza is resolved (same
    /// criterion as settlement/vacations — IsPrimary among the active ones, oldest StartDate, then oldest active,
    /// then oldest). The cost center is DERIVED from the resolved plaza; its name is snapshotted. Returns
    /// <see cref="RecurringIncomePlazaResolution.Found"/> = false when no plaza can be resolved.
    /// </summary>
    Task<RecurringIncomePlazaResolution> ResolveRecurringIncomePlazaAsync(
        long personnelFileInternalId,
        Guid? assignedPositionPublicId,
        CancellationToken cancellationToken);

    /// <summary>True when the personnel file's employee profile is RETIRADO (write-locked; EMPLOYEE_PROFILE_RETIRED_LOCKED).</summary>
    Task<bool> IsRecurringIncomeProfileRetiredAsync(
        long personnelFileInternalId,
        CancellationToken cancellationToken);

    // ── One-time deductions (REQ-009) ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes the mutations of one one-time deduction (advisory transaction lock — class id "ODED"), so two
    /// concurrent applications cannot both pass the at-most-one-active-application guard. Fail-safe default
    /// (no-op) so hand-written test doubles need not implement it; the production repository overrides it.
    /// </summary>
    Task AcquireOneTimeDeductionMutationLockAsync(Guid oneTimeDeductionPublicId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>Adds a one-time deduction (idioma post-fix) and returns the file's deductions including it.</summary>
    Task<IReadOnlyCollection<OneTimeDeductionResponse>> AddOneTimeDeductionAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileOneTimeDeduction entity,
        CancellationToken cancellationToken);

    /// <summary>Every one-time deduction of the personnel file (most recent deduction date first).</summary>
    Task<IReadOnlyCollection<OneTimeDeductionResponse>> GetOneTimeDeductionsAsync(
        Guid personnelFilePublicId,
        CancellationToken cancellationToken);

    /// <summary>A single one-time deduction by public id (or null when it is not on the file).</summary>
    Task<OneTimeDeductionResponse?> GetOneTimeDeductionAsync(
        Guid personnelFilePublicId,
        Guid oneTimeDeductionPublicId,
        CancellationToken cancellationToken);

    /// <summary>The TRACKED one-time-deduction aggregate WITH its applications (for a domain mutation).</summary>
    Task<PersonnelFileOneTimeDeduction?> GetOneTimeDeductionEntityAsync(
        Guid personnelFilePublicId,
        Guid oneTimeDeductionPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>Resolves the plaza of a one-time deduction (default the principal one). NO cost center (P-08).</summary>
    Task<OneTimeDeductionPlazaResolution> ResolveOneTimeDeductionPlazaAsync(
        long personnelFileInternalId,
        Guid? assignedPositionPublicId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The requester of a one-time deduction (the trío): the file, its display name (snapshotted) and its linked
    /// login — the THIRD leg of the anti-self triple. Null when the file is not of this company.
    /// </summary>
    Task<OneTimeDeductionRequesterLookup?> GetOneTimeDeductionRequesterLookupAsync(
        Guid requesterFilePublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>The employee's AUTORIZADO one-off deductions (tracked) to mark as charged when a settlement is
    /// issued — but ONLY those whose suggested line stayed INCLUDED (REQ-009 §3.5).</summary>
    Task<IReadOnlyCollection<PersonnelFileOneTimeDeduction>> GetAutorizadoOneTimeDeductionsForSettlementAsync(
        long personnelFileId,
        CancellationToken cancellationToken);

    /// <summary>The employee's one-off deductions charged by a specific settlement (tracked) to reopen when that
    /// settlement is annulled (REQ-009 §3.5).</summary>
    Task<IReadOnlyCollection<PersonnelFileOneTimeDeduction>> GetOneTimeDeductionsAppliedBySettlementAsync(
        long personnelFileId,
        Guid settlementPublicId,
        CancellationToken cancellationToken);

    // ── One-time-deduction bandeja + exports (REQ-009 PR-5) ─────────────────────────────────────────

    /// <summary>The company bandeja: a page + StatusCounts over EVERY status + the totals per currency.</summary>
    Task<OneTimeDeductionBandejaResponse> QueryOneTimeDeductionsAsync(
        QueryOneTimeDeductionsQuery query,
        CancellationToken cancellationToken);

    /// <summary>The bandeja export rows (Spanish headers).</summary>
    Task<IReadOnlyCollection<DescuentoEventualExportRow>> GetOneTimeDeductionExportRowsAsync(
        ExportOneTimeDeductionsQuery query,
        CancellationToken cancellationToken);

    /// <summary>The payroll-input rows: every CHARGED (APLICADA, active) application whose applied date falls in the range.</summary>
    Task<IReadOnlyCollection<InsumoPlanillaDescuentoEventualExportRow>> GetOneTimeDeductionPayrollInputRowsAsync(
        Guid tenantId,
        string? payrollTypeCode,
        DateOnly startDate,
        DateOnly endDate,
        int? maxRows,
        CancellationToken cancellationToken);

    // ── One-time-deduction applications (REQ-009 PR-4) ──────────────────────────────────────────────

    /// <summary>The TRACKED deduction WITH its applications (for an application / reversal under the lock).</summary>
    Task<PersonnelFileOneTimeDeduction?> GetTrackedOneTimeDeductionWithApplicationsAsync(
        Guid oneTimeDeductionPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>The application history of a deduction (APLICADA + ANULADA, most recent first).</summary>
    Task<IReadOnlyCollection<OneTimeDeductionApplicationResponse>> GetOneTimeDeductionApplicationsAsync(
        Guid personnelFilePublicId,
        Guid oneTimeDeductionPublicId,
        CancellationToken cancellationToken);

    /// <summary>Resolves a company payroll-period instance for an application (FK real).</summary>
    Task<OneTimeDeductionPayrollPeriodResolution?> ResolveOneTimeDeductionPayrollPeriodAsync(
        Guid tenantId,
        Guid payrollPeriodPublicId,
        CancellationToken cancellationToken);

    /// <summary>The AUTORIZADO deductions the apply-period batch will charge, ORDERED BY INTERNAL ID (anti-deadlock).</summary>
    Task<IReadOnlyList<OneTimeDeductionBatchCandidate>> GetOneTimeDeductionBatchScanAsync(
        Guid tenantId,
        string payrollTypeCode,
        Guid? payrollPeriodPublicId,
        CancellationToken cancellationToken);

    /// <summary>The company-wide work list: the AUTORIZADO deductions still waiting to be charged.</summary>
    Task<IReadOnlyList<OneTimeDeductionPendingData>> GetOneTimeDeductionPendingAsync(
        Guid tenantId,
        string? payrollTypeCode,
        Guid? payrollPeriodPublicId,
        Guid? employeeId,
        CancellationToken cancellationToken);

    // ── Recurring deductions (REQ-008 PR-3) ─────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes the mutations of one recurring deduction (advisory transaction lock, №11 — class id "RDED"), so
    /// two concurrent installment applications cannot both pass the sequence guard. Fail-safe default (no-op) so
    /// hand-written test doubles need not implement it; the production repository overrides it.
    /// </summary>
    Task AcquireRecurringDeductionMutationLockAsync(Guid recurringDeductionPublicId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Adds a recurring deduction (idioma post-fix): stages the entity and returns the file's recurring deductions
    /// including the just-added (not-yet-saved) one, so the create handler can pick the created record for its
    /// response. The unit of work commits afterwards.
    /// </summary>
    Task<IReadOnlyCollection<RecurringDeductionResponse>> AddRecurringDeductionAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileRecurringDeduction entity,
        CancellationToken cancellationToken);

    /// <summary>Returns every recurring deduction of the personnel file (most recent effective date first).</summary>
    Task<IReadOnlyCollection<RecurringDeductionResponse>> GetRecurringDeductionsAsync(
        Guid personnelFilePublicId,
        CancellationToken cancellationToken);

    /// <summary>Returns a single recurring deduction by public id (or null when it is not on the file).</summary>
    Task<RecurringDeductionResponse?> GetRecurringDeductionAsync(
        Guid personnelFilePublicId,
        Guid recurringDeductionPublicId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads the TRACKED recurring deduction aggregate WITH its plan segments (for a domain mutation): the write
    /// handlers apply the domain transition (pre-validated so the guards never throw) and the unit of work
    /// commits. Returns null when the credit is not on the file / tenant.
    /// </summary>
    Task<PersonnelFileRecurringDeduction?> GetRecurringDeductionEntityAsync(
        Guid personnelFilePublicId,
        Guid recurringDeductionPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the plaza of a recurring deduction (D-13): when <paramref name="assignedPositionPublicId"/> is
    /// supplied it must be an assignment of the employee; otherwise the principal plaza is resolved (IsPrimary
    /// among the active ones, oldest StartDate, then oldest active, then oldest). Unlike the recurring income
    /// there is NO cost center (P-08). Returns Found = false when no plaza can be resolved.
    /// </summary>
    Task<RecurringDeductionPlazaResolution> ResolveRecurringDeductionPlazaAsync(
        long personnelFileInternalId,
        Guid? assignedPositionPublicId,
        CancellationToken cancellationToken);

    // ── Recurring-deduction charges (REQ-008 PR-4) ──────────────────────────────────────────────────

    /// <summary>
    /// Loads the TRACKED recurring deduction WITH its plan segments and its applied charges (for an application /
    /// extraordinary payment / annulment under the advisory lock). Scoped to the tenant + credit public id.
    /// Do NOT pre-load the credit tracked before the lock — call this as the FIRST tracking load so it reflects
    /// any committed concurrent application.
    /// </summary>
    Task<PersonnelFileRecurringDeduction?> GetTrackedRecurringDeductionWithInstallmentsAsync(
        Guid recurringDeductionPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>Raw plan + charged data of a recurring deduction, so the schedule query can derive the projection.</summary>
    Task<RecurringDeductionScheduleData?> GetRecurringDeductionScheduleDataAsync(
        Guid personnelFilePublicId,
        Guid recurringDeductionPublicId,
        CancellationToken cancellationToken);

    /// <summary>A page of the charge history (APLICADA + ANULADA, regular + extraordinary).</summary>
    Task<RecurringDeductionInstallmentHistoryResponse?> GetRecurringDeductionInstallmentHistoryAsync(
        Guid personnelFilePublicId,
        Guid recurringDeductionPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Resolves a company payroll-period instance for a charge imputation (FK real).</summary>
    Task<RecurringDeductionPayrollPeriodResolution?> ResolveRecurringDeductionPayrollPeriodAsync(
        Guid tenantId,
        Guid payrollPeriodPublicId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The AsNoTracking scan the apply-period batch runs on: every VIGENTE credit of the payroll type, ORDERED BY
    /// INTERNAL ID (the batch takes its locks in that order — anti-deadlock), with the plan data and the applied
    /// charge numbers needed to compute the pending ones.
    /// </summary>
    Task<IReadOnlyList<RecurringDeductionBatchScanItem>> GetRecurringDeductionBatchScanAsync(
        Guid tenantId,
        string payrollTypeCode,
        CancellationToken cancellationToken);

    // ── Recurring-deduction bandejas + exports (REQ-008 PR-5) ───────────────────────────────────────

    /// <summary>The company-wide credit bandeja: a page + StatusCounts over EVERY status + the charged/outstanding
    /// totals per currency (both derived — never stored).</summary>
    Task<RecurringDeductionBandejaResponse> QueryRecurringDeductionsAsync(
        QueryRecurringDeductionsQuery query,
        CancellationToken cancellationToken);

    /// <summary>The bandeja export rows (Spanish headers).</summary>
    Task<IReadOnlyCollection<DescuentoCiclicoExportRow>> GetRecurringDeductionExportRowsAsync(
        ExportRecurringDeductionsQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// The VIGENTE credits the pending-charges projection scans, each carrying the very
    /// <see cref="RecurringDeductionBatchScanItem"/> the apply-period batch consumes (so the bandeja and the batch
    /// cuadran by construction) plus the employee / plaza metadata the bandeja rows need.
    /// </summary>
    Task<IReadOnlyList<RecurringDeductionPendingScanItem>> GetRecurringDeductionPendingScanAsync(
        Guid tenantId,
        string? payrollTypeCode,
        Guid? employeeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The payroll-input rows: every APPLIED charge (regular AND extraordinary) of the payroll type whose applied
    /// date falls in the range, with the creditor + reference + capital/interest split the payroll operator needs.
    /// </summary>
    Task<IReadOnlyCollection<InsumoPlanillaDescuentoExportRow>> GetRecurringDeductionPayrollInputRowsAsync(
        Guid tenantId,
        string? payrollTypeCode,
        DateOnly startDate,
        DateOnly endDate,
        int? maxRows,
        CancellationToken cancellationToken);

    // ── Recurring-income installments (REQ-005 PR-4) ────────────────────────────────────────────────

    /// <summary>
    /// Loads the TRACKED recurring income WITH its installments (for an installment application / annulment
    /// under the advisory lock). Scoped to the tenant + income public id; returns null when it is not found.
    /// Do NOT pre-load the income tracked before the lock — call this as the FIRST tracking load so it reflects
    /// any committed concurrent application.
    /// </summary>
    Task<PersonnelFileRecurringIncome?> GetTrackedRecurringIncomeWithInstallmentsAsync(
        Guid recurringIncomePublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>The employee's VIGENTE recurring incomes (tracked) to finalize when a settlement is issued
    /// (FinalizeBySettlement, REQ-005 §3.5); empty when none. The employee's cyclic incomes end when settled.</summary>
    Task<IReadOnlyCollection<PersonnelFileRecurringIncome>> GetVigenteRecurringIncomesForSettlementAsync(
        long personnelFileId,
        CancellationToken cancellationToken);

    /// <summary>The employee's VIGENTE recurring deductions (tracked) to finalize when a settlement is issued
    /// (FinalizeBySettlement, REQ-008 §3.5); empty when none. Those with DESCONTAR_SALDO were discounted through
    /// the suggested line; those with CANCELAR are written off (condonación).</summary>
    Task<IReadOnlyCollection<PersonnelFileRecurringDeduction>> GetVigenteRecurringDeductionsForSettlementAsync(
        long personnelFileId,
        CancellationToken cancellationToken);

    /// <summary>The employee's recurring deductions closed by a specific settlement (tracked) to reopen when that
    /// settlement is annulled (ReopenFromSettlement, REQ-008 §3.5); empty when none.</summary>
    Task<IReadOnlyCollection<PersonnelFileRecurringDeduction>> GetRecurringDeductionsClosedBySettlementAsync(
        long personnelFileId,
        Guid settlementPublicId,
        CancellationToken cancellationToken);

    /// <summary>The employee's recurring incomes closed by a specific settlement (tracked) to reopen when that
    /// settlement is annulled (ReopenFromSettlement, REQ-005 §3.5); empty when none.</summary>
    Task<IReadOnlyCollection<PersonnelFileRecurringIncome>> GetRecurringIncomesClosedBySettlementAsync(
        long personnelFileId,
        Guid settlementPublicId,
        CancellationToken cancellationToken);

    /// <summary>The employee's AUTORIZADO one-time incomes (tracked) to mark applied when a settlement is issued
    /// (MarkAppliedBySettlement, REQ-006 §3.5), filtered by which suggestion lines stayed included; empty when none.</summary>
    Task<IReadOnlyCollection<PersonnelFileOneTimeIncome>> GetAutorizadoOneTimeIncomesForSettlementAsync(
        long personnelFileId,
        CancellationToken cancellationToken);

    /// <summary>The employee's one-time incomes applied by a specific settlement (tracked) to reopen when that
    /// settlement is annulled (ReopenFromSettlement, REQ-006 §3.5); empty when none.</summary>
    Task<IReadOnlyCollection<PersonnelFileOneTimeIncome>> GetOneTimeIncomesAppliedBySettlementAsync(
        long personnelFileId,
        Guid settlementPublicId,
        CancellationToken cancellationToken);

    /// <summary>Raw plan + applied-installment-number data of a recurring income (AsNoTracking) for the derived
    /// schedule projection; null when the income is not on the file.</summary>
    Task<RecurringIncomeScheduleData?> GetRecurringIncomeScheduleDataAsync(
        Guid personnelFilePublicId,
        Guid recurringIncomePublicId,
        CancellationToken cancellationToken);

    /// <summary>A page of a recurring income's installment history (APLICADA + ANULADA, most recent activity
    /// first, payroll-period public id resolved); null when the income is not on the file.</summary>
    Task<RecurringIncomeInstallmentHistoryResponse?> GetRecurringIncomeInstallmentHistoryAsync(
        Guid personnelFilePublicId,
        Guid recurringIncomePublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Resolves a company payroll-period instance for an installment imputation (§0.13); null when it is
    /// not a period of the tenant.</summary>
    Task<RecurringIncomePayrollPeriodResolution?> ResolveRecurringIncomePayrollPeriodAsync(
        Guid tenantId,
        Guid payrollPeriodPublicId,
        CancellationToken cancellationToken);

    /// <summary>Resolves the public id of a payroll-period instance by its internal id (for the installment
    /// response); null when it no longer exists.</summary>
    Task<Guid?> ResolvePayrollPeriodPublicIdAsync(
        long payrollPeriodInternalId,
        CancellationToken cancellationToken);

    /// <summary>An AsNoTracking snapshot of the tenant's VIGENTE recurring incomes of a payroll type, ordered by
    /// internal id (anti-deadlock ordering for the apply-period batch), each with its applied installment numbers.</summary>
    Task<IReadOnlyList<RecurringIncomeBatchScanItem>> GetRecurringIncomeBatchScanAsync(
        Guid tenantId,
        string payrollTypeCode,
        CancellationToken cancellationToken);

    // ── Recurring-income bandeja + exports (REQ-005 PR-5) ───────────────────────────────────────────

    /// <summary>The company-wide recurring-income bandeja page (RF-010): paginated + filtered items plus
    /// per-status counts (computed over the full non-status filter, so every status is represented).</summary>
    Task<RecurringIncomeBandejaResponse> QueryRecurringIncomesAsync(
        QueryRecurringIncomesQuery query,
        CancellationToken cancellationToken);

    /// <summary>The recurring-income bandeja export rows (same filters as the bandeja; capped at
    /// <c>MaxRows + 1</c> so the caller can detect the synchronous-limit overflow → 413).</summary>
    Task<IReadOnlyCollection<IngresoCiclicoExportRow>> GetRecurringIncomeExportRowsAsync(
        ExportRecurringIncomesQuery query,
        CancellationToken cancellationToken);

    /// <summary>The tenant's VIGENTE recurring incomes (optionally scoped to a payroll type / employee), each
    /// enriched with the employee + plaza metadata and its applied installment numbers, for the pending-installments
    /// projection (RF-011). The theoretical pending installments are projected in-memory by the pure rules.</summary>
    Task<IReadOnlyList<RecurringIncomePendingScanItem>> GetRecurringIncomePendingScanAsync(
        Guid tenantId,
        string? payrollTypeCode,
        Guid? employeeId,
        CancellationToken cancellationToken);

    /// <summary>The payroll-input rows (RF-012 / §5): the APPLIED (<c>APLICADA</c>, active) installments of the
    /// mandatory applied-date range (optionally scoped to a payroll type), one row per installment with its
    /// imputed period label. Capped at <c>MaxRows + 1</c> for the 413 overflow. Cuadra against the pending
    /// installments of the same filter once applied (A.3-10); annulled installments are excluded.</summary>
    Task<IReadOnlyCollection<InsumoPlanillaCiclicoExportRow>> GetRecurringIncomePayrollInputRowsAsync(
        Guid tenantId,
        string? payrollTypeCode,
        DateOnly startDate,
        DateOnly endDate,
        int? maxRows,
        CancellationToken cancellationToken);

    // ── One-time incomes (REQ-006) ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes the application-mutating writes (register / annul the single application) of a one-time income
    /// with a transaction-scoped PostgreSQL advisory lock keyed on its public id, closing the check-then-act
    /// TOCTOU on the at-most-one-active-application rule (RF-011/RF-013, RN-06). Must run inside an open
    /// transaction (the handler opens one); the lock releases on commit/rollback. The default is a no-op (test
    /// fakes have no PostgreSQL); the EF repository takes the real advisory lock.
    /// </summary>
    Task AcquireOneTimeIncomeMutationLockAsync(Guid oneTimeIncomePublicId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Adds a one-time income (idioma post-fix, aclaración №3): stages the entity and returns the file's one-time
    /// incomes including the just-added (not-yet-saved) one, so the create handler can pick the created record for
    /// its response. The unit of work commits afterwards.
    /// </summary>
    Task<IReadOnlyCollection<OneTimeIncomeResponse>> AddOneTimeIncomeAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileOneTimeIncome entity,
        CancellationToken cancellationToken);

    /// <summary>Returns every one-time income of the personnel file (most recent income date first).</summary>
    Task<IReadOnlyCollection<OneTimeIncomeResponse>> GetOneTimeIncomesAsync(
        Guid personnelFilePublicId,
        CancellationToken cancellationToken);

    /// <summary>Returns a single one-time income by public id (or null when it is not on the file).</summary>
    Task<OneTimeIncomeResponse?> GetOneTimeIncomeAsync(
        Guid personnelFilePublicId,
        Guid oneTimeIncomePublicId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads the TRACKED one-time income aggregate (for a domain mutation): the write handlers apply the domain
    /// transition (pre-validated so the guards never throw) and the unit of work commits. Returns null when the
    /// income is not on the file / tenant.
    /// </summary>
    Task<PersonnelFileOneTimeIncome?> GetOneTimeIncomeEntityAsync(
        Guid personnelFilePublicId,
        Guid oneTimeIncomePublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the plaza + cost center of a one-time income (P-15): when <paramref name="assignedPositionPublicId"/>
    /// is supplied it must be an assignment of the employee; otherwise the principal plaza is resolved (same
    /// criterion as settlement/vacations — IsPrimary among the active ones, oldest StartDate, then oldest active,
    /// then oldest). The cost center is DERIVED from the resolved plaza; its name is snapshotted. Returns
    /// <see cref="OneTimeIncomePlazaResolution.Found"/> = false when no plaza can be resolved.
    /// </summary>
    Task<OneTimeIncomePlazaResolution> ResolveOneTimeIncomePlazaAsync(
        long personnelFileInternalId,
        Guid? assignedPositionPublicId,
        CancellationToken cancellationToken);

    /// <summary>True when the personnel file's employee profile is RETIRADO (write-locked; EMPLOYEE_PROFILE_RETIRED_LOCKED).</summary>
    Task<bool> IsOneTimeIncomeProfileRetiredAsync(
        long personnelFileInternalId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Requester lookup for the trío (№10) + the TRIPLE anti-self pata (c) (№6): the display name, activity and
    /// linked login of a personnel file of the company by public id. Returns null when the requester file is not
    /// found in the tenant.
    /// </summary>
    Task<OneTimeIncomeRequesterLookup?> GetOneTimeIncomeRequesterLookupAsync(
        Guid requesterFilePublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    // ── One-time-income applications (REQ-006 PR-4) ─────────────────────────────────────────────────

    /// <summary>
    /// Loads the TRACKED one-time income WITH its applications (for a unitary application / annulment under the
    /// advisory lock). Scoped to the tenant + income public id; returns null when it is not found. Do NOT pre-load
    /// the income tracked before the lock — call this as the FIRST tracking load so it reflects any committed
    /// concurrent application.
    /// </summary>
    Task<PersonnelFileOneTimeIncome?> GetTrackedOneTimeIncomeWithApplicationsAsync(
        Guid oneTimeIncomePublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>The application history of a one-time income (APLICADA + ANULADA, most recent activity first);
    /// null when the income is not on the file.</summary>
    Task<IReadOnlyCollection<OneTimeIncomeApplicationResponse>?> GetOneTimeIncomeApplicationsAsync(
        Guid personnelFilePublicId,
        Guid oneTimeIncomePublicId,
        CancellationToken cancellationToken);

    /// <summary>Resolves a company payroll-period instance for an application imputation (§0.13, FK real); null
    /// when it is not a period of the tenant.</summary>
    Task<OneTimeIncomePayrollPeriodResolution?> ResolveOneTimeIncomePayrollPeriodAsync(
        Guid tenantId,
        Guid payrollPeriodPublicId,
        CancellationToken cancellationToken);

    /// <summary>An AsNoTracking snapshot of the tenant's AUTORIZADO one-time incomes of a payroll type without an
    /// active application, ordered by internal id (anti-deadlock ordering for the apply-period batch).</summary>
    Task<IReadOnlyList<OneTimeIncomeBatchCandidate>> GetOneTimeIncomeApplyPeriodCandidatesAsync(
        Guid tenantId,
        string payrollTypeCode,
        CancellationToken cancellationToken);

    /// <summary>The tenant's AUTORIZADO one-time incomes without an active application for the pending/overdue tray
    /// (optionally filtered by payroll type), with the employee identity and value/destination snapshot.</summary>
    Task<IReadOnlyList<OneTimeIncomePendingData>> GetOneTimeIncomePendingRowsAsync(
        Guid tenantId,
        string? payrollTypeCode,
        CancellationToken cancellationToken);

    // ── One-time-income bandeja + exports (REQ-006 PR-5) ──────────────────────────────────────────────

    /// <summary>The company-wide one-time-income bandeja page (RF-008): paginated + filtered items, per-status
    /// counts (over the full non-status filter, so every status is represented), the amount totals BY CURRENCY of
    /// the filtered set (RN-13) and — when a group dimension is supplied — the aggregation buckets (composite key
    /// (dimension, currency), so they cuadran against the flat totals of the same filter, №14).</summary>
    Task<OneTimeIncomeBandejaResponse> QueryOneTimeIncomesAsync(
        QueryOneTimeIncomesQuery query,
        OneTimeIncomeGroupDimension? groupBy,
        CancellationToken cancellationToken);

    /// <summary>The one-time-income bandeja export rows (same filters as the bandeja; capped at
    /// <c>MaxRows + 1</c> so the caller can detect the synchronous-limit overflow → 413).</summary>
    Task<IReadOnlyCollection<IngresoEventualExportRow>> GetOneTimeIncomeExportRowsAsync(
        ExportOneTimeIncomesQuery query,
        CancellationToken cancellationToken);

    /// <summary>The pending / overdue tray export rows (RF-012): the AUTORIZADO one-time incomes without an active
    /// application (optionally filtered by payroll type / only the overdue), each marked overdue against
    /// <paramref name="today"/>. Capped at <c>MaxRows + 1</c> for the 413 overflow.</summary>
    Task<IReadOnlyCollection<IngresoEventualPendienteExportRow>> GetOneTimeIncomePendingExportRowsAsync(
        Guid tenantId,
        string? payrollTypeCode,
        bool onlyOverdue,
        DateOnly today,
        int? maxRows,
        CancellationToken cancellationToken);

    /// <summary>The payroll-input rows (§5): the pending (AUTORIZADO, active, unapplied) one-time incomes of a
    /// MANDATORY payroll type + period (matched against the declared period label). Cuadra EXACTLY against the
    /// pending tray of the same filter (excludes annulled and applied). Capped at <c>MaxRows + 1</c> for the 413.</summary>
    Task<IReadOnlyCollection<InsumoPlanillaEventualExportRow>> GetOneTimeIncomePayrollInputRowsAsync(
        Guid tenantId,
        string payrollTypeCode,
        string payrollPeriod,
        int? maxRows,
        CancellationToken cancellationToken);

    // ── Overtime records (REQ-007) ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes the application-mutating writes (register / annul the single application, per-period batch) of a
    /// single overtime record with a transaction-scoped PostgreSQL advisory lock keyed on its public id, closing
    /// the check-then-act TOCTOU on the at-most-one-active-application rule (RF-011/RF-013, RN-06/№11). Must run
    /// inside an open transaction (the handler opens one); the lock releases on commit/rollback. The default is a
    /// no-op (test fakes have no PostgreSQL); the EF repository takes the real advisory lock.
    /// </summary>
    Task AcquireOvertimeRecordMutationLockAsync(Guid overtimeRecordPublicId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>Adds a new overtime record and returns the file's records (idioma post-fix: Add + AsNoTracking
    /// re-query + Append the in-memory entity; the handler owns the SaveChanges).</summary>
    Task<IReadOnlyCollection<OvertimeRecordResponse>> AddOvertimeRecordAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileOvertimeRecord entity,
        CancellationToken cancellationToken);

    /// <summary>The overtime records of a file (most recent work date first) for the list read.</summary>
    Task<IReadOnlyCollection<OvertimeRecordResponse>> GetOvertimeRecordsAsync(
        Guid personnelFilePublicId,
        CancellationToken cancellationToken);

    /// <summary>A single overtime record projection (null when it is not on the file).</summary>
    Task<OvertimeRecordResponse?> GetOvertimeRecordAsync(
        Guid personnelFilePublicId,
        Guid overtimeRecordPublicId,
        CancellationToken cancellationToken);

    /// <summary>The TRACKED overtime record (for a lifecycle mutation); null when it is not found.</summary>
    Task<PersonnelFileOvertimeRecord?> GetOvertimeRecordEntityAsync(
        Guid personnelFilePublicId,
        Guid overtimeRecordPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>Resolves the plaza of an overtime record (D-12): the requested assignment, or the principal plaza
    /// when omitted; <see cref="OvertimePlazaResolution.NotFound"/> when the employee has no matching assignment.</summary>
    Task<OvertimePlazaResolution> ResolveOvertimePlazaAsync(
        long personnelFileInternalId,
        Guid? assignedPositionPublicId,
        CancellationToken cancellationToken);

    /// <summary>Resolves a company overtime-type master by public id (code + name + reference factor + activity);
    /// null when it is not a type of the tenant.</summary>
    Task<OvertimeTypeLookup?> GetOvertimeTypeLookupAsync(
        Guid overtimeTypePublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>Resolves a company overtime justification-type master by public id (code + name + activity); null
    /// when it is not a type of the tenant.</summary>
    Task<OvertimeJustificationLookup?> GetOvertimeJustificationLookupAsync(
        Guid justificationTypePublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>Resolves the requester file's display name + activity + linked login (the trío snapshot + the
    /// TRIPLE anti-self pata c); null when it is not a personnel file of the tenant.</summary>
    Task<OvertimeRequesterLookup?> GetOvertimeRequesterLookupAsync(
        Guid requesterFilePublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>The sum of the day's active overtime minutes (EN_REVISION + AUTORIZADA + APLICADA) for the file +
    /// work date, optionally excluding a record being edited — feeds the daily-cap accumulation (P-05/№12).</summary>
    Task<int> GetActiveOvertimeMinutesForDayAsync(
        long personnelFileInternalId,
        DateOnly workDate,
        Guid tenantId,
        Guid? excludeRecordPublicId,
        CancellationToken cancellationToken);

    /// <summary>True when the file's employment profile is RETIRADO (a retired file is locked for new records).</summary>
    Task<bool> IsOvertimeProfileRetiredAsync(
        long personnelFileInternalId,
        CancellationToken cancellationToken);

    // ── Overtime-record applications (REQ-007 PR-4) ─────────────────────────────────────────────────

    /// <summary>
    /// Loads the TRACKED overtime record WITH its applications (for a unitary application / annulment under the
    /// advisory lock). Scoped to the tenant + record public id; returns null when it is not found. Do NOT pre-load
    /// the record tracked before the lock — call this as the FIRST tracking load so it reflects any committed
    /// concurrent application.
    /// </summary>
    Task<PersonnelFileOvertimeRecord?> GetTrackedOvertimeRecordWithApplicationsAsync(
        Guid overtimeRecordPublicId,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>The application history of an overtime record (APLICADA + ANULADA, most recent activity first);
    /// null when the record is not on the file.</summary>
    Task<IReadOnlyCollection<OvertimeRecordApplicationResponse>?> GetOvertimeRecordApplicationsAsync(
        Guid personnelFilePublicId,
        Guid overtimeRecordPublicId,
        CancellationToken cancellationToken);

    /// <summary>Resolves a company payroll-period instance for an application imputation (§0.14, FK real); null
    /// when it is not a period of the tenant.</summary>
    Task<OvertimePayrollPeriodResolution?> ResolveOvertimePayrollPeriodAsync(
        Guid tenantId,
        Guid payrollPeriodPublicId,
        CancellationToken cancellationToken);

    /// <summary>An AsNoTracking snapshot of the tenant's AUTORIZADA overtime records of a payroll type whose work
    /// date has elapsed (future organized shifts excluded) without an active application, ordered by internal id
    /// (anti-deadlock ordering for the apply-period batch).</summary>
    Task<IReadOnlyList<OvertimeApplyPeriodCandidate>> GetOvertimeApplyPeriodCandidatesAsync(
        Guid tenantId,
        string payrollTypeCode,
        DateOnly today,
        CancellationToken cancellationToken);

    /// <summary>The tenant's AUTORIZADA overtime records without an active application for the pending/overdue tray
    /// (optionally filtered by payroll type), with the employee identity and shift/destination snapshot.</summary>
    Task<IReadOnlyList<OvertimePendingData>> GetOvertimePendingRowsAsync(
        Guid tenantId,
        string? payrollTypeCode,
        CancellationToken cancellationToken);

    // ── Overtime bandeja + exports (REQ-007 PR-5) ──────────────────────────────────────────────────────

    /// <summary>The company-wide overtime bandeja page (RF-011): paginated + filtered items, per-status counts
    /// (over the full non-status filter, so every status is represented), the global total decimal HOURS of the
    /// filtered set and the totals-by-type buckets (a <c>GroupBy</c> over <c>duration_decimal_hours</c>). The totals
    /// cuadran: Σ <c>TotalsByType.TotalHours</c> == <c>TotalHours</c> (§0.16 — hours, not money).</summary>
    Task<OvertimeRecordBandejaResponse> QueryOvertimeRecordsAsync(
        QueryOvertimeRecordsQuery query,
        CancellationToken cancellationToken);

    /// <summary>The overtime bandeja export rows (same filters as the bandeja; capped at <c>MaxRows + 1</c> so the
    /// caller can detect the synchronous-limit overflow → 413).</summary>
    Task<IReadOnlyCollection<HoraExtraExportRow>> GetOvertimeRecordExportRowsAsync(
        ExportOvertimeRecordsQuery query,
        CancellationToken cancellationToken);

    /// <summary>The pending / overdue tray export rows (RF-012): the AUTORIZADA overtime records without an active
    /// application (optionally filtered by payroll type / only the overdue), each marked overdue against
    /// <paramref name="today"/>. Capped at <c>MaxRows + 1</c> for the 413 overflow.</summary>
    Task<IReadOnlyCollection<HoraExtraPendienteExportRow>> GetOvertimeRecordPendingExportRowsAsync(
        Guid tenantId,
        string? payrollTypeCode,
        bool onlyOverdue,
        DateOnly today,
        int? maxRows,
        CancellationToken cancellationToken);

    /// <summary>The payroll-input rows (§0.16): the pending (AUTORIZADA, active, unapplied, elapsed, NOT compensated)
    /// overtime records of a MANDATORY payroll type + period, with the cost center derived from the plaza (join to
    /// the employment assignment, D-12). Cuadra against the pending tray of the same filter (excludes annulled +
    /// applied + compensated + future). Capped at <c>MaxRows + 1</c> for the 413.</summary>
    Task<IReadOnlyCollection<InsumoPlanillaHoraExtraExportRow>> GetOvertimeRecordPayrollInputRowsAsync(
        Guid tenantId,
        string payrollTypeCode,
        string payrollPeriod,
        DateOnly today,
        int? maxRows,
        CancellationToken cancellationToken);

    // ── Overtime ↔ settlement hooks (REQ-007 PR-6, §0.15) ───────────────────────────────────────────────

    /// <summary>The plaza's AUTORIZADA overtime records (tracked, active, NOT compensated — RF-013) that a settlement
    /// closes when its HORAS_EXTRAS_PENDIENTES_PAGO line stays included: the caller splits them by work date — the
    /// elapsed ones become APLICADA and the future organized shifts are annulled (§0.15/№13/№15); empty when none.</summary>
    Task<IReadOnlyCollection<PersonnelFileOvertimeRecord>> GetOvertimeRecordsForSettlementAsync(
        long personnelFileId,
        Guid assignedPositionPublicId,
        CancellationToken cancellationToken);

    /// <summary>The overtime records a specific settlement closed (tracked): the ones it APPLIED and the FUTURE ones it
    /// ANNULLED (stamped with the settlement id, regardless of is_active), to reopen when that settlement is annulled
    /// (ReopenFromSettlement, §0.15); empty when none.</summary>
    Task<IReadOnlyCollection<PersonnelFileOvertimeRecord>> GetOvertimeRecordsAppliedOrAnnulledBySettlementAsync(
        long personnelFileId,
        Guid settlementPublicId,
        CancellationToken cancellationToken);
}
