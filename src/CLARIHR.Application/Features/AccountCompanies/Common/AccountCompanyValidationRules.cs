namespace CLARIHR.Application.Features.AccountCompanies.Common;

/// <summary>
/// Single source of truth for AccountCompanies list/validation bounds and unique-constraint names,
/// shared by the FluentValidation validators, the controller-boundary <c>[Range]</c> attributes, the
/// <c>AccountCompanyPaginationGuardrailsTests</c> guardrail (AC-6), and the duplicate-slug race backstop
/// (AC-4) — so a rule cannot drift apart from the guardrail or the DB constraint it mirrors.
/// </summary>
public static class AccountCompanyValidationRules
{
    /// <summary>Upper bound for the owned-companies list <c>pageSize</c> (mirrors the handler validator).</summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Name of the unique index on <c>companies.slug</c> (see <c>CompanyConfiguration</c>:
    /// <c>uq_companies__slug</c>). Single-sourced so the AC-4 duplicate-slug race backstop in the create
    /// handler maps the 23505 to an internal retry instead of letting it escape as an HTTP 500.
    /// </summary>
    public const string SlugUniqueConstraintName = "uq_companies__slug";
}

/// <summary>
/// Maps a <see cref="CLARIHR.Application.Abstractions.Persistence.UniqueConstraintViolationException"/> to the
/// AccountCompanies slug constraint, mirroring the CostCenters/PositionSlots constraint-violation guards.
/// </summary>
public static class AccountCompanyConstraintViolations
{
    public static bool IsSlugConflict(string? constraintName) =>
        string.Equals(constraintName, AccountCompanyValidationRules.SlugUniqueConstraintName, StringComparison.Ordinal);
}
