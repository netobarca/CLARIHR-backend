using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Common.Policies;

/// <summary>Authorization model a resource uses to gate its actions.</summary>
public enum ResourceAuthModel
{
    /// <summary>RBAC screen/action matrix; per-action checks delegate to <see cref="RbacAuthorizationEvaluator"/>.</summary>
    Rbac,

    /// <summary>Feature policy/code model; capabilities are gated by precise permission codes.</summary>
    Policy,
}

/// <summary>
/// Record state needed to refine permission-based capabilities (extracted from the
/// already-loaded response DTO — no DB access). All fields are optional/defaulted so
/// an unwired resource is conservative (active, no dependencies, not system).
/// </summary>
public readonly record struct ResourceState(
    string? State = null,
    bool IsActive = true,
    bool IsSystem = false,
    bool HasDependencies = false,
    IReadOnlyCollection<string>? NonEditableStates = null);

/// <summary>
/// Declarative capability definition for one resource. The permission codes MUST mirror
/// the codes the resource's real authorization gate enforces, so advertised capabilities
/// can never exceed enforcement (see <see cref="IdentityAccess.Common.RbacAuthorizationEvaluator"/>
/// for RBAC and each feature's <c>*PermissionCodes</c> for policy resources).
/// </summary>
public sealed class ResourceActionDefinition
{
    public required string ResourceKey { get; init; }

    public required ResourceAuthModel AuthModel { get; init; }

    /// <summary>RBAC screen (required when <see cref="AuthModel"/> is <see cref="ResourceAuthModel.Rbac"/>).</summary>
    public RbacPermissionScreen? Screen { get; init; }

    /// <summary>Codes that grant management (edit/delete/archive/activate/inactivate) for a policy resource.</summary>
    public IReadOnlyList<string> ManageCodes { get; init; } = [];

    /// <summary>Codes that grant create. Defaults to <see cref="ManageCodes"/> when null.</summary>
    public IReadOnlyList<string>? CreateCodes { get; init; }

    /// <summary>Codes that grant read/view. Defaults to "always allowed on a successful read" when null.</summary>
    public IReadOnlyList<string>? ViewCodes { get; init; }

    /// <summary>Codes that grant delete. Defaults to <see cref="ManageCodes"/> when null.</summary>
    public IReadOnlyList<string>? DeleteCodes { get; init; }

    public bool SupportsCreate { get; init; } = true;

    public bool SupportsDelete { get; init; }

    public bool SupportsArchive { get; init; }

    public bool SupportsActivate { get; init; }

    public bool SupportsInactivate { get; init; }

    /// <summary>Reads record state (active/system/dependencies/non-editable states) from the response DTO.</summary>
    public Func<object?, ResourceState>? StateExtractor { get; init; }
}

/// <summary>
/// Central, in-memory registry mapping a <c>resourceKey</c> to its capability definition.
/// Lookups are O(1) and allocation-free. An unregistered key is fail-closed: the resolver
/// returns no <c>allowedActions</c>. Resources are wired incrementally (see rollout phases).
/// </summary>
public static class AllowedActionsRegistry
{
    private static readonly IReadOnlyDictionary<string, ResourceActionDefinition> Definitions =
        BuildDefinitions().ToDictionary(
            static definition => definition.ResourceKey,
            StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string resourceKey, out ResourceActionDefinition definition) =>
        Definitions.TryGetValue(resourceKey, out definition!);

    private static IEnumerable<ResourceActionDefinition> BuildDefinitions()
    {
        // RBAC resources — per-action checks delegate to RbacAuthorizationEvaluator
        // (honors the ACCESS gate + manage override).
        yield return Rbac(CompanyUserFieldKeys.ResourceKey, RbacPermissionScreen.Users, CompanyUserStateExtractor);

        // Policy resources — capabilities gated by precise permission codes. Activation state is
        // read from the DTO's `IsActive` (IHasActivationState or a conventional bool property).
        yield return Policy(
            CompetencyFrameworkPermissionCodes.ResourceKey,
            CompetencyFrameworkPermissionCodes.Read,
            CompetencyFrameworkPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.ManageAdministration);
        yield return Policy(
            LocationPermissionCodes.ResourceKey,
            LocationPermissionCodes.Read,
            LocationPermissionCodes.Admin,
            LocationPermissionCodes.ManageAdministration);
        yield return Policy(
            CostCenterPermissionCodes.ResourceKey,
            CostCenterPermissionCodes.Read,
            CostCenterPermissionCodes.Admin,
            CostCenterPermissionCodes.ManageAdministration);
        yield return Policy(
            LegalRepresentativePermissionCodes.ResourceKey,
            LegalRepresentativePermissionCodes.Read,
            LegalRepresentativePermissionCodes.Admin,
            LegalRepresentativePermissionCodes.ManageAdministration);
        yield return Policy(
            OrgUnitPermissionCodes.ResourceKey,
            OrgUnitPermissionCodes.Read,
            OrgUnitPermissionCodes.Admin,
            OrgUnitPermissionCodes.ManageAdministration);
        yield return Policy(
            OrgStructureCatalogPermissionCodes.ResourceKey,
            OrgStructureCatalogPermissionCodes.Read,
            OrgStructureCatalogPermissionCodes.Admin,
            OrgStructureCatalogPermissionCodes.ManageAdministration);
        yield return Policy(
            PositionDescriptionCatalogPermissionCodes.ResourceKey,
            PositionDescriptionCatalogPermissionCodes.Read,
            PositionDescriptionCatalogPermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.ManageAdministration,
            supportsActivate: false,
            supportsInactivate: false);

        // JobProfiles family (profiles + sub-resources) — gated by Admin.
        yield return Policy(
            JobProfilePermissionCodes.ResourceKey,
            JobProfilePermissionCodes.Read,
            JobProfilePermissionCodes.Admin,
            JobProfilePermissionCodes.ManageAdministration,
            supportsActivate: false,
            supportsInactivate: false);

        // Job catalogs — writes gated by the catalog-specific code (NOT JobProfiles.Admin),
        // mirroring the JobProfiles.ManageCatalogs policy so we never over-report.
        yield return Policy(
            "JOB_CATALOGS",
            JobProfilePermissionCodes.Read,
            JobProfilePermissionCodes.CatalogAdmin,
            JobProfilePermissionCodes.ManageAdministration,
            supportsActivate: false,
            supportsInactivate: false);

        yield return Policy(
            PositionSlotPermissionCodes.ResourceKey,
            PositionSlotPermissionCodes.Read,
            PositionSlotPermissionCodes.Admin,
            PositionSlotPermissionCodes.ManageAdministration,
            supportsActivate: false,
            supportsInactivate: false);

        // Job-profile competency matrix — a single full-replace resource (no activate/inactivate),
        // gated by the CompetencyFramework codes.
        yield return Policy(
            "JOB_PROFILE_COMPETENCY_MATRIX",
            CompetencyFrameworkPermissionCodes.Read,
            CompetencyFrameworkPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.ManageAdministration,
            supportsActivate: false,
            supportsInactivate: false);

        // RBAC roles administration — create/update/delete (no activate/inactivate). System roles are
        // flagged via IsSystemRole on the DTO (read generically), so edit/delete fail-close for them.
        yield return Rbac(
            "RBAC_ROLES",
            RbacPermissionScreen.Roles,
            supportsDelete: true,
            supportsActivate: false,
            supportsInactivate: false);

        // Location hierarchy config — a singleton config edited via PUT (no activate/inactivate),
        // gated by the Locations codes.
        yield return Policy(
            "LOCATION_HIERARCHY",
            LocationPermissionCodes.Read,
            LocationPermissionCodes.Admin,
            LocationPermissionCodes.ManageAdministration,
            supportsActivate: false,
            supportsInactivate: false);
    }

    private static ResourceState CompanyUserStateExtractor(object? dto) => dto switch
    {
        CompanyUserResponse user => new ResourceState(user.Status?.ToString(), user.Status == UserStatus.Active),
        CompanyUserSummaryResponse user => new ResourceState(user.Status?.ToString(), user.Status == UserStatus.Active),
        _ => new ResourceState(),
    };

    private static ResourceActionDefinition Policy(
        string resourceKey,
        string readCode,
        string adminCode,
        string manageAdministrationCode,
        bool supportsDelete = false,
        bool supportsArchive = false,
        bool supportsActivate = true,
        bool supportsInactivate = true) =>
        new()
        {
            ResourceKey = resourceKey,
            AuthModel = ResourceAuthModel.Policy,
            ManageCodes = [adminCode, manageAdministrationCode],
            ViewCodes = [readCode, adminCode, manageAdministrationCode],
            SupportsCreate = true,
            SupportsDelete = supportsDelete,
            SupportsArchive = supportsArchive,
            SupportsActivate = supportsActivate,
            SupportsInactivate = supportsInactivate,
        };

    private static ResourceActionDefinition Rbac(
        string resourceKey,
        RbacPermissionScreen screen,
        Func<object?, ResourceState>? stateExtractor = null,
        bool supportsDelete = false,
        bool supportsActivate = true,
        bool supportsInactivate = true) =>
        new()
        {
            ResourceKey = resourceKey,
            AuthModel = ResourceAuthModel.Rbac,
            Screen = screen,
            SupportsCreate = true,
            SupportsDelete = supportsDelete,
            SupportsActivate = supportsActivate,
            SupportsInactivate = supportsInactivate,
            StateExtractor = stateExtractor,
        };
}
