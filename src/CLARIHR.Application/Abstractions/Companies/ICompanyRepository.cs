using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICompanyRepository
{
    void Add(Company company);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    Task<Company?> FindByPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken);
}
