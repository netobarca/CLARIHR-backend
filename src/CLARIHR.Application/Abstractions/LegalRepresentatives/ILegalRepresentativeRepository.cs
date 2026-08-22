using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Domain.LegalRepresentatives;

namespace CLARIHR.Application.Abstractions.LegalRepresentatives;

public interface ILegalRepresentativeRepository
{
    void Add(LegalRepresentative legalRepresentative);

    Task<LegalRepresentative?> GetByIdAsync(Guid legalRepresentativeId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid legalRepresentativeId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the company's country, which decides WHICH identity documents are valid: a DUI only exists
    /// in El Salvador, a cédula de ciudadanía only in Colombia.
    /// </summary>
    Task<string?> GetCompanyCountryCodeAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// True when the code is an active identity document type for that country. Legal representatives share
    /// this catalog with personnel files: a legal representative is a person and is identified by the same
    /// documents as an employee, so a second vocabulary would let the same human be registered twice under
    /// two different type codes.
    /// </summary>
    Task<bool> IdentificationTypeExistsAsync(
        string countryCode,
        string normalizedCode,
        CancellationToken cancellationToken);

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

    /// <summary>
    /// B-04 — el sucesor cuando se da de baja al principal. Devuelve el representante activo más antiguo del
    /// tenant, excluyendo al que se está dando de baja; <c>null</c> si no queda ninguno.
    /// <para>
    /// El orden es por antigüedad y se desempata por <c>Id</c>, para que la promoción sea determinista: sin un
    /// orden estable, dos empresas con los mismos datos podrían promover a personas distintas.
    /// </para>
    /// </summary>
    Task<LegalRepresentative?> GetPromotionCandidateAsync(
        Guid tenantId,
        Guid excludingLegalRepresentativePublicId,
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
