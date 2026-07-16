namespace CLARIHR.Application.Features.Audit.Common;

public static class AuditEventTypes
{
    public const string UserCreated = "USER_CREATED";
    public const string UserUpdated = "USER_UPDATED";
    public const string UserDeactivated = "USER_DEACTIVATED";
    public const string UserReactivated = "USER_REACTIVATED";
    public const string UserActivated = "USER_ACTIVATED";
    public const string UserInvited = "USER_INVITED";
    public const string UserInvitationReset = "USER_INVITATION_RESET";
    public const string RoleCreated = "ROLE_CREATED";
    public const string RoleUpdated = "ROLE_UPDATED";
    public const string RoleCloned = "ROLE_CLONED";
    public const string RoleDeleted = "ROLE_DELETED";
    public const string RoleResourcePermissionsUpdated = "ROLE_RESOURCE_PERMISSIONS_UPDATED";
    public const string RoleFieldPermissionsUpdated = "ROLE_FIELD_PERMISSIONS_UPDATED";
    public const string CompanyCreated = "COMPANY_CREATED";
    public const string CompanyUpdated = "COMPANY_UPDATED";
    public const string CompanyArchived = "COMPANY_ARCHIVED";
    public const string CompanyReactivated = "COMPANY_REACTIVATED";
    public const string ActiveCompanySwitched = "ACTIVE_COMPANY_SWITCHED";
    public const string CommercialAddonCreated = "COMMERCIAL_ADDON_CREATED";
    public const string CommercialAddonUpdated = "COMMERCIAL_ADDON_UPDATED";
    public const string CommercialAddonActivated = "COMMERCIAL_ADDON_ACTIVATED";
    public const string CommercialAddonInactivated = "COMMERCIAL_ADDON_INACTIVATED";
    public const string CommercialPlanCreated = "COMMERCIAL_PLAN_CREATED";
    public const string CommercialPlanUpdated = "COMMERCIAL_PLAN_UPDATED";
    public const string CommercialPlanActivated = "COMMERCIAL_PLAN_ACTIVATED";
    public const string CommercialPlanInactivated = "COMMERCIAL_PLAN_INACTIVATED";
    public const string BankCatalogItemCreated = "BANK_CATALOG_ITEM_CREATED";
    public const string BankCatalogItemUpdated = "BANK_CATALOG_ITEM_UPDATED";
    public const string BankCatalogItemActivated = "BANK_CATALOG_ITEM_ACTIVATED";
    public const string BankCatalogItemInactivated = "BANK_CATALOG_ITEM_INACTIVATED";
    public const string CompanySubscriptionActivated = "COMPANY_SUBSCRIPTION_ACTIVATED";
    public const string CompanySubscriptionScheduled = "COMPANY_SUBSCRIPTION_SCHEDULED";
    public const string CompanySubscriptionStatusChanged = "COMPANY_SUBSCRIPTION_STATUS_CHANGED";
    public const string CompanySubscriptionStatusChangeRequested = "COMPANY_SUBSCRIPTION_STATUS_CHANGE_REQUESTED";
    public const string CompanySubscriptionStatusChangeApplied = "COMPANY_SUBSCRIPTION_STATUS_CHANGE_APPLIED";
    public const string CompanySubscriptionStatusChangeRejected = "COMPANY_SUBSCRIPTION_STATUS_CHANGE_REJECTED";
    public const string CompanySubscriptionPromotionProcessed = "COMPANY_SUBSCRIPTION_PROMOTION_PROCESSED";
    public const string CompanySubscriptionExpirationProcessed = "COMPANY_SUBSCRIPTION_EXPIRATION_PROCESSED";
    public const string CompanySubscriptionPlanChangeRequested = "COMPANY_SUBSCRIPTION_PLAN_CHANGE_REQUESTED";
    public const string CompanySubscriptionPlanChangeApplied = "COMPANY_SUBSCRIPTION_PLAN_CHANGE_APPLIED";
    public const string CompanySubscriptionPlanChangeCancelled = "COMPANY_SUBSCRIPTION_PLAN_CHANGE_CANCELLED";
    public const string CompanySubscriptionPlanChangeRejected = "COMPANY_SUBSCRIPTION_PLAN_CHANGE_REJECTED";
    public const string CompanySubscriptionAddonChangeRequested = "COMPANY_SUBSCRIPTION_ADDON_CHANGE_REQUESTED";
    public const string CompanySubscriptionAddonChangeApplied = "COMPANY_SUBSCRIPTION_ADDON_CHANGE_APPLIED";
    public const string CompanySubscriptionAddonChangeCancelled = "COMPANY_SUBSCRIPTION_ADDON_CHANGE_CANCELLED";
    public const string CompanySubscriptionAddonChangeRejected = "COMPANY_SUBSCRIPTION_ADDON_CHANGE_REJECTED";
    public const string CompanyTypeCatalogItemCreated = "COMPANY_TYPE_CATALOG_ITEM_CREATED";
    public const string CompanyTypeCatalogItemUpdated = "COMPANY_TYPE_CATALOG_ITEM_UPDATED";
    public const string CompanyTypeCatalogItemActivated = "COMPANY_TYPE_CATALOG_ITEM_ACTIVATED";
    public const string CompanyTypeCatalogItemInactivated = "COMPANY_TYPE_CATALOG_ITEM_INACTIVATED";
    public const string OrgUnitCreated = "ORG_UNIT_CREATED";
    public const string OrgUnitUpdated = "ORG_UNIT_UPDATED";
    public const string OrgUnitMoved = "ORG_UNIT_MOVED";
    public const string OrgUnitActivated = "ORG_UNIT_ACTIVATED";
    public const string OrgUnitInactivated = "ORG_UNIT_INACTIVATED";
    public const string OrgUnitTypeCatalogItemCreated = "ORG_UNIT_TYPE_CATALOG_ITEM_CREATED";
    public const string OrgUnitTypeCatalogItemUpdated = "ORG_UNIT_TYPE_CATALOG_ITEM_UPDATED";
    public const string OrgUnitTypeCatalogItemActivated = "ORG_UNIT_TYPE_CATALOG_ITEM_ACTIVATED";
    public const string OrgUnitTypeCatalogItemInactivated = "ORG_UNIT_TYPE_CATALOG_ITEM_INACTIVATED";
    public const string FunctionalAreaCatalogItemCreated = "FUNCTIONAL_AREA_CATALOG_ITEM_CREATED";
    public const string FunctionalAreaCatalogItemUpdated = "FUNCTIONAL_AREA_CATALOG_ITEM_UPDATED";
    public const string FunctionalAreaCatalogItemActivated = "FUNCTIONAL_AREA_CATALOG_ITEM_ACTIVATED";
    public const string FunctionalAreaCatalogItemInactivated = "FUNCTIONAL_AREA_CATALOG_ITEM_INACTIVATED";
    public const string JobProfileCreated = "JOB_PROFILE_CREATED";
    public const string JobProfileUpdated = "JOB_PROFILE_UPDATED";
    public const string JobProfilePublished = "JOB_PROFILE_PUBLISHED";
    public const string JobProfileArchived = "JOB_PROFILE_ARCHIVED";
    public const string JobProfileCompensationCreated = "JOB_PROFILE_COMPENSATION_CREATED";
    public const string JobProfileCompensationUpdated = "JOB_PROFILE_COMPENSATION_UPDATED";
    public const string JobProfileCompensationDeleted = "JOB_PROFILE_COMPENSATION_DELETED";
    public const string JobCatalogItemCreated = "JOB_CATALOG_ITEM_CREATED";
    public const string JobCatalogItemUpdated = "JOB_CATALOG_ITEM_UPDATED";
    public const string InternalCatalogValueCreated = "INTERNAL_CATALOG_VALUE_CREATED";
    public const string PositionDescriptionCatalogItemCreated = "POSITION_DESCRIPTION_CATALOG_ITEM_CREATED";
    public const string PositionDescriptionCatalogItemUpdated = "POSITION_DESCRIPTION_CATALOG_ITEM_UPDATED";
    public const string PositionDescriptionCatalogItemActivated = "POSITION_DESCRIPTION_CATALOG_ITEM_ACTIVATED";
    public const string PositionDescriptionCatalogItemInactivated = "POSITION_DESCRIPTION_CATALOG_ITEM_INACTIVATED";
    public const string PositionCategoryClassificationCreated = "POSITION_CATEGORY_CLASSIFICATION_CREATED";
    public const string PositionCategoryClassificationUpdated = "POSITION_CATEGORY_CLASSIFICATION_UPDATED";
    public const string PositionCategoryClassificationActivated = "POSITION_CATEGORY_CLASSIFICATION_ACTIVATED";
    public const string PositionCategoryClassificationInactivated = "POSITION_CATEGORY_CLASSIFICATION_INACTIVATED";
    public const string PositionCategoryCreated = "POSITION_CATEGORY_CREATED";
    public const string PositionCategoryUpdated = "POSITION_CATEGORY_UPDATED";
    public const string PositionCategoryActivated = "POSITION_CATEGORY_ACTIVATED";
    public const string PositionCategoryInactivated = "POSITION_CATEGORY_INACTIVATED";
    public const string PositionSlotCreated = "POSITION_SLOT_CREATED";
    public const string PositionSlotUpdated = "POSITION_SLOT_UPDATED";
    public const string PositionSlotStatusChanged = "POSITION_SLOT_STATUS_CHANGED";
    public const string PositionSlotDependencyUpdated = "POSITION_SLOT_DEPENDENCY_UPDATED";
    public const string PositionSlotOccupancyChanged = "POSITION_SLOT_OCCUPANCY_CHANGED";
    public const string CostCenterCreated = "COST_CENTER_CREATED";
    public const string CostCenterUpdated = "COST_CENTER_UPDATED";
    public const string CostCenterActivated = "COST_CENTER_ACTIVATED";
    public const string CostCenterInactivated = "COST_CENTER_INACTIVATED";
    public const string CostCenterTypeCreated = "COST_CENTER_TYPE_CREATED";
    public const string CostCenterTypeUpdated = "COST_CENTER_TYPE_UPDATED";
    public const string CostCenterTypeActivated = "COST_CENTER_TYPE_ACTIVATED";
    public const string CostCenterTypeInactivated = "COST_CENTER_TYPE_INACTIVATED";
    public const string LegalRepresentativeCreated = "LEGAL_REPRESENTATIVE_CREATED";
    public const string LegalRepresentativeUpdated = "LEGAL_REPRESENTATIVE_UPDATED";
    public const string LegalRepresentativeActivated = "LEGAL_REPRESENTATIVE_ACTIVATED";
    public const string LegalRepresentativeInactivated = "LEGAL_REPRESENTATIVE_INACTIVATED";
    public const string LegalRepresentativeSetPrimary = "LEGAL_REPRESENTATIVE_SET_PRIMARY";
    public const string PersonnelFileCreated = "PERSONNEL_FILE_CREATED";
    public const string PersonnelFileUpdated = "PERSONNEL_FILE_UPDATED";
    public const string PersonnelFileCompleted = "PERSONNEL_FILE_COMPLETED";
    public const string PersonnelFileActivated = "PERSONNEL_FILE_ACTIVATED";
    public const string PersonnelFileInactivated = "PERSONNEL_FILE_INACTIVATED";
    public const string PersonnelFileDocumentUploaded = "PERSONNEL_FILE_DOCUMENT_UPLOADED";
    public const string PersonnelFileDocumentFileReplaced = "PERSONNEL_FILE_DOCUMENT_FILE_REPLACED";
    public const string PersonnelFileDocumentInactivated = "PERSONNEL_FILE_DOCUMENT_INACTIVATED";
    public const string PersonnelFileObservationAdded = "PERSONNEL_FILE_OBSERVATION_ADDED";
    public const string OccupationalPyramidLevelCreated = "OCCUPATIONAL_PYRAMID_LEVEL_CREATED";
    public const string OccupationalPyramidLevelUpdated = "OCCUPATIONAL_PYRAMID_LEVEL_UPDATED";
    public const string OccupationalPyramidLevelActivated = "OCCUPATIONAL_PYRAMID_LEVEL_ACTIVATED";
    public const string OccupationalPyramidLevelInactivated = "OCCUPATIONAL_PYRAMID_LEVEL_INACTIVATED";
    public const string CompetencyConductCreated = "COMPETENCY_CONDUCT_CREATED";
    public const string CompetencyConductUpdated = "COMPETENCY_CONDUCT_UPDATED";
    public const string CompetencyConductActivated = "COMPETENCY_CONDUCT_ACTIVATED";
    public const string CompetencyConductInactivated = "COMPETENCY_CONDUCT_INACTIVATED";
    public const string CompetencyBehaviorLinked = "COMPETENCY_BEHAVIOR_LINKED";
    public const string JobProfileCompetencyMatrixUpdated = "JOB_PROFILE_COMPETENCY_MATRIX_UPDATED";
    public const string CompetencyRatingScaleUpdated = "COMPETENCY_RATING_SCALE_UPDATED";
    public const string SalaryTabulatorRequestCreated = "SALARY_TABULATOR_REQUEST_CREATED";
    public const string SalaryTabulatorRequestUpdated = "SALARY_TABULATOR_REQUEST_UPDATED";
    public const string SalaryTabulatorRequestSubmitted = "SALARY_TABULATOR_REQUEST_SUBMITTED";
    public const string SalaryTabulatorRequestApproved = "SALARY_TABULATOR_REQUEST_APPROVED";
    public const string SalaryTabulatorRequestRejected = "SALARY_TABULATOR_REQUEST_REJECTED";
    public const string SalaryTabulatorRequestCanceled = "SALARY_TABULATOR_REQUEST_CANCELED";
    public const string SalaryTabulatorLineApplied = "SALARY_TABULATOR_LINE_APPLIED";
    public const string SalaryTabulatorLineInactivated = "SALARY_TABULATOR_LINE_INACTIVATED";
    public const string ReportExported = "REPORT_EXPORTED";
    public const string ReportPrinted = "REPORT_PRINTED";
    public const string LocationGroupCreated = "LOCATION_GROUP_CREATED";
    public const string LocationGroupUpdated = "LOCATION_GROUP_UPDATED";
    public const string LocationGroupMoved = "LOCATION_GROUP_MOVED";
    public const string LocationGroupActivated = "LOCATION_GROUP_ACTIVATED";
    public const string LocationGroupInactivated = "LOCATION_GROUP_INACTIVATED";
    public const string WorkCenterCreated = "WORK_CENTER_CREATED";
    public const string WorkCenterUpdated = "WORK_CENTER_UPDATED";
    public const string WorkCenterReassigned = "WORK_CENTER_REASSIGNED";
    public const string WorkCenterActivated = "WORK_CENTER_ACTIVATED";
    public const string WorkCenterInactivated = "WORK_CENTER_INACTIVATED";
    public const string WorkCenterTypeCreated = "WORK_CENTER_TYPE_CREATED";
    public const string WorkCenterTypeUpdated = "WORK_CENTER_TYPE_UPDATED";
    public const string WorkCenterTypeActivated = "WORK_CENTER_TYPE_ACTIVATED";
    public const string WorkCenterTypeInactivated = "WORK_CENTER_TYPE_INACTIVATED";
    public const string LocationHierarchyUpdated = "LOCATION_HIERARCHY_UPDATED";
    public const string LocationLevelCreated = "LOCATION_LEVEL_CREATED";
    public const string LocationLevelUpdated = "LOCATION_LEVEL_UPDATED";
    public const string LocationLevelActivated = "LOCATION_LEVEL_ACTIVATED";
    public const string LocationLevelInactivated = "LOCATION_LEVEL_INACTIVATED";

    // CP-C: company-level configuration change (currency / time zone). Audited like the other
    // tenant-scoped admin controllers. UserPreferences stays un-audited by design — it is self-scoped
    // and the audit log is tenant-scoped (see audit doc 23).
    public const string CompanyPreferencesUpdated = "COMPANY_PREFERENCES_UPDATED";

    // Authentication / session lifecycle (AU-3). Recorded to the NON-tenant-scoped platform audit log
    // (IPlatformAuditService) because these events happen before / outside a tenant context.
    public const string UserLoggedIn = "USER_LOGGED_IN";
    public const string UserLoginFailed = "USER_LOGIN_FAILED";
    public const string UserLoginThrottled = "USER_LOGIN_THROTTLED";
    public const string UserLoggedOut = "USER_LOGGED_OUT";
    public const string UserRegistered = "USER_REGISTERED";
    public const string UserEmailVerified = "USER_EMAIL_VERIFIED";
    public const string UserExternalAuthenticated = "USER_EXTERNAL_AUTHENTICATED";
    public const string RefreshTokenReuseDetected = "REFRESH_TOKEN_REUSE_DETECTED";
    public const string ExitInterviewFormCreated = "EXIT_INTERVIEW_FORM_CREATED";
    public const string ExitInterviewFormUpdated = "EXIT_INTERVIEW_FORM_UPDATED";
    public const string ExitInterviewFormDeleted = "EXIT_INTERVIEW_FORM_DELETED";
    public const string ExitInterviewFormPublished = "EXIT_INTERVIEW_FORM_PUBLISHED";
    public const string ExitInterviewFormArchived = "EXIT_INTERVIEW_FORM_ARCHIVED";
    public const string ExitInterviewFormReasonAssigned = "EXIT_INTERVIEW_FORM_REASON_ASSIGNED";
    public const string ExitInterviewFormVersionCreated = "EXIT_INTERVIEW_FORM_VERSION_CREATED";
    public const string ExitInterviewSubmissionSaved = "EXIT_INTERVIEW_SUBMISSION_SAVED";
    public const string ExitInterviewSubmitted = "EXIT_INTERVIEW_SUBMITTED";
    public const string ExitInterviewSubmissionArchived = "EXIT_INTERVIEW_SUBMISSION_ARCHIVED";
    public const string MedicalClinicCreated = "MEDICAL_CLINIC_CREATED";
    public const string MedicalClinicUpdated = "MEDICAL_CLINIC_UPDATED";
    public const string MedicalClinicActivated = "MEDICAL_CLINIC_ACTIVATED";
    public const string MedicalClinicInactivated = "MEDICAL_CLINIC_INACTIVATED";
    public const string IncapacityTypeCreated = "INCAPACITY_TYPE_CREATED";
    public const string IncapacityTypeUpdated = "INCAPACITY_TYPE_UPDATED";
    public const string IncapacityTypeActivated = "INCAPACITY_TYPE_ACTIVATED";
    public const string IncapacityTypeInactivated = "INCAPACITY_TYPE_INACTIVATED";
    public const string CompensatoryTimeTypeCreated = "COMPENSATORY_TIME_TYPE_CREATED";
    public const string CompensatoryTimeTypeUpdated = "COMPENSATORY_TIME_TYPE_UPDATED";
    public const string CompensatoryTimeTypeActivated = "COMPENSATORY_TIME_TYPE_ACTIVATED";
    public const string CompensatoryTimeTypeInactivated = "COMPENSATORY_TIME_TYPE_INACTIVATED";
    public const string IncapacityRiskCreated = "INCAPACITY_RISK_CREATED";
    public const string IncapacityRiskUpdated = "INCAPACITY_RISK_UPDATED";
    public const string IncapacityRiskParametersReplaced = "INCAPACITY_RISK_PARAMETERS_REPLACED";
    public const string IncapacityRiskActivated = "INCAPACITY_RISK_ACTIVATED";
    public const string IncapacityRiskInactivated = "INCAPACITY_RISK_INACTIVATED";
    public const string CompanyHolidayCreated = "COMPANY_HOLIDAY_CREATED";
    public const string CompanyHolidayUpdated = "COMPANY_HOLIDAY_UPDATED";
    public const string CompanyHolidayActivated = "COMPANY_HOLIDAY_ACTIVATED";
    public const string CompanyHolidayInactivated = "COMPANY_HOLIDAY_INACTIVATED";
    public const string PayrollPeriodDefinitionCreated = "PAYROLL_PERIOD_DEFINITION_CREATED";
    public const string PayrollPeriodDefinitionUpdated = "PAYROLL_PERIOD_DEFINITION_UPDATED";
    public const string PayrollPeriodDefinitionActivated = "PAYROLL_PERIOD_DEFINITION_ACTIVATED";
    public const string PayrollPeriodDefinitionInactivated = "PAYROLL_PERIOD_DEFINITION_INACTIVATED";
    public const string LeaveTemplateLoaded = "LEAVE_TEMPLATE_LOADED";
    public const string RecognitionTypeCreated = "RECOGNITION_TYPE_CREATED";
    public const string RecognitionTypeUpdated = "RECOGNITION_TYPE_UPDATED";
    public const string RecognitionTypeActivated = "RECOGNITION_TYPE_ACTIVATED";
    public const string RecognitionTypeInactivated = "RECOGNITION_TYPE_INACTIVATED";
    public const string DisciplinaryActionTypeCreated = "DISCIPLINARY_ACTION_TYPE_CREATED";
    public const string DisciplinaryActionTypeUpdated = "DISCIPLINARY_ACTION_TYPE_UPDATED";
    public const string DisciplinaryActionTypeActivated = "DISCIPLINARY_ACTION_TYPE_ACTIVATED";
    public const string DisciplinaryActionTypeInactivated = "DISCIPLINARY_ACTION_TYPE_INACTIVATED";
    public const string DisciplinaryActionCauseCreated = "DISCIPLINARY_ACTION_CAUSE_CREATED";
    public const string DisciplinaryActionCauseUpdated = "DISCIPLINARY_ACTION_CAUSE_UPDATED";
    public const string DisciplinaryActionCauseActivated = "DISCIPLINARY_ACTION_CAUSE_ACTIVATED";
    public const string DisciplinaryActionCauseInactivated = "DISCIPLINARY_ACTION_CAUSE_INACTIVATED";
    public const string EmployeeRelationsTemplateLoaded = "EMPLOYEE_RELATIONS_TEMPLATE_LOADED";
    public const string OvertimeTypeCreated = "OVERTIME_TYPE_CREATED";
    public const string OvertimeTypeUpdated = "OVERTIME_TYPE_UPDATED";
    public const string OvertimeTypeActivated = "OVERTIME_TYPE_ACTIVATED";
    public const string OvertimeTypeInactivated = "OVERTIME_TYPE_INACTIVATED";
    public const string OvertimeJustificationTypeCreated = "OVERTIME_JUSTIFICATION_TYPE_CREATED";
    public const string OvertimeJustificationTypeUpdated = "OVERTIME_JUSTIFICATION_TYPE_UPDATED";
    public const string OvertimeJustificationTypeActivated = "OVERTIME_JUSTIFICATION_TYPE_ACTIVATED";
    public const string OvertimeJustificationTypeInactivated = "OVERTIME_JUSTIFICATION_TYPE_INACTIVATED";
    public const string OvertimeTemplateLoaded = "OVERTIME_TEMPLATE_LOADED";
    public const string PayrollDefinitionCreated = "PAYROLL_DEFINITION_CREATED";
    public const string PayrollDefinitionUpdated = "PAYROLL_DEFINITION_UPDATED";
    public const string PayrollDefinitionActivated = "PAYROLL_DEFINITION_ACTIVATED";
    public const string PayrollDefinitionInactivated = "PAYROLL_DEFINITION_INACTIVATED";
    public const string PayrollPeriodCalendarGenerated = "PAYROLL_PERIOD_CALENDAR_GENERATED";
    public const string WorkScheduleCreated = "WORK_SCHEDULE_CREATED";
    public const string WorkScheduleUpdated = "WORK_SCHEDULE_UPDATED";
    public const string WorkScheduleActivated = "WORK_SCHEDULE_ACTIVATED";
    public const string WorkScheduleInactivated = "WORK_SCHEDULE_INACTIVATED";
    public const string PayrollConfigurationTemplateLoaded = "PAYROLL_CONFIGURATION_TEMPLATE_LOADED";
    public const string VacationPeriodsGenerated = "VACATION_PERIODS_GENERATED";
    public const string VacationPlanSaved = "VACATION_PLAN_SAVED";
    public const string RecurringIncomeInstallmentsApplied = "RECURRING_INCOME_INSTALLMENTS_APPLIED";
    public const string RecurringDeductionInstallmentsApplied = "RECURRING_DEDUCTION_INSTALLMENTS_APPLIED";
    public const string OneTimeIncomeApplicationsApplied = "ONE_TIME_INCOME_APPLICATIONS_APPLIED";
    public const string OneTimeDeductionApplicationsApplied = "ONE_TIME_DEDUCTION_APPLICATIONS_APPLIED";
    public const string OvertimeApplicationsApplied = "OVERTIME_APPLICATIONS_APPLIED";

    public static readonly IReadOnlyCollection<string> All =
    [
        UserCreated,
        UserUpdated,
        UserDeactivated,
        UserReactivated,
        UserActivated,
        UserInvited,
        UserInvitationReset,
        RoleCreated,
        RoleUpdated,
        RoleCloned,
        RoleDeleted,
        RoleResourcePermissionsUpdated,
        RoleFieldPermissionsUpdated,
        CompanyCreated,
        CompanyUpdated,
        CompanyArchived,
        CompanyReactivated,
        ActiveCompanySwitched,
        CommercialAddonCreated,
        CommercialAddonUpdated,
        CommercialAddonActivated,
        CommercialAddonInactivated,
        CommercialPlanCreated,
        CommercialPlanUpdated,
        CommercialPlanActivated,
        CommercialPlanInactivated,
        BankCatalogItemCreated,
        BankCatalogItemUpdated,
        BankCatalogItemActivated,
        BankCatalogItemInactivated,
        CompanySubscriptionActivated,
        CompanySubscriptionScheduled,
        CompanySubscriptionStatusChanged,
        CompanySubscriptionStatusChangeRequested,
        CompanySubscriptionStatusChangeApplied,
        CompanySubscriptionStatusChangeRejected,
        CompanySubscriptionPromotionProcessed,
        CompanySubscriptionExpirationProcessed,
        CompanySubscriptionPlanChangeRequested,
        CompanySubscriptionPlanChangeApplied,
        CompanySubscriptionPlanChangeCancelled,
        CompanySubscriptionPlanChangeRejected,
        CompanySubscriptionAddonChangeRequested,
        CompanySubscriptionAddonChangeApplied,
        CompanySubscriptionAddonChangeCancelled,
        CompanySubscriptionAddonChangeRejected,
        CompanyTypeCatalogItemCreated,
        CompanyTypeCatalogItemUpdated,
        CompanyTypeCatalogItemActivated,
        CompanyTypeCatalogItemInactivated,
        OrgUnitCreated,
        OrgUnitUpdated,
        OrgUnitMoved,
        OrgUnitActivated,
        OrgUnitInactivated,
        OrgUnitTypeCatalogItemCreated,
        OrgUnitTypeCatalogItemUpdated,
        OrgUnitTypeCatalogItemActivated,
        OrgUnitTypeCatalogItemInactivated,
        FunctionalAreaCatalogItemCreated,
        FunctionalAreaCatalogItemUpdated,
        FunctionalAreaCatalogItemActivated,
        FunctionalAreaCatalogItemInactivated,
        JobProfileCreated,
        JobProfileUpdated,
        JobProfileCompensationCreated,
        JobProfileCompensationUpdated,
        JobProfileCompensationDeleted,
        JobProfilePublished,
        JobProfileArchived,
        JobCatalogItemCreated,
        JobCatalogItemUpdated,
        InternalCatalogValueCreated,
        PositionDescriptionCatalogItemCreated,
        PositionDescriptionCatalogItemUpdated,
        PositionDescriptionCatalogItemActivated,
        PositionDescriptionCatalogItemInactivated,
        PositionCategoryClassificationCreated,
        PositionCategoryClassificationUpdated,
        PositionCategoryClassificationActivated,
        PositionCategoryClassificationInactivated,
        PositionCategoryCreated,
        PositionCategoryUpdated,
        PositionCategoryActivated,
        PositionCategoryInactivated,
        PositionSlotCreated,
        PositionSlotUpdated,
        PositionSlotStatusChanged,
        PositionSlotDependencyUpdated,
        PositionSlotOccupancyChanged,
        CostCenterCreated,
        CostCenterUpdated,
        CostCenterActivated,
        CostCenterInactivated,
        CostCenterTypeCreated,
        CostCenterTypeUpdated,
        CostCenterTypeActivated,
        CostCenterTypeInactivated,
        LegalRepresentativeCreated,
        LegalRepresentativeUpdated,
        LegalRepresentativeActivated,
        LegalRepresentativeInactivated,
        LegalRepresentativeSetPrimary,
        PersonnelFileCreated,
        PersonnelFileUpdated,
        PersonnelFileCompleted,
        PersonnelFileActivated,
        PersonnelFileInactivated,
        PersonnelFileDocumentUploaded,
        PersonnelFileDocumentFileReplaced,
        PersonnelFileDocumentInactivated,
        PersonnelFileObservationAdded,
        OccupationalPyramidLevelCreated,
        OccupationalPyramidLevelUpdated,
        OccupationalPyramidLevelActivated,
        OccupationalPyramidLevelInactivated,
        CompetencyConductCreated,
        CompetencyConductUpdated,
        CompetencyConductActivated,
        CompetencyConductInactivated,
        CompetencyBehaviorLinked,
        JobProfileCompetencyMatrixUpdated,
        CompetencyRatingScaleUpdated,
        SalaryTabulatorRequestCreated,
        SalaryTabulatorRequestUpdated,
        SalaryTabulatorRequestSubmitted,
        SalaryTabulatorRequestApproved,
        SalaryTabulatorRequestRejected,
        SalaryTabulatorRequestCanceled,
        SalaryTabulatorLineApplied,
        SalaryTabulatorLineInactivated,
        ReportExported,
        ReportPrinted,
        LocationGroupCreated,
        LocationGroupUpdated,
        LocationGroupMoved,
        LocationGroupActivated,
        LocationGroupInactivated,
        WorkCenterCreated,
        WorkCenterUpdated,
        WorkCenterReassigned,
        WorkCenterActivated,
        WorkCenterInactivated,
        WorkCenterTypeCreated,
        WorkCenterTypeUpdated,
        WorkCenterTypeActivated,
        WorkCenterTypeInactivated,
        LocationHierarchyUpdated,
        LocationLevelCreated,
        LocationLevelUpdated,
        LocationLevelActivated,
        LocationLevelInactivated,
        CompanyPreferencesUpdated,
        UserLoggedIn,
        UserLoginFailed,
        UserLoginThrottled,
        UserLoggedOut,
        UserRegistered,
        UserEmailVerified,
        UserExternalAuthenticated,
        RefreshTokenReuseDetected,
        ExitInterviewFormCreated,
        ExitInterviewFormUpdated,
        ExitInterviewFormDeleted,
        ExitInterviewFormPublished,
        ExitInterviewFormArchived,
        ExitInterviewFormReasonAssigned,
        ExitInterviewFormVersionCreated,
        ExitInterviewSubmissionSaved,
        ExitInterviewSubmitted,
        ExitInterviewSubmissionArchived,
        MedicalClinicCreated,
        MedicalClinicUpdated,
        MedicalClinicActivated,
        MedicalClinicInactivated,
        IncapacityTypeCreated,
        IncapacityTypeUpdated,
        IncapacityTypeActivated,
        IncapacityTypeInactivated,
        CompensatoryTimeTypeCreated,
        CompensatoryTimeTypeUpdated,
        CompensatoryTimeTypeActivated,
        CompensatoryTimeTypeInactivated,
        IncapacityRiskCreated,
        IncapacityRiskUpdated,
        IncapacityRiskParametersReplaced,
        IncapacityRiskActivated,
        IncapacityRiskInactivated,
        CompanyHolidayCreated,
        CompanyHolidayUpdated,
        CompanyHolidayActivated,
        CompanyHolidayInactivated,
        PayrollPeriodDefinitionCreated,
        PayrollPeriodDefinitionUpdated,
        PayrollPeriodDefinitionActivated,
        PayrollPeriodDefinitionInactivated,
        LeaveTemplateLoaded,
        RecognitionTypeCreated,
        RecognitionTypeUpdated,
        RecognitionTypeActivated,
        RecognitionTypeInactivated,
        DisciplinaryActionTypeCreated,
        DisciplinaryActionTypeUpdated,
        DisciplinaryActionTypeActivated,
        DisciplinaryActionTypeInactivated,
        DisciplinaryActionCauseCreated,
        DisciplinaryActionCauseUpdated,
        DisciplinaryActionCauseActivated,
        DisciplinaryActionCauseInactivated,
        EmployeeRelationsTemplateLoaded,
        OvertimeTypeCreated,
        OvertimeTypeUpdated,
        OvertimeTypeActivated,
        OvertimeTypeInactivated,
        OvertimeJustificationTypeCreated,
        OvertimeJustificationTypeUpdated,
        OvertimeJustificationTypeActivated,
        OvertimeJustificationTypeInactivated,
        OvertimeTemplateLoaded,
        PayrollDefinitionCreated,
        PayrollDefinitionUpdated,
        PayrollDefinitionActivated,
        PayrollDefinitionInactivated,
        PayrollPeriodCalendarGenerated,
        WorkScheduleCreated,
        WorkScheduleUpdated,
        WorkScheduleActivated,
        WorkScheduleInactivated,
        PayrollConfigurationTemplateLoaded,
        VacationPeriodsGenerated,
        VacationPlanSaved,
        RecurringIncomeInstallmentsApplied,
        RecurringDeductionInstallmentsApplied,
        OneTimeIncomeApplicationsApplied,
        OneTimeDeductionApplicationsApplied,
        OvertimeApplicationsApplied
    ];

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        normalized = All.SingleOrDefault(candidate => candidate.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return normalized.Length > 0;
    }
}

public static class AuditEntityTypes
{
    public const string User = "User";
    public const string Role = "Role";
    public const string Permission = "Permission";
    public const string Company = "Company";
    public const string CommercialAddon = "CommercialAddon";
    public const string CommercialPlan = "CommercialPlan";
    public const string BankCatalogItem = "BankCatalogItem";
    public const string CompanySubscription = "CompanySubscription";
    public const string CompanySubscriptionStatusChangeRequest = "CompanySubscriptionStatusChangeRequest";
    public const string CompanySubscriptionPlanChange = "CompanySubscriptionPlanChange";
    public const string CompanyCommercialAddon = "CompanyCommercialAddon";
    public const string CompanyCommercialAddonChange = "CompanyCommercialAddonChange";
    public const string CompanyTypeCatalogItem = "CompanyTypeCatalogItem";
    public const string OrgUnit = "OrgUnit";
    public const string OrgUnitTypeCatalogItem = "OrgUnitTypeCatalogItem";
    public const string FunctionalAreaCatalogItem = "FunctionalAreaCatalogItem";
    public const string JobProfile = "JobProfile";
    public const string JobProfileCompensation = "JobProfileCompensation";
    public const string JobCatalogItem = "JobCatalogItem";
    public const string InternalCatalogValue = "InternalCatalogValue";
    public const string PositionDescriptionCatalogItem = "PositionDescriptionCatalogItem";
    public const string PositionCategoryClassification = "PositionCategoryClassification";
    public const string PositionCategory = "PositionCategory";
    public const string PositionSlot = "PositionSlot";
    public const string CostCenter = "CostCenter";
    public const string CostCenterType = "CostCenterType";
    public const string LegalRepresentative = "LegalRepresentative";
    public const string PersonnelFile = "PersonnelFile";
    public const string OccupationalPyramidLevel = "OccupationalPyramidLevel";
    public const string CompetencyRatingScale = "CompetencyRatingScale";
    public const string CompetencyConduct = "CompetencyConduct";
    public const string JobProfileCompetencyMatrix = "JobProfileCompetencyMatrix";
    public const string SalaryTabulatorChangeRequest = "SalaryTabulatorChangeRequest";
    public const string SalaryTabulatorLine = "SalaryTabulatorLine";
    public const string LocationGroup = "LocationGroup";
    public const string WorkCenter = "WorkCenter";
    public const string WorkCenterType = "WorkCenterType";
    public const string LocationHierarchy = "LocationHierarchy";
    public const string LocationLevel = "LocationLevel";
    public const string CompanyPreference = "CompanyPreference";
    public const string ExitInterviewForm = "ExitInterviewForm";
    public const string ExitInterviewSubmission = "ExitInterviewSubmission";
    public const string MedicalClinic = "MedicalClinic";
    public const string IncapacityType = "IncapacityType";
    public const string CompensatoryTimeType = "CompensatoryTimeType";
    public const string IncapacityRisk = "IncapacityRisk";
    public const string CompanyHoliday = "CompanyHoliday";
    public const string PayrollPeriodDefinition = "PayrollPeriodDefinition";
    public const string LeaveConfiguration = "LeaveConfiguration";
    public const string RecognitionType = "RecognitionType";
    public const string DisciplinaryActionType = "DisciplinaryActionType";
    public const string DisciplinaryActionCause = "DisciplinaryActionCause";
    public const string EmployeeRelationsConfiguration = "EmployeeRelationsConfiguration";
    public const string OvertimeType = "OvertimeType";
    public const string OvertimeJustificationType = "OvertimeJustificationType";
    public const string OvertimeConfiguration = "OvertimeConfiguration";
    public const string PayrollDefinition = "PayrollDefinition";
    public const string WorkSchedule = "WorkSchedule";
    public const string PayrollConfiguration = "PayrollConfiguration";

    public static readonly IReadOnlyCollection<string> All =
    [
        User,
        Role,
        Permission,
        Company,
        CommercialAddon,
        CommercialPlan,
        BankCatalogItem,
        CompanySubscription,
        CompanySubscriptionStatusChangeRequest,
        CompanySubscriptionPlanChange,
        CompanyCommercialAddon,
        CompanyCommercialAddonChange,
        CompanyTypeCatalogItem,
        OrgUnit,
        OrgUnitTypeCatalogItem,
        FunctionalAreaCatalogItem,
        JobProfile,
        JobProfileCompensation,
        JobCatalogItem,
        InternalCatalogValue,
        PositionDescriptionCatalogItem,
        PositionCategoryClassification,
        PositionCategory,
        PositionSlot,
        CostCenter,
        CostCenterType,
        LegalRepresentative,
        PersonnelFile,
        OccupationalPyramidLevel,
        CompetencyConduct,
        CompetencyRatingScale,
        JobProfileCompetencyMatrix,
        SalaryTabulatorChangeRequest,
        SalaryTabulatorLine,
        LocationGroup,
        WorkCenter,
        WorkCenterType,
        LocationHierarchy,
        LocationLevel,
        CompanyPreference,
        ExitInterviewForm,
        ExitInterviewSubmission,
        MedicalClinic,
        IncapacityType,
        CompensatoryTimeType,
        IncapacityRisk,
        CompanyHoliday,
        PayrollPeriodDefinition,
        LeaveConfiguration,
        RecognitionType,
        DisciplinaryActionType,
        DisciplinaryActionCause,
        EmployeeRelationsConfiguration,
        OvertimeType,
        OvertimeJustificationType,
        OvertimeConfiguration,
        PayrollDefinition,
        WorkSchedule,
        PayrollConfiguration
    ];

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        normalized = All.SingleOrDefault(candidate => candidate.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return normalized.Length > 0;
    }
}

public static class AuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Deactivate = "Deactivate";
    public const string Reactivate = "Reactivate";
    public const string Invite = "Invite";
    public const string InvitationReset = "InvitationReset";
    public const string Clone = "Clone";
    public const string PermissionChange = "PermissionChange";
    public const string Archive = "Archive";
    public const string Switch = "Switch";
    public const string Export = "Export";
    public const string Print = "Print";
    public const string Login = "Login";
    public const string Logout = "Logout";
    public const string SecurityAlert = "SecurityAlert";
}
