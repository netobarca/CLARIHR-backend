using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICompanySubscriptionRepository
{
    void Add(CompanySubscription subscription);

    Task<string?> GetActivePlanCodeAsync(Guid companyPublicId, CancellationToken cancellationToken);
}
