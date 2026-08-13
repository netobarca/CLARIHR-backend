using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// H-21 — which catalog categories of the company-less surface are the SAME for every country. They are served
/// during onboarding, when the caller may have no company at all, so they must never require a country.
/// <para>
/// The set lives here, in the application layer, because both the query handler (to decide whether it has to
/// resolve a country before calling the repository) and the repository (to pick its system branch) must agree on
/// it. Two independent lists would drift, and the drift is invisible: a country-scoped category misfiled as
/// system-scoped simply returns an empty list forever.
/// </para>
/// </summary>
public static class GeneralCatalogScopes
{
    public static readonly IReadOnlySet<string> SystemScoped = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PersonnelCurriculumCatalogCategories.Country,
        PersonnelCurriculumCatalogCategories.EducationStatus,
        PersonnelCurriculumCatalogCategories.StudyType,
        PersonnelCurriculumCatalogCategories.EducationLevel,
        PersonnelCurriculumCatalogCategories.Shift,
        PersonnelCurriculumCatalogCategories.Modality,
        PersonnelCurriculumCatalogCategories.FileDocumentType,
    };

    public static bool IsSystemScoped(string? category) =>
        !string.IsNullOrWhiteSpace(category) && SystemScoped.Contains(category.Trim());

    /// <summary>Everything that is not global is scoped to a country and therefore needs one resolved.</summary>
    public static bool RequiresCountry(string? category) => !IsSystemScoped(category);
}

/// <summary>
/// H-21 — resolves the country a catalog read is scoped to.
/// <para>
/// Country-scoped catalog reads used to answer <c>200 []</c> when <c>countryCode</c> was missing or unknown, which
/// is indistinguishable from "this catalog was never seeded" — and §5.6 of the test playbook teaches the reader
/// that an empty concept catalog means the payroll engine cannot calculate. A missing query parameter therefore
/// read as a broken environment. Worse, the country was never really unknown: the token carries the tenant, the
/// tenant IS the company (<c>companies.public_id == tenant_id</c>) and the company has a country code.
/// </para>
/// <para>
/// So: the explicit code wins (it is what lets the backoffice and the onboarding surface read another country),
/// the tenant's country is the fallback, and when neither resolves the answer is a <c>400</c> naming the field.
/// An empty list now means one thing only — that country's catalog is empty.
/// </para>
/// </summary>
public static class CatalogCountryResolution
{
    public const string CountryRequiredCode = "CATALOG_COUNTRY_REQUIRED";
    public const string CountryUnknownCode = "CATALOG_COUNTRY_UNKNOWN";

    public static async Task<Result<string>> ResolveAsync(
        string? requestedCountryCode,
        ITenantContext tenantContext,
        ICatalogCountryLookup repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(repository);

        var explicitCode = Normalize(requestedCountryCode);
        if (explicitCode is not null)
        {
            return await repository.CountryCodeIsActiveAsync(explicitCode, cancellationToken)
                ? Result<string>.Success(explicitCode)
                : Result<string>.Failure(CountryUnknown(explicitCode));
        }

        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result<string>.Failure(CountryRequired());
        }

        var tenantCountryCode = Normalize(await repository.GetCompanyCountryCodeAsync(tenantId, cancellationToken));
        if (tenantCountryCode is null)
        {
            // Authenticated, but with no company behind the tenant (or a company with no country): the caller has
            // to say which country, exactly as the company-less onboarding surface always could.
            return Result<string>.Failure(CountryRequired());
        }

        return await repository.CountryCodeIsActiveAsync(tenantCountryCode, cancellationToken)
            ? Result<string>.Success(tenantCountryCode)
            : Result<string>.Failure(CountryUnknown(tenantCountryCode));
    }

    private static string? Normalize(string? countryCode) =>
        string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant();

    private static Error CountryRequired() =>
        new(
            CountryRequiredCode,
            "The country could not be resolved from the current tenant, so 'countryCode' is required.",
            ErrorType.Validation,
            new Dictionary<string, string[]>
            {
                ["countryCode"] = ["The country could not be resolved from the current tenant, so 'countryCode' is required."]
            });

    private static Error CountryUnknown(string countryCode) =>
        new(
            CountryUnknownCode,
            "The country code does not match any active country.",
            ErrorType.Validation,
            new Dictionary<string, string[]>
            {
                ["countryCode"] = [$"Country code '{countryCode}' does not match any active country."]
            });
}
