using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class CompanyOwnershipPolicy(
    ICompanyRepository companyRepository,
    IOptions<CompanyOwnershipOptions> options) : ICompanyOwnershipPolicy
{
    public async Task<bool> HasCapacityForAnotherActiveCompanyAsync(Guid ownerUserPublicId, CancellationToken cancellationToken)
    {
        var count = await companyRepository.CountOwnedByUserAsync(
            ownerUserPublicId,
            new CompanyOwnershipCountFilter([CompanyStatus.Active, CompanyStatus.Suspended]),
            cancellationToken);

        return count < Math.Max(1, options.Value.MaxOwnedActiveCompanies);
    }
}
