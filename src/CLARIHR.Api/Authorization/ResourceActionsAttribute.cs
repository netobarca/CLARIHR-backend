namespace CLARIHR.Api.Authorization;

/// <summary>
/// Declares the resource key the centralized AllowedActionsResultFilter uses to
/// populate <c>allowedActions</c> on this controller's PUT/PATCH/GET responses.
/// Co-located with the controller (single, auditable source of truth that cannot
/// drift). The value must match a <c>*PermissionCodes.ResourceKey</c> constant
/// registered in <c>AllowedActionsRegistry</c>; an unregistered key is fail-closed
/// (no <c>allowedActions</c> is emitted). Class-level only: it inherits to every action.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ResourceActionsAttribute(string resourceKey) : Attribute
{
    public string ResourceKey { get; } = resourceKey;
}
