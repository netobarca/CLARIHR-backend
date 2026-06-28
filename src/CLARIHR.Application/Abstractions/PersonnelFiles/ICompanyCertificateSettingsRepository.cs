using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>Repository for the company-level certificate settings (D-17): one row per tenant.</summary>
public interface ICompanyCertificateSettingsRepository
{
    void Add(CompanyCertificateSettings settings);

    Task<CompanyCertificateSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken);
}
