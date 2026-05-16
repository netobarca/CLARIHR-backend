using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CLARIHR.Api.Common.Conventions;

/// <summary>
/// Assigns the declarative authorization policy to every action of the JobProfile and
/// PositionDescriptionCatalog controllers by HTTP verb, as defense-in-depth on top of the
/// class-level <c>[Authorize]</c> (both compose with AND — the handler remains the precise
/// gate for tenant/entitlement/membership). This is the single source of truth for the
/// endpoint→policy mapping: a newly added GET/POST inherits the correct policy
/// automatically, preventing the kind of drift documented in JobProfiles debt §2.3.
///
/// Mapping (verified inventory, 71 endpoints):
/// <list type="bullet">
///   <item>GET/HEAD → <c>{Domain}Policies.Read</c></item>
///   <item>POST/PUT/PATCH/DELETE → <c>{Domain}Policies.Manage</c></item>
/// </list>
/// Controllers in scope:
/// <list type="bullet">
///   <item>JobProfile (10): JobProfilesController + 9 sub-controllers → <see cref="JobProfilePolicies"/></item>
///   <item>PositionDescriptionCatalog (3): PositionCategoriesController,
///     PositionCategoryClassificationsController, PositionDescriptionCatalogItemsController
///     → <see cref="PositionDescriptionCatalogPolicies"/></item>
/// </list>
/// Mirrors the project convention pattern (<see cref="ProducesStandardErrorsConvention"/>,
/// registered alongside it in <c>Program.cs</c>).
/// </summary>
public sealed class AuthorizationPolicyConvention : IActionModelConvention
{
    private static readonly IReadOnlyDictionary<string, (string Read, string Manage)> PolicyByController =
        new Dictionary<string, (string Read, string Manage)>(StringComparer.Ordinal)
        {
            ["JobProfilesController"] = (JobProfilePolicies.Read, JobProfilePolicies.Manage),
            ["JobProfileBenefitsController"] = (JobProfilePolicies.Read, JobProfilePolicies.Manage),
            ["JobProfileCompensationsController"] = (JobProfilePolicies.Read, JobProfilePolicies.Manage),
            ["JobProfileCompetenciesController"] = (JobProfilePolicies.Read, JobProfilePolicies.Manage),
            ["JobProfileDependentPositionsController"] = (JobProfilePolicies.Read, JobProfilePolicies.Manage),
            ["JobProfileFunctionsController"] = (JobProfilePolicies.Read, JobProfilePolicies.Manage),
            ["JobProfileRelationsController"] = (JobProfilePolicies.Read, JobProfilePolicies.Manage),
            ["JobProfileRequirementsController"] = (JobProfilePolicies.Read, JobProfilePolicies.Manage),
            ["JobProfileTrainingsController"] = (JobProfilePolicies.Read, JobProfilePolicies.Manage),
            ["JobProfileWorkingConditionsController"] = (JobProfilePolicies.Read, JobProfilePolicies.Manage),
            ["PositionCategoriesController"] =
                (PositionDescriptionCatalogPolicies.Read, PositionDescriptionCatalogPolicies.Manage),
            ["PositionCategoryClassificationsController"] =
                (PositionDescriptionCatalogPolicies.Read, PositionDescriptionCatalogPolicies.Manage),
            ["PositionDescriptionCatalogItemsController"] =
                (PositionDescriptionCatalogPolicies.Read, PositionDescriptionCatalogPolicies.Manage),
        };

    public void Apply(ActionModel action)
    {
        if (!PolicyByController.TryGetValue(action.Controller.ControllerType.Name, out var policies))
        {
            return;
        }

        var httpMethods = action.Attributes
            .OfType<HttpMethodAttribute>()
            .SelectMany(static attribute => attribute.HttpMethods)
            .ToArray();

        var isReadVerb = httpMethods.Length > 0 && httpMethods.All(static method =>
            string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase));

        var policyName = isReadVerb ? policies.Read : policies.Manage;

        foreach (var selector in action.Selectors)
        {
            if (selector.EndpointMetadata
                .OfType<IAuthorizeData>()
                .Any(data => string.Equals(data.Policy, policyName, StringComparison.Ordinal)))
            {
                continue;
            }

            selector.EndpointMetadata.Add(new AuthorizeAttribute(policyName));
        }
    }
}
