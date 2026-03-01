using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class CompanySubscriptionRepository(ApplicationDbContext dbContext) : ICompanySubscriptionRepository
{
    public void Add(CompanySubscription subscription) => dbContext.CompanySubscriptions.Add(subscription);

    public Task<string?> GetActivePlanCodeAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .Where(subscription =>
                subscription.Status == SubscriptionStatus.Active &&
                dbContext.Companies.Any(company => company.Id == subscription.CompanyId && company.PublicId == companyPublicId))
            .Select(subscription => subscription.PlanCode)
            .SingleOrDefaultAsync(cancellationToken);
}
