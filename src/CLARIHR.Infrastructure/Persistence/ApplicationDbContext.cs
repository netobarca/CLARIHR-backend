using System.Reflection;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Domain.Auditing;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Banks;
using CLARIHR.Domain.CatalogTypes;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.Compliance;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.GeneralCatalogs;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.InternalCatalogs;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Domain.OrgUnits;
using CLARIHR.Domain.EducationCatalogs;
using CLARIHR.Domain.DocumentTypeCatalogs;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.Platform;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Domain.Preferences;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.Reports;
using CLARIHR.Domain.SalaryTabulator;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider)
    : DbContext(options), IApplicationDbContext
{
    private Guid? CurrentTenantId => tenantContext.TenantId;

    private bool HasTenantScope => CurrentTenantId.HasValue;

    private Guid CurrentTenantIdOrDefault => CurrentTenantId ?? Guid.Empty;

    public DbSet<User> AuthUsers => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

    public DbSet<UserSocialLink> UserSocialLinks => Set<UserSocialLink>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CompanyPreference> CompanyPreferences => Set<CompanyPreference>();

    public DbSet<CompanyLegalProfile> CompanyLegalProfiles => Set<CompanyLegalProfile>();

    public DbSet<CommercialAddon> CommercialAddons => Set<CommercialAddon>();

    public DbSet<CommercialAddonEntitlement> CommercialAddonEntitlements => Set<CommercialAddonEntitlement>();

    public DbSet<CommercialPlan> CommercialPlans => Set<CommercialPlan>();

    public DbSet<CommercialPlanVersion> CommercialPlanVersions => Set<CommercialPlanVersion>();

    public DbSet<CommercialPlanLimit> CommercialPlanLimits => Set<CommercialPlanLimit>();

    public DbSet<CompanySubscription> CompanySubscriptions => Set<CompanySubscription>();

    public DbSet<CompanySubscriptionStatusTransition> CompanySubscriptionStatusTransitions => Set<CompanySubscriptionStatusTransition>();

    public DbSet<CompanySubscriptionStatusChangeRequest> CompanySubscriptionStatusChangeRequests => Set<CompanySubscriptionStatusChangeRequest>();

    public DbSet<CompanySubscriptionPlanChange> CompanySubscriptionPlanChanges => Set<CompanySubscriptionPlanChange>();

    public DbSet<CompanyCommercialAddon> CompanyCommercialAddons => Set<CompanyCommercialAddon>();

    public DbSet<CompanyCommercialAddonChange> CompanyCommercialAddonChanges => Set<CompanyCommercialAddonChange>();

    public DbSet<UserCompanyMembership> UserCompanyMemberships => Set<UserCompanyMembership>();

    public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();

    public DbSet<InvitationToken> InvitationTokens => Set<InvitationToken>();

    public DbSet<IamUser> IamUsers => Set<IamUser>();

    public DbSet<IamRole> IamRoles => Set<IamRole>();

    public DbSet<IamPermission> IamPermissions => Set<IamPermission>();

    public DbSet<IamUserRoleAssignment> IamUserRoleAssignments => Set<IamUserRoleAssignment>();

    public DbSet<IamRolePermissionAssignment> IamRolePermissionAssignments => Set<IamRolePermissionAssignment>();

    public DbSet<RoleFieldPermission> RoleFieldPermissions => Set<RoleFieldPermission>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<PlatformOperator> PlatformOperators => Set<PlatformOperator>();

    public DbSet<PlatformAuditLog> PlatformAuditLogs => Set<PlatformAuditLog>();

    public DbSet<LocationHierarchyConfig> LocationHierarchyConfigs => Set<LocationHierarchyConfig>();

    public DbSet<LocationLevel> LocationLevels => Set<LocationLevel>();

    public DbSet<LocationGroup> LocationGroups => Set<LocationGroup>();

    public DbSet<CountryCatalogItem> CountryCatalogItems => Set<CountryCatalogItem>();

    public DbSet<BankCatalogItem> BankCatalogItems => Set<BankCatalogItem>();

    public DbSet<InternalCatalogValue> InternalCatalogValues => Set<InternalCatalogValue>();

    public DbSet<WorkCenterType> WorkCenterTypes => Set<WorkCenterType>();

    public DbSet<WorkCenter> WorkCenters => Set<WorkCenter>();

    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();

    public DbSet<CompanyTypeCatalogItem> CompanyTypeCatalogItems => Set<CompanyTypeCatalogItem>();

    public DbSet<OrgUnitTypeCatalogItem> OrgUnitTypeCatalogItems => Set<OrgUnitTypeCatalogItem>();

    public DbSet<FunctionalAreaCatalogItem> FunctionalAreaCatalogItems => Set<FunctionalAreaCatalogItem>();

    public DbSet<JobProfile> JobProfiles => Set<JobProfile>();

    public DbSet<JobCatalogItem> JobCatalogItems => Set<JobCatalogItem>();

    public DbSet<JobProfileRequirement> JobProfileRequirements => Set<JobProfileRequirement>();

    public DbSet<JobProfileFunction> JobProfileFunctions => Set<JobProfileFunction>();

    public DbSet<JobProfileRelation> JobProfileRelations => Set<JobProfileRelation>();

    public DbSet<JobProfileCompetency> JobProfileCompetencies => Set<JobProfileCompetency>();

    public DbSet<JobProfileTraining> JobProfileTrainings => Set<JobProfileTraining>();

    public DbSet<JobProfileBenefit> JobProfileBenefits => Set<JobProfileBenefit>();

    public DbSet<JobProfileWorkingCondition> JobProfileWorkingConditions => Set<JobProfileWorkingCondition>();

    public DbSet<JobProfileDependentPosition> JobProfileDependentPositions => Set<JobProfileDependentPosition>();

    public DbSet<JobProfileCompensation> JobProfileCompensations => Set<JobProfileCompensation>();

    public DbSet<PositionSlot> PositionSlots => Set<PositionSlot>();

    public DbSet<PositionDescriptionCatalogItem> PositionDescriptionCatalogItems => Set<PositionDescriptionCatalogItem>();

    public DbSet<PositionCategoryClassification> PositionCategoryClassifications => Set<PositionCategoryClassification>();

    public DbSet<PositionCategory> PositionCategories => Set<PositionCategory>();

    public DbSet<CostCenterType> CostCenterTypes => Set<CostCenterType>();

    public DbSet<CostCenter> CostCenters => Set<CostCenter>();

    public DbSet<LegalRepresentative> LegalRepresentatives => Set<LegalRepresentative>();

    public DbSet<LegalRepresentativePositionTitleCatalogItem> LegalRepresentativePositionTitleCatalogItems => Set<LegalRepresentativePositionTitleCatalogItem>();

    public DbSet<LegalRepresentativeRepresentationTypeCatalogItem> LegalRepresentativeRepresentationTypeCatalogItems => Set<LegalRepresentativeRepresentationTypeCatalogItem>();

    public DbSet<PersonnelFile> PersonnelFiles => Set<PersonnelFile>();

    public DbSet<PersonnelFileIdentification> PersonnelFileIdentifications => Set<PersonnelFileIdentification>();

    public DbSet<PersonnelFileAddress> PersonnelFileAddresses => Set<PersonnelFileAddress>();

    public DbSet<PersonnelFileEmergencyContact> PersonnelFileEmergencyContacts => Set<PersonnelFileEmergencyContact>();

    public DbSet<PersonnelFileFamilyMember> PersonnelFileFamilyMembers => Set<PersonnelFileFamilyMember>();

    public DbSet<PersonnelFileHobby> PersonnelFileHobbies => Set<PersonnelFileHobby>();

    public DbSet<PersonnelFileEmployeeRelation> PersonnelFileEmployeeRelations => Set<PersonnelFileEmployeeRelation>();

    public DbSet<PersonnelFileBankAccount> PersonnelFileBankAccounts => Set<PersonnelFileBankAccount>();

    public DbSet<PersonnelFileAssociation> PersonnelFileAssociations => Set<PersonnelFileAssociation>();

    public DbSet<PersonnelFileEducation> PersonnelFileEducations => Set<PersonnelFileEducation>();

    public DbSet<PersonnelFileLanguage> PersonnelFileLanguages => Set<PersonnelFileLanguage>();

    public DbSet<PersonnelFileTraining> PersonnelFileTrainings => Set<PersonnelFileTraining>();

    public DbSet<PersonnelFilePreviousEmployment> PersonnelFilePreviousEmployments => Set<PersonnelFilePreviousEmployment>();

    public DbSet<PersonnelFileReference> PersonnelFileReferences => Set<PersonnelFileReference>();

    public DbSet<PersonnelFileDocument> PersonnelFileDocuments => Set<PersonnelFileDocument>();

    public DbSet<MedicalClaimDocument> MedicalClaimDocuments => Set<MedicalClaimDocument>();


    public DbSet<PersonnelFileObservation> PersonnelFileObservations => Set<PersonnelFileObservation>();

    // Education system-wide catalog DbSets (global, no country scope)
    public DbSet<CLARIHR.Domain.EducationCatalogs.EducationStatusCatalogItem> EducationStatusCatalogItems => Set<CLARIHR.Domain.EducationCatalogs.EducationStatusCatalogItem>();

    public DbSet<CLARIHR.Domain.EducationCatalogs.EducationStudyTypeCatalogItem> EducationStudyTypeCatalogItems => Set<CLARIHR.Domain.EducationCatalogs.EducationStudyTypeCatalogItem>();

    public DbSet<CLARIHR.Domain.EducationCatalogs.EducationLevelCatalogItem> EducationLevelCatalogItems => Set<CLARIHR.Domain.EducationCatalogs.EducationLevelCatalogItem>();

    // Careers are COUNTRY-scoped since RF-009/DP-06 (kept in this block for discoverability).
    public DbSet<CLARIHR.Domain.EducationCatalogs.EducationCareerCatalogItem> EducationCareerCatalogItems => Set<CLARIHR.Domain.EducationCatalogs.EducationCareerCatalogItem>();

    public DbSet<CLARIHR.Domain.EducationCatalogs.EducationShiftCatalogItem> EducationShiftCatalogItems => Set<CLARIHR.Domain.EducationCatalogs.EducationShiftCatalogItem>();

    public DbSet<CLARIHR.Domain.EducationCatalogs.EducationModalityCatalogItem> EducationModalityCatalogItems => Set<CLARIHR.Domain.EducationCatalogs.EducationModalityCatalogItem>();

    public DbSet<LanguageCatalogItem> LanguageCatalogItems => Set<LanguageCatalogItem>();

    public DbSet<LanguageLevelCatalogItem> LanguageLevelCatalogItems => Set<LanguageLevelCatalogItem>();

    public DbSet<TrainingTypeCatalogItem> TrainingTypeCatalogItems => Set<TrainingTypeCatalogItem>();

    public DbSet<AssignmentTypeCatalogItem> AssignmentTypeCatalogItems => Set<AssignmentTypeCatalogItem>();

    public DbSet<SubstitutionTypeCatalogItem> SubstitutionTypeCatalogItems => Set<SubstitutionTypeCatalogItem>();

    public DbSet<MedicalClaimTypeCatalogItem> MedicalClaimTypeCatalogItems => Set<MedicalClaimTypeCatalogItem>();

    public DbSet<MedicalClaimStatusCatalogItem> MedicalClaimStatusCatalogItems => Set<MedicalClaimStatusCatalogItem>();

    public DbSet<AssetAccessTypeCatalogItem> AssetAccessTypeCatalogItems => Set<AssetAccessTypeCatalogItem>();

    public DbSet<BankAccountTypeCatalogItem> BankAccountTypeCatalogItems => Set<BankAccountTypeCatalogItem>();

    public DbSet<OffPayrollTransactionTypeCatalogItem> OffPayrollTransactionTypeCatalogItems => Set<OffPayrollTransactionTypeCatalogItem>();

    public DbSet<EconomicAidTypeCatalogItem> EconomicAidTypeCatalogItems => Set<EconomicAidTypeCatalogItem>();

    public DbSet<EconomicAidStatusCatalogItem> EconomicAidStatusCatalogItems => Set<EconomicAidStatusCatalogItem>();

    public DbSet<CertificateTypeCatalogItem> CertificateTypeCatalogItems => Set<CertificateTypeCatalogItem>();

    public DbSet<CertificateRequestStatusCatalogItem> CertificateRequestStatusCatalogItems => Set<CertificateRequestStatusCatalogItem>();

    public DbSet<RetirementRequestStatusCatalogItem> RetirementRequestStatusCatalogItems => Set<RetirementRequestStatusCatalogItem>();

    public DbSet<SettlementStatusCatalogItem> SettlementStatusCatalogItems => Set<SettlementStatusCatalogItem>();

    public DbSet<SettlementConceptCatalogItem> SettlementConceptCatalogItems => Set<SettlementConceptCatalogItem>();

    public DbSet<CertificateDeliveryMethodCatalogItem> CertificateDeliveryMethodCatalogItems => Set<CertificateDeliveryMethodCatalogItem>();

    public DbSet<CertificatePurposeCatalogItem> CertificatePurposeCatalogItems => Set<CertificatePurposeCatalogItem>();

    public DbSet<ClinicSectorCatalogItem> ClinicSectorCatalogItems => Set<ClinicSectorCatalogItem>();

    public DbSet<IncapacityStatusCatalogItem> IncapacityStatusCatalogItems => Set<IncapacityStatusCatalogItem>();

    public DbSet<VacationRequestStatusCatalogItem> VacationRequestStatusCatalogItems => Set<VacationRequestStatusCatalogItem>();

    public DbSet<CompensatoryTimeStatusCatalogItem> CompensatoryTimeStatusCatalogItems => Set<CompensatoryTimeStatusCatalogItem>();

    public DbSet<CompensatoryTimeOperationCatalogItem> CompensatoryTimeOperationCatalogItems => Set<CompensatoryTimeOperationCatalogItem>();

    public DbSet<PersonnelTransactionStatusCatalogItem> PersonnelTransactionStatusCatalogItems => Set<PersonnelTransactionStatusCatalogItem>();

    public DbSet<AgeRangeCatalogItem> AgeRangeCatalogItems => Set<AgeRangeCatalogItem>();

    public DbSet<SeniorityRangeCatalogItem> SeniorityRangeCatalogItems => Set<SeniorityRangeCatalogItem>();

    public DbSet<DeliveryStatusCatalogItem> DeliveryStatusCatalogItems => Set<DeliveryStatusCatalogItem>();

    public DbSet<PaymentMethodCatalogItem> PaymentMethodCatalogItems => Set<PaymentMethodCatalogItem>();

    public DbSet<EmploymentStatusCatalogItem> EmploymentStatusCatalogItems => Set<EmploymentStatusCatalogItem>();

    public DbSet<DurationUnitCatalogItem> DurationUnitCatalogItems => Set<DurationUnitCatalogItem>();

    public DbSet<ExperienceMetricCatalogItem> ExperienceMetricCatalogItems => Set<ExperienceMetricCatalogItem>();

    public DbSet<ReferenceTypeCatalogItem> ReferenceTypeCatalogItems => Set<ReferenceTypeCatalogItem>();

    public DbSet<CurrencyCatalogItem> CurrencyCatalogItems => Set<CurrencyCatalogItem>();

    public DbSet<ContractTypeCatalogItem> ContractTypeCatalogItems => Set<ContractTypeCatalogItem>();

    public DbSet<ActionTypeCatalogItem> ActionTypeCatalogItems => Set<ActionTypeCatalogItem>();

    public DbSet<ActionStatusCatalogItem> ActionStatusCatalogItems => Set<ActionStatusCatalogItem>();

    public DbSet<PayrollTypeCatalogItem> PayrollTypeCatalogItems => Set<PayrollTypeCatalogItem>();

    public DbSet<RecurringIncomeStatusCatalogItem> RecurringIncomeStatusCatalogItems => Set<RecurringIncomeStatusCatalogItem>();

    public DbSet<RecurringIncomeSettlementActionCatalogItem> RecurringIncomeSettlementActionCatalogItems => Set<RecurringIncomeSettlementActionCatalogItem>();

    public DbSet<RecurringIncomeTypeCatalogItem> RecurringIncomeTypeCatalogItems => Set<RecurringIncomeTypeCatalogItem>();

    public DbSet<OneTimeDeductionStatusCatalogItem> OneTimeDeductionStatusCatalogItems => Set<OneTimeDeductionStatusCatalogItem>();

    public DbSet<NotWorkedTimeStatusCatalogItem> NotWorkedTimeStatusCatalogItems => Set<NotWorkedTimeStatusCatalogItem>();

    public DbSet<RecurringDeductionStatusCatalogItem> RecurringDeductionStatusCatalogItems => Set<RecurringDeductionStatusCatalogItem>();

    public DbSet<RecurringDeductionSettlementActionCatalogItem> RecurringDeductionSettlementActionCatalogItems => Set<RecurringDeductionSettlementActionCatalogItem>();

    public DbSet<RecurringDeductionTypeCatalogItem> RecurringDeductionTypeCatalogItems => Set<RecurringDeductionTypeCatalogItem>();

    public DbSet<OneTimeIncomeStatusCatalogItem> OneTimeIncomeStatusCatalogItems => Set<OneTimeIncomeStatusCatalogItem>();

    public DbSet<OvertimeRecordStatusCatalogItem> OvertimeRecordStatusCatalogItems => Set<OvertimeRecordStatusCatalogItem>();

    public DbSet<PayrollRunStatusCatalogItem> PayrollRunStatusCatalogItems => Set<PayrollRunStatusCatalogItem>();

    public DbSet<PayrollPeriodStatusCatalogItem> PayrollPeriodStatusCatalogItems => Set<PayrollPeriodStatusCatalogItem>();

    public DbSet<HobbyCatalogItem> HobbyCatalogItems => Set<HobbyCatalogItem>();

    public DbSet<AssociationCatalogItem> AssociationCatalogItems => Set<AssociationCatalogItem>();

    public DbSet<AdditionalBenefitTypeCatalogItem> AdditionalBenefitTypeCatalogItems => Set<AdditionalBenefitTypeCatalogItem>();

    public DbSet<CLARIHR.Domain.Afps.AfpCatalogItem> AfpCatalogItems => Set<CLARIHR.Domain.Afps.AfpCatalogItem>();

    public DbSet<CLARIHR.Domain.Compensation.CompensationConceptTypeCatalogItem> CompensationConceptTypeCatalogItems => Set<CLARIHR.Domain.Compensation.CompensationConceptTypeCatalogItem>();

    public DbSet<PayPeriodCatalogItem> PayPeriodCatalogItems => Set<PayPeriodCatalogItem>();

    public DbSet<CalculationBaseCatalogItem> CalculationBaseCatalogItems => Set<CalculationBaseCatalogItem>();

    public DbSet<CLARIHR.Domain.Compensation.IncomeTaxWithholdingBracket> IncomeTaxWithholdingBrackets => Set<CLARIHR.Domain.Compensation.IncomeTaxWithholdingBracket>();

    public DbSet<CLARIHR.Domain.Compensation.IndebtednessLimit> IndebtednessLimits => Set<CLARIHR.Domain.Compensation.IndebtednessLimit>();

    public DbSet<IdentificationTypeCatalogItem> IdentificationTypeCatalogItems => Set<IdentificationTypeCatalogItem>();

    public DbSet<ProfessionCatalogItem> ProfessionCatalogItems => Set<ProfessionCatalogItem>();

    public DbSet<MaritalStatusCatalogItem> MaritalStatusCatalogItems => Set<MaritalStatusCatalogItem>();

    public DbSet<KinshipCatalogItem> KinshipCatalogItems => Set<KinshipCatalogItem>();

    public DbSet<DepartmentCatalogItem> DepartmentCatalogItems => Set<DepartmentCatalogItem>();

    public DbSet<MunicipalityCatalogItem> MunicipalityCatalogItems => Set<MunicipalityCatalogItem>();

    public DbSet<InsuranceTypeCatalogItem> InsuranceTypeCatalogItems => Set<InsuranceTypeCatalogItem>();

    public DbSet<InsuranceRangeCatalogItem> InsuranceRangeCatalogItems => Set<InsuranceRangeCatalogItem>();

    public DbSet<RetirementCategoryCatalogItem> RetirementCategoryCatalogItems => Set<RetirementCategoryCatalogItem>();

    public DbSet<RetirementReasonCatalogItem> RetirementReasonCatalogItems => Set<RetirementReasonCatalogItem>();

    public DbSet<PersonalTitleCatalogItem> PersonalTitleCatalogItems => Set<PersonalTitleCatalogItem>();

    public DbSet<AddressTypeCatalogItem> AddressTypeCatalogItems => Set<AddressTypeCatalogItem>();

    public DbSet<PersonnelFileEmployeeProfile> PersonnelFileEmployeeProfiles => Set<PersonnelFileEmployeeProfile>();

    public DbSet<ExitInterviewForm> ExitInterviewForms => Set<ExitInterviewForm>();

    public DbSet<ExitInterviewFormGroup> ExitInterviewFormGroups => Set<ExitInterviewFormGroup>();

    public DbSet<ExitInterviewFormField> ExitInterviewFormFields => Set<ExitInterviewFormField>();

    public DbSet<ExitInterviewFormFieldOption> ExitInterviewFormFieldOptions => Set<ExitInterviewFormFieldOption>();

    public DbSet<ExitInterviewSubmission> ExitInterviewSubmissions => Set<ExitInterviewSubmission>();

    public DbSet<ExitInterviewAnswer> ExitInterviewAnswers => Set<ExitInterviewAnswer>();

    public DbSet<PersonnelFileEmploymentAssignment> PersonnelFileEmploymentAssignments => Set<PersonnelFileEmploymentAssignment>();

    public DbSet<PersonnelFileContractHistory> PersonnelFileContractHistories => Set<PersonnelFileContractHistory>();

    public DbSet<PersonnelFileCompensationConcept> PersonnelFileCompensationConcepts => Set<PersonnelFileCompensationConcept>();

    public DbSet<PersonnelFileAdditionalBenefit> PersonnelFileAdditionalBenefits => Set<PersonnelFileAdditionalBenefit>();


    public DbSet<PersonnelFileAuthorizationSubstitution> PersonnelFileAuthorizationSubstitutions => Set<PersonnelFileAuthorizationSubstitution>();

    public DbSet<PersonnelFilePersonnelAction> PersonnelFilePersonnelActions => Set<PersonnelFilePersonnelAction>();

    public DbSet<PersonnelFilePayrollTransaction> PersonnelFilePayrollTransactions => Set<PersonnelFilePayrollTransaction>();

    public DbSet<PersonnelFileAssetAccess> PersonnelFileAssetAccesses => Set<PersonnelFileAssetAccess>();

    public DbSet<PersonnelFileInsurance> PersonnelFileInsurances => Set<PersonnelFileInsurance>();

    public DbSet<PersonnelFileInsuranceBeneficiary> PersonnelFileInsuranceBeneficiaries => Set<PersonnelFileInsuranceBeneficiary>();

    public DbSet<PersonnelFileMedicalClaim> PersonnelFileMedicalClaims => Set<PersonnelFileMedicalClaim>();

    public DbSet<PersonnelFileOffPayrollTransaction> PersonnelFileOffPayrollTransactions => Set<PersonnelFileOffPayrollTransaction>();

    public DbSet<OffPayrollTransactionDocument> OffPayrollTransactionDocuments => Set<OffPayrollTransactionDocument>();

    public DbSet<PersonnelFileEconomicAidRequest> PersonnelFileEconomicAidRequests => Set<PersonnelFileEconomicAidRequest>();

    public DbSet<EconomicAidRequestDocument> EconomicAidRequestDocuments => Set<EconomicAidRequestDocument>();

    public DbSet<PersonnelFileRetirementRequest> PersonnelFileRetirementRequests => Set<PersonnelFileRetirementRequest>();

    public DbSet<RetirementRequestClosedRecord> RetirementRequestClosedRecords => Set<RetirementRequestClosedRecord>();

    public DbSet<PersonnelFileSettlement> PersonnelFileSettlements => Set<PersonnelFileSettlement>();

    public DbSet<PersonnelFileSettlementLine> PersonnelFileSettlementLines => Set<PersonnelFileSettlementLine>();

    public DbSet<PersonnelFileIncapacity> PersonnelFileIncapacities => Set<PersonnelFileIncapacity>();

    public DbSet<PersonnelFileIncapacityDocument> PersonnelFileIncapacityDocuments => Set<PersonnelFileIncapacityDocument>();

    public DbSet<PersonnelFileLactationPeriod> PersonnelFileLactationPeriods => Set<PersonnelFileLactationPeriod>();

    public DbSet<LactationSchedule> LactationSchedules => Set<LactationSchedule>();

    public DbSet<PersonnelFileCompensatoryTimeCredit> PersonnelFileCompensatoryTimeCredits => Set<PersonnelFileCompensatoryTimeCredit>();

    public DbSet<PersonnelFileCompensatoryTimeCreditDocument> PersonnelFileCompensatoryTimeCreditDocuments => Set<PersonnelFileCompensatoryTimeCreditDocument>();

    public DbSet<PersonnelFileCompensatoryTimeAbsence> PersonnelFileCompensatoryTimeAbsences => Set<PersonnelFileCompensatoryTimeAbsence>();

    public DbSet<PersonnelFileRecurringIncome> PersonnelFileRecurringIncomes => Set<PersonnelFileRecurringIncome>();

    public DbSet<PersonnelFileRecurringIncomeInstallment> PersonnelFileRecurringIncomeInstallments => Set<PersonnelFileRecurringIncomeInstallment>();

    public DbSet<PersonnelFileOneTimeDeduction> PersonnelFileOneTimeDeductions => Set<PersonnelFileOneTimeDeduction>();

    public DbSet<PersonnelFileOneTimeDeductionApplication> PersonnelFileOneTimeDeductionApplications => Set<PersonnelFileOneTimeDeductionApplication>();

    public DbSet<PersonnelFileRecurringDeduction> PersonnelFileRecurringDeductions => Set<PersonnelFileRecurringDeduction>();

    public DbSet<PersonnelFileRecurringDeductionPlanSegment> PersonnelFileRecurringDeductionPlanSegments => Set<PersonnelFileRecurringDeductionPlanSegment>();

    public DbSet<PersonnelFileRecurringDeductionInstallment> PersonnelFileRecurringDeductionInstallments => Set<PersonnelFileRecurringDeductionInstallment>();

    public DbSet<PersonnelFileOneTimeIncome> PersonnelFileOneTimeIncomes => Set<PersonnelFileOneTimeIncome>();

    public DbSet<PersonnelFileOneTimeIncomeApplication> PersonnelFileOneTimeIncomeApplications => Set<PersonnelFileOneTimeIncomeApplication>();

    public DbSet<PersonnelFileOvertimeRecord> PersonnelFileOvertimeRecords => Set<PersonnelFileOvertimeRecord>();

    public DbSet<PersonnelFileOvertimeRecordApplication> PersonnelFileOvertimeRecordApplications => Set<PersonnelFileOvertimeRecordApplication>();

    public DbSet<PersonnelFileRecognition> PersonnelFileRecognitions => Set<PersonnelFileRecognition>();

    public DbSet<PersonnelFileRecognitionDocument> PersonnelFileRecognitionDocuments => Set<PersonnelFileRecognitionDocument>();

    public DbSet<PersonnelFileDisciplinaryAction> PersonnelFileDisciplinaryActions => Set<PersonnelFileDisciplinaryAction>();

    public DbSet<PersonnelFileDisciplinaryActionDocument> PersonnelFileDisciplinaryActionDocuments => Set<PersonnelFileDisciplinaryActionDocument>();

    public DbSet<PersonnelFileVacationPeriod> PersonnelFileVacationPeriods => Set<PersonnelFileVacationPeriod>();

    public DbSet<PersonnelFileVacationRequest> PersonnelFileVacationRequests => Set<PersonnelFileVacationRequest>();

    public DbSet<VacationRequestAllocation> VacationRequestAllocations => Set<VacationRequestAllocation>();

    public DbSet<VacationReturn> VacationReturns => Set<VacationReturn>();

    public DbSet<VacationPlan> VacationPlans => Set<VacationPlan>();

    public DbSet<VacationPlanLine> VacationPlanLines => Set<VacationPlanLine>();

    public DbSet<PersonnelFileCertificateRequest> PersonnelFileCertificateRequests => Set<PersonnelFileCertificateRequest>();

    public DbSet<CertificateRequestDocument> CertificateRequestDocuments => Set<CertificateRequestDocument>();

    public DbSet<CompanyCertificateSettings> CompanyCertificateSettings => Set<CompanyCertificateSettings>();

    public DbSet<PersonnelFilePerformanceEvaluation> PersonnelFilePerformanceEvaluations => Set<PersonnelFilePerformanceEvaluation>();

    public DbSet<PersonnelFilePositionCompetencyResult> PersonnelFilePositionCompetencyResults => Set<PersonnelFilePositionCompetencyResult>();

    public DbSet<PersonnelFileSelectionContest> PersonnelFileSelectionContests => Set<PersonnelFileSelectionContest>();

    public DbSet<PersonnelFileCurricularCompetency> PersonnelFileCurricularCompetencies => Set<PersonnelFileCurricularCompetency>();

    public DbSet<OccupationalPyramidLevel> OccupationalPyramidLevels => Set<OccupationalPyramidLevel>();

    public DbSet<CompetencyConduct> CompetencyConducts => Set<CompetencyConduct>();

    public DbSet<CompetencyConductBehavior> CompetencyConductBehaviors => Set<CompetencyConductBehavior>();

    public DbSet<JobProfileCompetencyExpectation> JobProfileCompetencyExpectations => Set<JobProfileCompetencyExpectation>();

    public DbSet<JobProfileCompetencyExpectationConduct> JobProfileCompetencyExpectationConducts => Set<JobProfileCompetencyExpectationConduct>();

    public DbSet<CompetencyRatingScale> CompetencyRatingScales => Set<CompetencyRatingScale>();

    public DbSet<CompetencyRatingScaleLevel> CompetencyRatingScaleLevels => Set<CompetencyRatingScaleLevel>();

    public DbSet<SalaryTabulatorLine> SalaryTabulatorLines => Set<SalaryTabulatorLine>();

    public DbSet<SalaryTabulatorChangeRequest> SalaryTabulatorChangeRequests => Set<SalaryTabulatorChangeRequest>();

    public DbSet<SalaryTabulatorChangeRequestItem> SalaryTabulatorChangeRequestItems => Set<SalaryTabulatorChangeRequestItem>();

    // Leave & incapacity masters (vacaciones e incapacidades module)
    public DbSet<MedicalClinic> MedicalClinics => Set<MedicalClinic>();

    public DbSet<IncapacityRisk> IncapacityRisks => Set<IncapacityRisk>();

    public DbSet<IncapacityRiskParameter> IncapacityRiskParameters => Set<IncapacityRiskParameter>();

    public DbSet<NotWorkedTimeType> NotWorkedTimeTypes => Set<NotWorkedTimeType>();

    public DbSet<CLARIHR.Domain.PersonnelFiles.PersonnelFileNotWorkedTime> PersonnelFileNotWorkedTimes => Set<CLARIHR.Domain.PersonnelFiles.PersonnelFileNotWorkedTime>();

    public DbSet<IncapacityType> IncapacityTypes => Set<IncapacityType>();

    public DbSet<CompensatoryTimeType> CompensatoryTimeTypes => Set<CompensatoryTimeType>();

    public DbSet<CompanyHoliday> CompanyHolidays => Set<CompanyHoliday>();

    public DbSet<PayrollPeriodDefinition> PayrollPeriodDefinitions => Set<PayrollPeriodDefinition>();

    public DbSet<ReportExportJob> ReportExportJobs => Set<ReportExportJob>();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    // Document type system-wide catalog DbSet (global, no country scope)
    public DbSet<DocumentTypeCatalogItem> DocumentTypeCatalogItems => Set<DocumentTypeCatalogItem>();

    // Job Profile catalog type registry (global, no tenant/country scope)
    public DbSet<CatalogTypeDescriptor> CatalogTypeDescriptors => Set<CatalogTypeDescriptor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        ApplyPublicIdConventions(modelBuilder);
        ApplyTenantFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditMetadata();

        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditMetadata()
    {
        var utcNow = dateTimeProvider.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.MarkCreated(utcNow);
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.MarkModified(utcNow);
            }
        }

        foreach (var entry in ChangeTracker.Entries<TenantEntity>().Where(static entry => entry.State == EntityState.Added))
        {
            if (entry.Entity.TenantId != Guid.Empty)
            {
                continue;
            }

            if (!CurrentTenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant-scoped writes require a tenant context.");
            }

            entry.Entity.SetTenantId(CurrentTenantId.Value);
        }
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        var setFilterMethod = typeof(ApplicationDbContext)
            .GetMethod(nameof(SetTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Tenant filter method could not be found.");

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            _ = setFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }
    }

    private static void ApplyPublicIdConventions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned() || !typeof(Entity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            if (entityType.FindProperty(nameof(Entity.PublicId)) is null)
            {
                continue;
            }

            var entityBuilder = modelBuilder.Entity(entityType.ClrType);
            entityBuilder.Property<Guid>(nameof(Entity.PublicId)).HasColumnName("public_id");

            var hasPublicIdIndex = entityType.GetIndexes().Any(index =>
                index.Properties.Count == 1 &&
                index.Properties[0].Name == nameof(Entity.PublicId));

            if (hasPublicIdIndex)
            {
                continue;
            }

            var tableName = entityType.GetTableName() ?? entityType.ClrType.Name.ToLowerInvariant();
            entityBuilder.HasIndex(nameof(Entity.PublicId))
                .IsUnique()
                .HasDatabaseName($"uq_{tableName}__public_id");
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScopedEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity => HasTenantScope && entity.TenantId == CurrentTenantIdOrDefault);
    }
}
