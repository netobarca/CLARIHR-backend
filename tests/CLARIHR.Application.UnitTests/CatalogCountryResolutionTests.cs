using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// H-21 — the rule that stopped a missing query parameter from looking like an unseeded database. The explicit code
/// wins, the tenant's country is the fallback, and when neither resolves the answer is a 400 that names the field
/// instead of an empty list that names nothing.
/// </summary>
public sealed class CatalogCountryResolutionTests
{
    [Fact]
    public async Task ResolveAsync_WithExplicitCountry_UsesItAndDoesNotTouchTheTenant()
    {
        var repository = new StubRepository(activeCountries: ["SV", "CR"], tenantCountryCode: "SV");

        var result = await CatalogCountryResolution.ResolveAsync(
            "cr", new StubTenantContext(Guid.NewGuid()), repository, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("CR", result.Value);

        // The explicit code is what lets the backoffice read another country; asking the tenant would defeat it.
        Assert.Equal(0, repository.TenantCountryLookups);
    }

    [Fact]
    public async Task ResolveAsync_WithoutCountry_FallsBackToTheTenantCountry()
    {
        var repository = new StubRepository(activeCountries: ["SV"], tenantCountryCode: "sv");

        var result = await CatalogCountryResolution.ResolveAsync(
            null, new StubTenantContext(Guid.NewGuid()), repository, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SV", result.Value);
        Assert.Equal(1, repository.TenantCountryLookups);
    }

    /// <summary>Whitespace is not a country: it is the same as omitting the parameter.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveAsync_WithBlankCountry_FallsBackToTheTenantCountry(string countryCode)
    {
        var repository = new StubRepository(activeCountries: ["SV"], tenantCountryCode: "SV");

        var result = await CatalogCountryResolution.ResolveAsync(
            countryCode, new StubTenantContext(Guid.NewGuid()), repository, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SV", result.Value);
    }

    /// <summary>The company-less onboarding caller: no parameter and no tenant to fall back on.</summary>
    [Fact]
    public async Task ResolveAsync_WithoutCountryAndWithoutTenant_Fails()
    {
        var repository = new StubRepository(activeCountries: ["SV"], tenantCountryCode: "SV");

        var result = await CatalogCountryResolution.ResolveAsync(
            null, new StubTenantContext(null), repository, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogCountryResolution.CountryRequiredCode, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.True(result.Error.ValidationErrors?.ContainsKey("countryCode"));
    }

    /// <summary>Authenticated, but the tenant has no company row (or a company without a country).</summary>
    [Fact]
    public async Task ResolveAsync_WhenTheTenantHasNoCountry_Fails()
    {
        var repository = new StubRepository(activeCountries: ["SV"], tenantCountryCode: null);

        var result = await CatalogCountryResolution.ResolveAsync(
            null, new StubTenantContext(Guid.NewGuid()), repository, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogCountryResolution.CountryRequiredCode, result.Error.Code);
    }

    /// <summary>
    /// An ISO-shaped code matching no active country is a bad parameter, not an empty catalog — the second silent
    /// failure of the finding, and the one that survived even in the family that already required the code.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WithUnknownCountry_Fails()
    {
        var repository = new StubRepository(activeCountries: ["SV"], tenantCountryCode: "SV");

        var result = await CatalogCountryResolution.ResolveAsync(
            "XX", new StubTenantContext(Guid.NewGuid()), repository, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogCountryResolution.CountryUnknownCode, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    // ── The scope classification both layers share ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Country")]
    [InlineData("CurriculumEducationStatus")]
    [InlineData("CurriculumStudyType")]
    [InlineData("CurriculumEducationLevel")]
    [InlineData("CurriculumShift")]
    [InlineData("CurriculumModality")]
    [InlineData("FileDocumentType")]
    public void SystemScopedCategories_DoNotRequireACountry(string category)
    {
        Assert.True(GeneralCatalogScopes.IsSystemScoped(category));
        Assert.False(GeneralCatalogScopes.RequiresCountry(category));
    }

    /// <summary>
    /// The set is closed on purpose: everything else is country-scoped. `CurriculumCareer` is the trap — it sits
    /// among the education catalogs but its rows ARE per country.
    /// </summary>
    [Theory]
    [InlineData("CurriculumCareer")]
    [InlineData("Bank")]
    [InlineData("CompensationConceptType")]
    [InlineData("SettlementConcept")]
    [InlineData("PayrollType")]
    public void EverythingElse_RequiresACountry(string category)
    {
        Assert.True(GeneralCatalogScopes.RequiresCountry(category));
    }

    private sealed class StubTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class StubRepository(string[] activeCountries, string? tenantCountryCode) : ICatalogCountryLookup
    {
        public int TenantCountryLookups { get; private set; }

        public Task<bool> CountryCodeIsActiveAsync(string countryCode, CancellationToken cancellationToken) =>
            Task.FromResult(activeCountries.Contains(countryCode.Trim().ToUpperInvariant(), StringComparer.Ordinal));

        public Task<string?> GetCompanyCountryCodeAsync(Guid companyId, CancellationToken cancellationToken)
        {
            TenantCountryLookups++;
            return Task.FromResult(tenantCountryCode);
        }
    }
}
