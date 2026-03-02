using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICompanyProvisioningService
{
    Task<Result<ProvisionedCompanyResult>> ProvisionAsync(
        ProvisionCompanyRequest request,
        CancellationToken cancellationToken);
}

public sealed record ProvisionCompanyRequest(
    Guid OwnerUserPublicId,
    string? CompanyName,
    bool MakePrimary,
    string PlanCode,
    bool ProvisionAsInitialCompany);

public sealed record ProvisionedCompanyResult(
    Guid CompanyId,
    string CompanyName,
    string Slug,
    string PlanCode,
    DateTime CreatedAtUtc);
