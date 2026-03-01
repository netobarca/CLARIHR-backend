using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class CompanyRepository(ApplicationDbContext dbContext) : ICompanyRepository
{
    public void Add(Company company) => dbContext.Companies.Add(company);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        dbContext.Companies.AnyAsync(company => company.Slug == slug, cancellationToken);

    public Task<Company?> FindByPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
        dbContext.Companies.SingleOrDefaultAsync(company => company.PublicId == companyPublicId, cancellationToken);
}
