using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Domain.LegalRepresentatives;

namespace CLARIHR.Application.Abstractions.LegalRepresentatives;

public interface ILegalRepresentativeRepository
{
    void Add(LegalRepresentative legalRepresentative);

    Task<LegalRepresentative?> GetByIdAsync(Guid legalRepresentativeId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid legalRepresentativeId, CancellationToken cancellationToken);

    Task<bool> DocumentExistsAsync(
        Guid tenantId,
        string documentType,
        string normalizedDocumentNumber,
        long? excludingLegalRepresentativeId,
        CancellationToken cancellationToken);

    Task<PagedResponse<LegalRepresentativeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        bool? isPrimary,
        LegalRepresentativeRepresentationType? representationType,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<LegalRepresentativeResponse?> GetResponseByIdAsync(Guid legalRepresentativeId, CancellationToken cancellationToken);

    Task<LegalRepresentativeUsageResponse?> GetUsageByIdAsync(Guid legalRepresentativeId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>> GetPositionTitleCatalogItemsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>> GetRepresentationTypeCatalogItemsAsync(
        CancellationToken cancellationToken);

    Task<int> GetActiveCountAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Cheap boolean probe: does the tenant have another active legal representative besides the
    /// given one? Used by the detail GET to derive <c>CanInactivate</c> without re-projecting the
    /// entity or counting (§LR5) — pairs with the response's own <c>IsActive</c>.
    /// </summary>
    Task<bool> HasOtherActiveRepresentativeAsync(
        Guid tenantId,
        Guid excludingLegalRepresentativePublicId,
        CancellationToken cancellationToken);

    Task<LegalRepresentative?> GetActivePrimaryAsync(
        Guid tenantId,
        Guid? excludingLegalRepresentativePublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LegalRepresentativeExportRow>> GetExportRowsAsync(
        Guid tenantId,
        bool? isActive,
        bool? isPrimary,
        LegalRepresentativeRepresentationType? representationType,
        string? search,
        int? maxRows,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ActiveLegalRepresentativeSummary>> GetActiveSummariesByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken);
}
