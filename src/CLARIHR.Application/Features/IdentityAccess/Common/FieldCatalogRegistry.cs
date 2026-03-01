namespace CLARIHR.Application.Features.IdentityAccess.Common;

public static class FieldCatalogRegistry
{
    public static readonly IReadOnlyCollection<FieldCatalogDefinition> Definitions =
    [
        new(
            CompanyUserFieldKeys.Id,
            CompanyUserFieldKeys.ResourceKey,
            "Id",
            "Internal Id",
            "guid",
            IsConfigurable: false,
            IsSensitive: false),
        new(
            CompanyUserFieldKeys.Email,
            CompanyUserFieldKeys.ResourceKey,
            "Email",
            "Email",
            "string",
            IsConfigurable: true,
            IsSensitive: true),
        new(
            CompanyUserFieldKeys.FirstName,
            CompanyUserFieldKeys.ResourceKey,
            "FirstName",
            "First Name",
            "string",
            IsConfigurable: true,
            IsSensitive: false),
        new(
            CompanyUserFieldKeys.LastName,
            CompanyUserFieldKeys.ResourceKey,
            "LastName",
            "Last Name",
            "string",
            IsConfigurable: true,
            IsSensitive: false),
        new(
            CompanyUserFieldKeys.Role,
            CompanyUserFieldKeys.ResourceKey,
            "Role",
            "Role",
            "lookup",
            IsConfigurable: true,
            IsSensitive: false),
        new(
            CompanyUserFieldKeys.Status,
            CompanyUserFieldKeys.ResourceKey,
            "Status",
            "Status",
            "enum",
            IsConfigurable: true,
            IsSensitive: false)
    ];

    private static readonly IReadOnlyDictionary<string, FieldCatalogDefinition> ByFieldKey =
        Definitions.ToDictionary(static definition => definition.FieldKey, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<FieldCatalogDefinition>> ByResourceKey =
        Definitions
            .GroupBy(static definition => definition.ResourceKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyCollection<FieldCatalogDefinition>)group
                    .OrderBy(static definition => definition.DisplayName)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

    public static bool TryGetField(string fieldKey, out FieldCatalogDefinition definition) =>
        ByFieldKey.TryGetValue(fieldKey, out definition!);

    public static bool TryGetResource(string resourceKey, out IReadOnlyCollection<FieldCatalogDefinition> definitions) =>
        ByResourceKey.TryGetValue(resourceKey, out definitions!);

    public static IReadOnlyCollection<FieldCatalogDefinition> GetResourceFields(string resourceKey) =>
        ByResourceKey.TryGetValue(resourceKey, out var definitions)
            ? definitions
            : [];

    public static IReadOnlyCollection<FieldCatalogDefinition> GetConfigurableResourceFields(string resourceKey) =>
        GetResourceFields(resourceKey)
            .Where(static definition => definition.IsConfigurable)
            .ToArray();
}

public static class CompanyUserFieldKeys
{
    public const string ResourceKey = "RBAC_USERS";
    public const string Id = "RBAC_USERS.ID";
    public const string Email = "RBAC_USERS.EMAIL";
    public const string FirstName = "RBAC_USERS.FIRST_NAME";
    public const string LastName = "RBAC_USERS.LAST_NAME";
    public const string Role = "RBAC_USERS.ROLE";
    public const string Status = "RBAC_USERS.STATUS";
}
