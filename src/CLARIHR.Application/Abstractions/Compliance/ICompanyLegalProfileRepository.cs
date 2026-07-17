using CLARIHR.Domain.Compliance;

namespace CLARIHR.Application.Abstractions.Compliance;

public interface ICompanyLegalProfileRepository
{
    void Add(CompanyLegalProfile profile);

    Task<CompanyLegalProfile?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken);
}
