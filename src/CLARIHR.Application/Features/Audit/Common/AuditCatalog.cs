namespace CLARIHR.Application.Features.Audit.Common;

public static class AuditEventTypes
{
    public const string UserCreated = "USER_CREATED";
    public const string UserUpdated = "USER_UPDATED";
    public const string UserDeactivated = "USER_DEACTIVATED";
    public const string UserReactivated = "USER_REACTIVATED";
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
    public const string OrgUnitCreated = "ORG_UNIT_CREATED";
    public const string OrgUnitUpdated = "ORG_UNIT_UPDATED";
    public const string OrgUnitMoved = "ORG_UNIT_MOVED";
    public const string OrgUnitActivated = "ORG_UNIT_ACTIVATED";
    public const string OrgUnitInactivated = "ORG_UNIT_INACTIVATED";
    public const string JobProfileCreated = "JOB_PROFILE_CREATED";
    public const string JobProfileUpdated = "JOB_PROFILE_UPDATED";
    public const string JobProfilePublished = "JOB_PROFILE_PUBLISHED";
    public const string JobProfileArchived = "JOB_PROFILE_ARCHIVED";
    public const string JobCatalogItemCreated = "JOB_CATALOG_ITEM_CREATED";
    public const string JobCatalogItemUpdated = "JOB_CATALOG_ITEM_UPDATED";

    public static readonly IReadOnlyCollection<string> All =
    [
        UserCreated,
        UserUpdated,
        UserDeactivated,
        UserReactivated,
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
        OrgUnitCreated,
        OrgUnitUpdated,
        OrgUnitMoved,
        OrgUnitActivated,
        OrgUnitInactivated,
        JobProfileCreated,
        JobProfileUpdated,
        JobProfilePublished,
        JobProfileArchived,
        JobCatalogItemCreated,
        JobCatalogItemUpdated
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
    public const string OrgUnit = "OrgUnit";
    public const string JobProfile = "JobProfile";
    public const string JobCatalogItem = "JobCatalogItem";

    public static readonly IReadOnlyCollection<string> All =
    [
        User,
        Role,
        Permission,
        Company,
        OrgUnit,
        JobProfile,
        JobCatalogItem
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
}
