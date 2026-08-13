namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>
/// H-21 — the two questions a country-scoped catalog read has to ask before it can answer: does this country code
/// exist, and what country is the current tenant in.
/// <para>
/// It is split out of <see cref="IPersonnelFileRepository"/> (which extends it, so nothing else had to change) so
/// the resolution rule can be exercised without standing up the whole personnel-file repository. Narrow enough to
/// stub, which is what a rule that decides between "here is the catalog" and "400" deserves.
/// </para>
/// </summary>
public interface ICatalogCountryLookup
{
    /// <summary>The country code of the company behind the tenant — the company IS the tenant (<c>public_id == tenant_id</c>).</summary>
    Task<string?> GetCompanyCountryCodeAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the code matches an ACTIVE country. It is what tells "you passed a country that does not exist"
    /// (a bad parameter) from "this country's catalog has no rows" (a legitimate empty list); both used to answer
    /// <c>200 []</c>.
    /// </summary>
    Task<bool> CountryCodeIsActiveAsync(string countryCode, CancellationToken cancellationToken);
}
