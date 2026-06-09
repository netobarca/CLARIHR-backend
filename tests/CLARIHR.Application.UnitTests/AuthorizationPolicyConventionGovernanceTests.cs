using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Application.Abstractions.OrgUnits;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Application.Features.PositionSlots.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §S1 guardrail (finding §J1 root cause): the authorization-policy convention is
/// drift-proof by construction — every in-scope controller declares its policy pair via
/// <see cref="AuthorizationPolicySetAttribute"/>. These tests close the residual drift
/// (a new in-scope controller that forgets the marker and would silently inherit the
/// bare authenticated <c>FallbackPolicy</c>) using only reflection — no hand-maintained
/// controller list, no IL inspection.
///
/// <para>Source A (independent "ought"): a domain is governed iff some CQRS handler in
/// <c>CLARIHR.Application</c> injects its precise authorization service. This is derived,
/// not enumerated, so it cannot drift out of sync with the handlers it mirrors.</para>
/// </summary>
public sealed class AuthorizationPolicyConventionGovernanceTests
{
    private static readonly Assembly ApplicationAssembly = typeof(IQuery<>).Assembly;
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    // The PersonnelFile sub-resource family is enrolled for whole-family coverage EXCEPT
    // PersonnelFileReportingController, excluded structurally via the (?!Reporting) lookahead
    // (no hand-maintained controller list, no cross-controller coupling). Reporting cannot carry
    // [AuthorizationPolicySet]: its POST `dynamic-query` is a read whose handler gates on
    // EnsureCanReadAsync, so the convention's Manage-on-POST would exceed the gate and yield false
    // 403s (the two-layer authorization superset invariant). It stays handler-gated + [Authorize].
    // JobProfileCompetencyMatrixController is carved out via (?!CompetencyMatrix): it belongs to the
    // CompetencyFramework domain (handler-gated via ICompetencyFrameworkAuthorizationService, like the
    // former CompetencyFrameworkController) and stays [Authorize]-only — requiring
    // [AuthorizationPolicySet(JobProfilePolicies...)] would be the wrong policy pair.
    // JobProfileInternalCatalogsController is carved out via (?!...|InternalCatalogs) for the same reason:
    // it is the JobProfiles "Internal" catalog family but PLATFORM-GLOBAL (cross-tenant, authn-only,
    // platform-audited), so a tenant-scoped [AuthorizationPolicySet(JobProfilePolicies...)] would be wrong.
    private static readonly Regex GovernedFamilyRegex =
        new(@"^(JobProfile(?!CompetencyMatrix|InternalCatalogs)|JobCatalog|PositionCategor|PositionDescriptionCatalog|PositionSlot|PersonnelFile(?!Reporting)|CostCenter|WorkCenter|LocationGroups|LocationLevels|LocationHierarchy|LegalRepresentatives|OrganizationUnits|OrganizationStructureCatalogs)", RegexOptions.Compiled);

    private static readonly HashSet<string> JobProfilePolicyNames = new(StringComparer.Ordinal)
    {
        JobProfilePolicies.Read,
        JobProfilePolicies.Manage,
        JobProfilePolicies.ManageCatalogs,
    };

    private static readonly HashSet<string> PositionDescriptionCatalogPolicyNames = new(StringComparer.Ordinal)
    {
        PositionDescriptionCatalogPolicies.Read,
        PositionDescriptionCatalogPolicies.Manage,
    };

    private static readonly HashSet<string> PositionSlotPolicyNames = new(StringComparer.Ordinal)
    {
        PositionSlotPolicies.Read,
        PositionSlotPolicies.Manage,
    };

    // The whole PersonnelFile sub-resource family is enrolled in GovernedFamilyRegex (so every
    // PF controller must declare the marker), except PersonnelFileReportingController which is
    // excluded by the (?!Reporting) lookahead for the read-via-POST reason documented there.
    private static readonly HashSet<string> PersonnelFilePolicyNames = new(StringComparer.Ordinal)
    {
        PersonnelFilePolicies.Read,
        PersonnelFilePolicies.Manage,
    };

    private static readonly HashSet<string> CostCenterPolicyNames = new(StringComparer.Ordinal)
    {
        CostCenterPolicies.Read,
        CostCenterPolicies.Manage,
    };

    private static readonly HashSet<string> LocationPolicyNames = new(StringComparer.Ordinal)
    {
        LocationPolicies.Read,
        LocationPolicies.Manage,
    };

    private static readonly HashSet<string> LegalRepresentativePolicyNames = new(StringComparer.Ordinal)
    {
        LegalRepresentativePolicies.Read,
        LegalRepresentativePolicies.Manage,
    };

    private static readonly HashSet<string> OrgUnitPolicyNames = new(StringComparer.Ordinal)
    {
        OrgUnitPolicies.Read,
        OrgUnitPolicies.Manage,
    };

    private static readonly HashSet<string> OrgStructureCatalogPolicyNames = new(StringComparer.Ordinal)
    {
        OrgStructureCatalogPolicies.Read,
        OrgStructureCatalogPolicies.Manage,
    };

    private static IReadOnlyList<(Type Controller, AuthorizationPolicySetAttribute? Marker)> Controllers() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Select(static type => (type, type.GetCustomAttribute<AuthorizationPolicySetAttribute>()))
            .ToArray();

    private static bool AnyHandlerInjects(Type authorizationService) =>
        ApplicationAssembly.GetTypes()
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.GetInterfaces().Any(static @interface => @interface.IsGenericType &&
                    (@interface.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
                     @interface.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))))
            .SelectMany(static type => type.GetConstructors(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            .SelectMany(static constructor => constructor.GetParameters())
            .Any(parameter => authorizationService.IsAssignableFrom(parameter.ParameterType));

    /// <summary>
    /// Inv-1 — every domain gated by a precise handler-layer authorization service has at
    /// least one controller declaring that domain's declarative policy pair. Catches an
    /// entire feature area shipping with zero declarative defense-in-depth.
    /// </summary>
    [Fact]
    public void EveryAuthzGovernedDomain_HasAtLeastOneMarkedController()
    {
        var controllers = Controllers();

        if (AnyHandlerInjects(typeof(IJobProfileAuthorizationService)))
        {
            Assert.Contains(controllers, entry => entry.Marker is not null &&
                JobProfilePolicyNames.Contains(entry.Marker.ReadPolicy) &&
                JobProfilePolicyNames.Contains(entry.Marker.ManagePolicy));
        }

        if (AnyHandlerInjects(typeof(IPositionDescriptionCatalogAuthorizationService)))
        {
            Assert.Contains(controllers, entry => entry.Marker is not null &&
                PositionDescriptionCatalogPolicyNames.Contains(entry.Marker.ReadPolicy) &&
                PositionDescriptionCatalogPolicyNames.Contains(entry.Marker.ManagePolicy));
        }

        if (AnyHandlerInjects(typeof(IPositionSlotAuthorizationService)))
        {
            Assert.Contains(controllers, entry => entry.Marker is not null &&
                PositionSlotPolicyNames.Contains(entry.Marker.ReadPolicy) &&
                PositionSlotPolicyNames.Contains(entry.Marker.ManagePolicy));
        }

        // The PersonnelFile family is enrolled for whole-family coverage in
        // EveryGovernedFamilyController_DeclaresPolicySetMarker (except Reporting, see the regex);
        // this Inv-1 check independently asserts at least one marked controller exists for the domain.
        if (AnyHandlerInjects(typeof(IPersonnelFileAuthorizationService)))
        {
            Assert.Contains(controllers, entry => entry.Marker is not null &&
                PersonnelFilePolicyNames.Contains(entry.Marker.ReadPolicy) &&
                PersonnelFilePolicyNames.Contains(entry.Marker.ManagePolicy));
        }

        if (AnyHandlerInjects(typeof(ICostCenterAuthorizationService)))
        {
            Assert.Contains(controllers, entry => entry.Marker is not null &&
                CostCenterPolicyNames.Contains(entry.Marker.ReadPolicy) &&
                CostCenterPolicyNames.Contains(entry.Marker.ManagePolicy));
        }

        if (AnyHandlerInjects(typeof(ILocationAuthorizationService)))
        {
            Assert.Contains(controllers, entry => entry.Marker is not null &&
                LocationPolicyNames.Contains(entry.Marker.ReadPolicy) &&
                LocationPolicyNames.Contains(entry.Marker.ManagePolicy));
        }

        if (AnyHandlerInjects(typeof(ILegalRepresentativeAuthorizationService)))
        {
            Assert.Contains(controllers, entry => entry.Marker is not null &&
                LegalRepresentativePolicyNames.Contains(entry.Marker.ReadPolicy) &&
                LegalRepresentativePolicyNames.Contains(entry.Marker.ManagePolicy));
        }

        if (AnyHandlerInjects(typeof(IOrgUnitAuthorizationService)))
        {
            Assert.Contains(controllers, entry => entry.Marker is not null &&
                OrgUnitPolicyNames.Contains(entry.Marker.ReadPolicy) &&
                OrgUnitPolicyNames.Contains(entry.Marker.ManagePolicy));
        }

        if (AnyHandlerInjects(typeof(IOrgStructureCatalogAuthorizationService)))
        {
            Assert.Contains(controllers, entry => entry.Marker is not null &&
                OrgStructureCatalogPolicyNames.Contains(entry.Marker.ReadPolicy) &&
                OrgStructureCatalogPolicyNames.Contains(entry.Marker.ManagePolicy));
        }
    }

    /// <summary>
    /// Inv-2 — the §J1 catcher. Every controller in a policy-governed feature family
    /// (JobProfile / JobCatalog / PositionDescriptionCatalog) MUST carry the marker, so a
    /// newly added one cannot silently fall back to the authenticated-only FallbackPolicy
    /// the way <c>JobCatalogsController</c> did when it was missing from the old dictionary.
    /// This is a structural pattern, not a hand-maintained enumeration.
    /// </summary>
    [Fact]
    public void EveryGovernedFamilyController_DeclaresPolicySetMarker()
    {
        // Sentinel: a future controller rename (or a regex typo) that makes the family
        // filter match zero controllers must fail loudly, not pass vacuously.
        Assert.Contains(Controllers(), entry => GovernedFamilyRegex.IsMatch(entry.Controller.Name));

        var unmarked = Controllers()
            .Where(entry => GovernedFamilyRegex.IsMatch(entry.Controller.Name) && entry.Marker is null)
            .Select(static entry => entry.Controller.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unmarked.Length == 0,
            "Finding §J1/§S1: controllers in a policy-governed family must declare " +
            "[AuthorizationPolicySet] or they silently inherit the bare authenticated " +
            "FallbackPolicy (no per-permission gate). Missing the marker:\n  " +
            string.Join("\n  ", unmarked));
    }

    /// <summary>
    /// Inv-3 — every declared marker references a real policy-name constant from the
    /// canonical <c>*Policies</c> classes (no typo, no orphan policy name).
    /// </summary>
    [Fact]
    public void EveryMarker_ReferencesCanonicalPolicyConstants()
    {
        var valid = new HashSet<string>(StringComparer.Ordinal);
        valid.UnionWith(JobProfilePolicyNames);
        valid.UnionWith(PositionDescriptionCatalogPolicyNames);
        valid.UnionWith(PositionSlotPolicyNames);
        valid.UnionWith(PersonnelFilePolicyNames);
        valid.UnionWith(CostCenterPolicyNames);
        valid.UnionWith(LocationPolicyNames);
        valid.UnionWith(LegalRepresentativePolicyNames);
        valid.UnionWith(OrgUnitPolicyNames);
        valid.UnionWith(OrgStructureCatalogPolicyNames);

        var invalid = Controllers()
            .Where(static entry => entry.Marker is not null)
            .SelectMany(static entry => new[]
            {
                (entry.Controller.Name, Policy: entry.Marker!.ReadPolicy),
                (entry.Controller.Name, Policy: entry.Marker!.ManagePolicy),
            })
            .Where(pair => !valid.Contains(pair.Policy))
            .Select(static pair => $"{pair.Name} -> '{pair.Policy}'")
            .OrderBy(static entry => entry, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            invalid.Length == 0,
            "[AuthorizationPolicySet] must reference a constant from JobProfilePolicies / " +
            "PositionDescriptionCatalogPolicies / PositionSlotPolicies / PersonnelFilePolicies / " +
            "CostCenterPolicies / LocationPolicies / LegalRepresentativePolicies / OrgUnitPolicies. " +
            "Offending:\n  " +
            string.Join("\n  ", invalid));
    }
}
