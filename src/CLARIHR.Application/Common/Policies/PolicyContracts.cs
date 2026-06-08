namespace CLARIHR.Application.Common.Policies;

public sealed record AllowedActionsResponse(
    bool CanEdit,
    bool CanDelete,
    bool CanArchive,
    bool CanActivate,
    bool CanInactivate,
    IReadOnlyCollection<string> Reasons,
    bool CanSubmit = false,
    bool CanApprove = false,
    bool CanReject = false,
    bool CanCancel = false,
    bool CanPublish = false,
    bool CanFinalize = false,
    IReadOnlyCollection<AllowedActionPermissionResponse>? ActionPermissions = null)
{
    public IReadOnlyCollection<AllowedActionPermissionResponse> ActionPermissions { get; init; } =
        ActionPermissions ?? [];
}

public sealed record AllowedActionPermissionResponse(
    string Action,
    string PermissionCode,
    bool Allowed,
    IReadOnlyCollection<string> Reasons);

public sealed record ResourceActionContext(
    string ResourceKey,
    string? State,
    bool IsActive,
    bool IsSystem = false,
    bool HasDependencies = false,
    bool SupportsEdit = true,
    bool EditAllowed = true,
    bool SupportsDelete = false,
    bool DeleteAllowed = true,
    bool SupportsArchive = false,
    bool ArchiveAllowed = true,
    bool SupportsActivate = false,
    bool ActivateAllowed = true,
    bool SupportsInactivate = false,
    bool InactivateAllowed = true,
    IReadOnlyCollection<string>? NonEditableStates = null);
