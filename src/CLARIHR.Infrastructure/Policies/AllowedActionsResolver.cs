using System.Collections.Concurrent;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Infrastructure.Policies;

/// <summary>
/// Resolves <see cref="AllowedActionsResponse"/> from the current user's JWT permission
/// claims (no DB access) and the response DTO's state. Per-action checks reuse the exact
/// evaluators the server enforces with (<see cref="RbacAuthorizationEvaluator"/> for RBAC;
/// precise permission codes for policy resources), so advertised capabilities are a subset
/// of what is enforced — never a superset.
/// </summary>
internal sealed class AllowedActionsResolver(
    ICurrentUserService currentUserService,
    IResourceActionPolicyService policyService) : IAllowedActionsResolver
{
    public AllowedActionsResponse? Resolve(string resourceKey, object? dto)
    {
        if (string.IsNullOrWhiteSpace(resourceKey) || !AllowedActionsRegistry.TryGet(resourceKey, out var definition))
        {
            // Fail-closed: unregistered resource → emit nothing.
            return null;
        }

        var permissions = currentUserService.Permissions;
        var granted = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

        bool viewAllowed;
        bool updateAllowed;
        bool deleteAllowed;
        bool createAllowed;
        bool lifecycleAllowed; // archive / activate / inactivate share the manage gate

        if (definition.AuthModel == ResourceAuthModel.Rbac && definition.Screen is { } screen)
        {
            viewAllowed = RbacAuthorizationEvaluator.IsAllowed(permissions, screen, RbacPermissionAction.Read);
            updateAllowed = RbacAuthorizationEvaluator.IsAllowed(permissions, screen, RbacPermissionAction.Update);
            deleteAllowed = RbacAuthorizationEvaluator.IsAllowed(permissions, screen, RbacPermissionAction.Delete);
            createAllowed = RbacAuthorizationEvaluator.IsAllowed(permissions, screen, RbacPermissionAction.Create);
            lifecycleAllowed = updateAllowed;
        }
        else
        {
            var canManage = HasAny(granted, definition.ManageCodes);
            updateAllowed = canManage;
            lifecycleAllowed = canManage;
            deleteAllowed = definition.DeleteCodes is not null ? HasAny(granted, definition.DeleteCodes) : canManage;
            createAllowed = definition.CreateCodes is not null ? HasAny(granted, definition.CreateCodes) : canManage;
            viewAllowed = definition.ViewCodes is null || HasAny(granted, definition.ViewCodes);
        }

        var state = ExtractState(definition, dto);

        var context = new ResourceActionContext(
            definition.ResourceKey,
            state.State,
            state.IsActive,
            IsSystem: state.IsSystem,
            HasDependencies: state.HasDependencies,
            SupportsEdit: true,
            EditAllowed: updateAllowed,
            SupportsDelete: definition.SupportsDelete,
            DeleteAllowed: deleteAllowed,
            SupportsArchive: definition.SupportsArchive,
            ArchiveAllowed: lifecycleAllowed,
            SupportsActivate: definition.SupportsActivate,
            ActivateAllowed: lifecycleAllowed,
            SupportsInactivate: definition.SupportsInactivate,
            InactivateAllowed: lifecycleAllowed,
            NonEditableStates: state.NonEditableStates,
            SupportsCreate: definition.SupportsCreate,
            CreateAllowed: createAllowed,
            SupportsView: true,
            ViewAllowed: viewAllowed,
            SupportsPublish: definition.SupportsPublish,
            PublishAllowed: lifecycleAllowed,
            PublishableStates: definition.PublishableStates);

        var result = policyService.Evaluate(context);
        return result with { ActionPermissions = BuildActionPermissions(definition, result) };
    }

    private static bool HasAny(HashSet<string> granted, IReadOnlyList<string>? codes) =>
        codes is not null && codes.Any(granted.Contains);

    private static readonly ConcurrentDictionary<(Type Type, string Property), Func<object, bool>?> BoolReaders = new();

    private static ResourceState ExtractState(ResourceActionDefinition definition, object? dto)
    {
        if (definition.StateExtractor is not null)
        {
            return definition.StateExtractor(dto);
        }

        if (dto is null)
        {
            return new ResourceState();
        }

        // Fall back to conventional public bool properties on the DTO (cached per type+name).
        var isActive = dto is IHasActivationState activation
            ? activation.IsActive
            : ReadBool(dto, "IsActive", defaultValue: true);
        var isSystem = ReadBool(dto, "IsSystem", defaultValue: false)
            || ReadBool(dto, "IsSystemRole", defaultValue: false);

        return new ResourceState(IsActive: isActive, IsSystem: isSystem);
    }

    private static bool ReadBool(object dto, string propertyName, bool defaultValue)
    {
        var reader = BoolReaders.GetOrAdd((dto.GetType(), propertyName), static key =>
        {
            var property = key.Type.GetProperty(key.Property);
            return property is not null && property.PropertyType == typeof(bool)
                ? instance => (bool)property.GetValue(instance)!
                : null;
        });

        return reader is null ? defaultValue : reader(dto);
    }

    private static IReadOnlyCollection<AllowedActionPermissionResponse> BuildActionPermissions(
        ResourceActionDefinition definition,
        AllowedActionsResponse result)
    {
        var list = new List<AllowedActionPermissionResponse>(8);

        Add("View", supported: true, allowed: result.CanView, Code(definition, RbacPermissionAction.Read, isCreate: false));
        Add("Create", definition.SupportsCreate, result.CanCreate, Code(definition, RbacPermissionAction.Create, isCreate: true));
        Add("Update", supported: true, allowed: result.CanEdit, Code(definition, RbacPermissionAction.Update, isCreate: false));
        Add("Delete", definition.SupportsDelete, result.CanDelete, Code(definition, RbacPermissionAction.Delete, isCreate: false));
        Add("Archive", definition.SupportsArchive, result.CanArchive, Code(definition, RbacPermissionAction.Update, isCreate: false));
        Add("Activate", definition.SupportsActivate, result.CanActivate, Code(definition, RbacPermissionAction.Update, isCreate: false));
        Add("Inactivate", definition.SupportsInactivate, result.CanInactivate, Code(definition, RbacPermissionAction.Update, isCreate: false));
        Add("Publish", definition.SupportsPublish, result.CanPublish, Code(definition, RbacPermissionAction.Update, isCreate: false));

        return list;

        void Add(string action, bool supported, bool allowed, string code)
        {
            if (supported)
            {
                list.Add(new AllowedActionPermissionResponse(action, code, allowed, []));
            }
        }
    }

    private static string Code(ResourceActionDefinition definition, RbacPermissionAction action, bool isCreate)
    {
        if (definition.AuthModel == ResourceAuthModel.Rbac && definition.Screen is { } screen)
        {
            return PermissionMatrixCatalog.BuildPermissionCode(screen, action);
        }

        if (isCreate && definition.CreateCodes is { Count: > 0 } createCodes)
        {
            return createCodes[0];
        }

        return definition.ManageCodes.Count > 0 ? definition.ManageCodes[0] : string.Empty;
    }
}
