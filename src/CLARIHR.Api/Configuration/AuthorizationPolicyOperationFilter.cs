using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CLARIHR.Api.Configuration;

/// <summary>
/// Surfaces the declarative authorization policy assigned by
/// <see cref="CLARIHR.Api.Common.Conventions.AuthorizationPolicyConvention"/> into the
/// OpenAPI document by appending the accepted permission set to the operation description.
/// The policy→codes map mirrors the <c>AddAuthorization</c> registrations in
/// <c>Program.cs</c>; its single source of truth is the <c>*PermissionCodes</c> constants,
/// so the documentation cannot drift from the runtime assertions.
/// </summary>
public sealed class AuthorizationPolicyOperationFilter : IOperationFilter
{
    private static readonly IReadOnlyDictionary<string, string[]> CodesByPolicy =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [JobProfilePolicies.Read] =
            [
                JobProfilePermissionCodes.Read,
                JobProfilePermissionCodes.Admin,
                JobProfilePermissionCodes.CatalogAdmin,
                JobProfilePermissionCodes.ManageAdministration,
            ],
            [JobProfilePolicies.Manage] =
            [
                JobProfilePermissionCodes.Admin,
                JobProfilePermissionCodes.ManageAdministration,
            ],
            [JobProfilePolicies.ManageCatalogs] =
            [
                JobProfilePermissionCodes.CatalogAdmin,
                JobProfilePermissionCodes.ManageAdministration,
            ],
            [PositionDescriptionCatalogPolicies.Read] =
            [
                PositionDescriptionCatalogPermissionCodes.Read,
                PositionDescriptionCatalogPermissionCodes.Admin,
                PositionDescriptionCatalogPermissionCodes.ManageAdministration,
            ],
            [PositionDescriptionCatalogPolicies.Manage] =
            [
                PositionDescriptionCatalogPermissionCodes.Admin,
                PositionDescriptionCatalogPermissionCodes.ManageAdministration,
            ],
            [PositionSlotPolicies.Read] =
            [
                PositionSlotPermissionCodes.Read,
                PositionSlotPermissionCodes.Admin,
                PositionSlotPermissionCodes.ManageAdministration,
            ],
            [PositionSlotPolicies.Manage] =
            [
                PositionSlotPermissionCodes.Admin,
                PositionSlotPermissionCodes.ManageAdministration,
            ],
            [PersonnelFilePolicies.Read] =
            [
                PersonnelFilePermissionCodes.Read,
                PersonnelFilePermissionCodes.Admin,
                PersonnelFilePermissionCodes.ManageAdministration,
            ],
            [PersonnelFilePolicies.Manage] =
            [
                PersonnelFilePermissionCodes.Admin,
                PersonnelFilePermissionCodes.ManageAdministration,
            ],
        };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var policyName = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Select(static data => data.Policy)
            .LastOrDefault(name => name is not null && CodesByPolicy.ContainsKey(name));

        if (policyName is null)
        {
            return;
        }

        var line = $"**Requires permission:** one of `{string.Join("`, `", CodesByPolicy[policyName])}`.";
        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? line
            : $"{operation.Description}\n\n{line}";
    }
}
