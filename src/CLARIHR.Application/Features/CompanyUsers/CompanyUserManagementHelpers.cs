using System.Security.Cryptography;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;

namespace CLARIHR.Application.Features.CompanyUsers;

internal static class CompanyUserManagementHelpers
{
    public static bool IsAdministrativeRole(IamRole role)
    {
        var normalizedManageUsers = CompanyUserPermissionCodes.ManageUsers.ToUpperInvariant();
        var normalizedManageAdministration = IdentityPermissionCodes.ManageAdministration.ToUpperInvariant();

        return role.PermissionAssignments.Any(assignment =>
            assignment.Permission.NormalizedCode == normalizedManageUsers ||
            assignment.Permission.NormalizedCode == normalizedManageAdministration);
    }

    public static void StampTenant(IEnumerable<IamUserRoleAssignment> assignments, Guid tenantId)
    {
        foreach (var assignment in assignments)
        {
            assignment.SetTenantId(tenantId);
        }
    }

    public static IReadOnlyCollection<string> GetCreateFieldKeys() =>
    [
        CompanyUserFieldKeys.Email,
        CompanyUserFieldKeys.FirstName,
        CompanyUserFieldKeys.LastName,
        CompanyUserFieldKeys.Role
    ];

    public static IReadOnlyCollection<string> GetChangedUpdateFieldKeys(
        User user,
        UserCompanyMembership membership,
        IamRole desiredRole,
        UpdateCompanyUserCommand command)
    {
        var changedFields = new List<string>(3);

        if (!string.Equals(user.FirstName, command.FirstName, StringComparison.Ordinal))
        {
            changedFields.Add(CompanyUserFieldKeys.FirstName);
        }

        if (!string.Equals(user.LastName, command.LastName, StringComparison.Ordinal))
        {
            changedFields.Add(CompanyUserFieldKeys.LastName);
        }

        if (membership.RoleId != desiredRole.Id)
        {
            changedFields.Add(CompanyUserFieldKeys.Role);
        }

        return changedFields;
    }

    public static PagedResponse<CompanyUserSummaryResponse> Filter(
        PagedResponse<CompanyUserSummaryResponse> response,
        FieldAccessProfile fieldAccessProfile,
        IFieldSerializationService fieldSerializationService)
    {
        var items = response.Items
            .Select(item => Filter(item, fieldAccessProfile, fieldSerializationService))
            .ToArray();

        return new PagedResponse<CompanyUserSummaryResponse>(items, response.PageNumber, response.PageSize, response.TotalCount);
    }

    public static CompanyUserSummaryResponse Filter(
        CompanyUserSummaryResponse response,
        FieldAccessProfile fieldAccessProfile,
        IFieldSerializationService fieldSerializationService) =>
        new(
            response.Id,
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Email), response.Email),
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.FirstName), response.FirstName),
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.LastName), response.LastName),
            response.RoleId.HasValue
                ? fieldSerializationService.SerializeGuid(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Role), response.RoleId.Value)
                : null,
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Role), response.Role),
            response.Status.HasValue
                ? fieldSerializationService.SerializeEnum(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Status), response.Status.Value)
                : null,
            response.AllowedActions);

    public static CompanyUserResponse Filter(
        CompanyUserResponse response,
        FieldAccessProfile fieldAccessProfile,
        IFieldSerializationService fieldSerializationService) =>
        new(
            response.Id,
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Email), response.Email),
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.FirstName), response.FirstName),
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.LastName), response.LastName),
            response.RoleId.HasValue
                ? fieldSerializationService.SerializeGuid(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Role), response.RoleId.Value)
                : null,
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Role), response.Role),
            response.Status.HasValue
                ? fieldSerializationService.SerializeEnum(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Status), response.Status.Value)
                : null);

    public static string CreateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public static CompanyUserSummaryResponse ApplyAllowedActions(
        CompanyUserSummaryResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManageUsers,
        bool isLastActiveAdministrator)
    {
        var status = response.Status;
        var isActive = status == UserStatus.Active;
        var isInactive = status == UserStatus.Inactive;

        return response with
        {
            AllowedActions = resourceActionPolicyService.Evaluate(
                new ResourceActionContext(
                    ResourceKey: CompanyUserFieldKeys.ResourceKey,
                    State: status?.ToString(),
                    IsActive: isActive,
                    HasDependencies: isLastActiveAdministrator,
                    SupportsEdit: true,
                    EditAllowed: canManageUsers,
                    SupportsActivate: isInactive,
                    ActivateAllowed: canManageUsers,
                    SupportsInactivate: isActive,
                    InactivateAllowed: canManageUsers))
        };
    }
}
