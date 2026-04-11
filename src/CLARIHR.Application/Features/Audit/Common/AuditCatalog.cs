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
    public const string PersonnelFileDocumentInactivated = "PERSONNEL_FILE_DOCUMENT_INACTIVATED";
    public const string PersonnelFileObservationAdded = "PERSONNEL_FILE_OBSERVATION_ADDED";
    public const string PersonnelCustomFieldDefinitionCreated = "PERSONNEL_CUSTOM_FIELD_DEFINITION_CREATED";
    public const string PersonnelCustomFieldDefinitionUpdated = "PERSONNEL_CUSTOM_FIELD_DEFINITION_UPDATED";
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
        PersonnelFileDocumentInactivated,
        PersonnelFileObservationAdded,
        PersonnelCustomFieldDefinitionCreated,
        PersonnelCustomFieldDefinitionUpdated,
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
        SalaryTabulatorRequestCreated,
        SalaryTabulatorRequestUpdated,
        SalaryTabulatorRequestSubmitted,
        SalaryTabulatorRequestApproved,
        SalaryTabulatorRequestRejected,
        SalaryTabulatorRequestCanceled,
        SalaryTabulatorLineApplied,
        SalaryTabulatorLineInactivated,
        ReportExported,
        ReportPrinted
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
    public const string JobCatalogItem = "JobCatalogItem";
    public const string InternalCatalogValue = "InternalCatalogValue";
    public const string PositionDescriptionCatalogItem = "PositionDescriptionCatalogItem";
    public const string PositionCategoryClassification = "PositionCategoryClassification";
    public const string PositionCategory = "PositionCategory";
    public const string PositionSlot = "PositionSlot";
    public const string CostCenter = "CostCenter";
    public const string LegalRepresentative = "LegalRepresentative";
    public const string PersonnelFile = "PersonnelFile";
    public const string OccupationalPyramidLevel = "OccupationalPyramidLevel";
    public const string CompetencyConduct = "CompetencyConduct";
    public const string JobProfileCompetencyMatrix = "JobProfileCompetencyMatrix";
    public const string SalaryTabulatorChangeRequest = "SalaryTabulatorChangeRequest";
    public const string SalaryTabulatorLine = "SalaryTabulatorLine";

    public static readonly IReadOnlyCollection<string> All =
    [
        User,
        Role,
        Permission,
        Company,
        CommercialAddon,
        CommercialPlan,
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
        JobCatalogItem,
        InternalCatalogValue,
        PositionDescriptionCatalogItem,
        PositionCategoryClassification,
        PositionCategory,
        PositionSlot,
        CostCenter,
        LegalRepresentative,
        PersonnelFile,
        OccupationalPyramidLevel,
        CompetencyConduct,
        JobProfileCompetencyMatrix,
        SalaryTabulatorChangeRequest,
        SalaryTabulatorLine
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
}
