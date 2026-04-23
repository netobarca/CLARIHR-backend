using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.LegalRepresentatives.Common;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICompanyProvisioningService
{
    Task<Result<ProvisionedCompanyResult>> ProvisionAsync(
        ProvisionCompanyRequest request,
        CancellationToken cancellationToken);

    Task<Result> EnsureOwnerAdministrationAsync(
        Guid ownerUserPublicId,
        Guid companyPublicId,
        CancellationToken cancellationToken);
}

public sealed record ProvisionCompanyRequest(
    Guid OwnerUserPublicId,
    string? CompanyName,
    string CountryCode,
    InitialLegalRepresentativeInput InitialLegalRepresentative,
    bool MakePrimary,
    string PlanCode,
    bool ProvisionAsInitialCompany,
    long? CompanyTypeCatalogItemId = null);

public sealed record ProvisionedCompanyResult(
    Guid CompanyId,
    string CompanyName,
    string Slug,
    string PlanCode,
    DateTime CreatedAtUtc);
