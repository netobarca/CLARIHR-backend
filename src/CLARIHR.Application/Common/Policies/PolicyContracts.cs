namespace CLARIHR.Application.Common.Policies;

public sealed record AllowedActionsResponse(
    bool CanEdit,
    bool CanDelete,
    bool CanArchive,
    bool CanActivate,
    bool CanInactivate,
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

public sealed record ReportCapabilitiesResponse(
    string ResourceKey,
    bool SupportsPrint,
    bool SupportsExport,
    IReadOnlyCollection<string> SupportedTableFormats,
    IReadOnlyCollection<string> SupportedGraphFormats);

public sealed record ReportCapabilityDefinition(
    ReportCapabilitiesResponse Capabilities,
    string ReadPermissionCode,
    string AdminPermissionCode,
    string? PrintPermissionCode,
    string? ExportPermissionCode);
