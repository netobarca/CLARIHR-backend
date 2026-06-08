using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.OrgUnits;
using CLARIHR.Domain.OrgUnits;

namespace CLARIHR.Application.Abstractions.OrgUnits;

public interface IOrgUnitRepository
{
    void Add(OrgUnit orgUnit);

    Task<OrgUnit?> GetByIdAsync(Guid orgUnitId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid orgUnitId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingOrgUnitId, CancellationToken cancellationToken);

    Task<PagedResponse<OrgUnitResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        Guid? orgUnitTypeId,
        Guid? functionalAreaId,
        Guid? parentId,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OrgUnitResponse?> GetResponseByIdAsync(Guid orgUnitId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrgUnitHierarchyNodeData>> GetHierarchyAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<OrgUnitExportRow>> GetExportRowsAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        Guid? orgUnitTypeId,
        Guid? functionalAreaId,
        Guid? parentId,
        int? maxRows,
        CancellationToken cancellationToken);

    Task<bool> HasActiveChildrenAsync(long orgUnitId, CancellationToken cancellationToken);

    // OU-007: read-path variant that resolves the active-child flag from the public id in a single query,
    // so GetById no longer needs a separate GetByIdAsync just to obtain the internal id. The internal-id
    // overload above stays for write handlers (Inactivate) that already hold the loaded entity.
    Task<bool> HasActiveChildrenByPublicIdAsync(Guid orgUnitPublicId, CancellationToken cancellationToken);
}
