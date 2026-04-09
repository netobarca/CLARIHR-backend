namespace CLARIHR.Domain.Companies;

public static class AuthorizationScopeTypes
{
    public const string Company = "COMPANY";
    public const string Location = "LOCATION";
    public const string Department = "DEPARTMENT";
    public const string Employee = "EMPLOYEE";
    public const string Country = "COUNTRY";
}

public sealed record AuthorizationScopeDefinition(
    string ScopeType,
    string DisplayName,
    string Description);

public static class AuthorizationScopeCatalog
{
    public static readonly IReadOnlyCollection<AuthorizationScopeDefinition> All =
    [
        new(AuthorizationScopeTypes.Company, "Company", "Applies to the full active company context."),
        new(AuthorizationScopeTypes.Location, "Location", "Applies to a constrained set of company locations."),
        new(AuthorizationScopeTypes.Department, "Department", "Applies to a constrained set of departments or org units."),
        new(AuthorizationScopeTypes.Employee, "Employee", "Applies to a constrained set of employees or personnel files."),
        new(AuthorizationScopeTypes.Country, "Country", "Applies to country constrained resources.")
    ];

    private static readonly IReadOnlyDictionary<string, AuthorizationScopeDefinition> ByScopeType =
        All.ToDictionary(static definition => definition.ScopeType, static definition => definition, StringComparer.Ordinal);

    public static bool IsKnown(string scopeType)
    {
        if (string.IsNullOrWhiteSpace(scopeType))
        {
            return false;
        }

        return ByScopeType.ContainsKey(scopeType.Trim().ToUpperInvariant());
    }

    public static AuthorizationScopeDefinition Get(string scopeType)
    {
        var normalizedScopeType = CompanyNormalization.NormalizeModuleKey(scopeType);
        return ByScopeType.TryGetValue(normalizedScopeType, out var definition)
            ? definition
            : throw new ArgumentException($"Unknown authorization scope type '{normalizedScopeType}'.", nameof(scopeType));
    }
}
