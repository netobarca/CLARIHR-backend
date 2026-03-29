using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.IdentityAccess;

public sealed class IamPermission : TenantEntity
{
    private readonly List<IamRolePermissionAssignment> _roleAssignments = [];

    private IamPermission()
    {
    }

    private IamPermission(
        Guid publicId,
        string code,
        string name,
        string? description,
        string module,
        string screen,
        IamPermissionKind kind,
        string? action,
        string? fieldName,
        IamFieldAccessLevel? fieldAccess)
    {
        PublicId = publicId;
        Code = IdentityNormalization.Normalize(code);
        NormalizedCode = Code;
        Name = IdentityNormalization.Clean(name, nameof(name));
        Description = IdentityNormalization.CleanOptional(description);
        Module = IdentityNormalization.Clean(module, nameof(module));
        NormalizedModule = IdentityNormalization.Normalize(module);
        Screen = IdentityNormalization.Clean(screen, nameof(screen));
        NormalizedScreen = IdentityNormalization.Normalize(screen);
        Kind = kind;
        Action = IdentityNormalization.CleanOptional(action);
        NormalizedAction = Action is null ? null : IdentityNormalization.Normalize(Action);
        FieldName = IdentityNormalization.CleanOptional(fieldName);
        NormalizedFieldName = FieldName is null ? null : IdentityNormalization.Normalize(FieldName);
        FieldAccess = fieldAccess;
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string Module { get; private set; } = string.Empty;

    public string NormalizedModule { get; private set; } = string.Empty;

    public string Screen { get; private set; } = string.Empty;

    public string NormalizedScreen { get; private set; } = string.Empty;

    public IamPermissionKind Kind { get; private set; }

    public string? Action { get; private set; }

    public string? NormalizedAction { get; private set; }

    public string? FieldName { get; private set; }

    public string? NormalizedFieldName { get; private set; }

    public IamFieldAccessLevel? FieldAccess { get; private set; }

    public IReadOnlyCollection<IamRolePermissionAssignment> RoleAssignments => _roleAssignments;

    public static IamPermission CreateScreenAction(
        string code,
        string name,
        string? description,
        string module,
        string screen,
        string action)
    {
        return new IamPermission(
            publicId: Guid.NewGuid(),
            code: code,
            name: name,
            description: description,
            module: module,
            screen: screen,
            kind: IamPermissionKind.ScreenAction,
            action: action,
            fieldName: null,
            fieldAccess: null);
    }

    public static IamPermission CreateFieldPermission(
        string code,
        string name,
        string? description,
        string module,
        string screen,
        string fieldName,
        IamFieldAccessLevel fieldAccess)
    {
        return new IamPermission(
            publicId: Guid.NewGuid(),
            code: code,
            name: name,
            description: description,
            module: module,
            screen: screen,
            kind: IamPermissionKind.Field,
            action: null,
            fieldName: fieldName,
            fieldAccess: fieldAccess);
    }
}
