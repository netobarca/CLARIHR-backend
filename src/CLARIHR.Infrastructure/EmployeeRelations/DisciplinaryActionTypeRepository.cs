using CLARIHR.Application.Abstractions.EmployeeRelations;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EmployeeRelations;
using CLARIHR.Domain.EmployeeRelations;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.EmployeeRelations;

internal sealed class DisciplinaryActionTypeRepository(ApplicationDbContext dbContext) : IDisciplinaryActionTypeRepository
{
    public void Add(DisciplinaryActionType type) => dbContext.Set<DisciplinaryActionType>().Add(type);

    public Task<DisciplinaryActionType?> GetByIdAsync(Guid disciplinaryActionTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<DisciplinaryActionType>().SingleOrDefaultAsync(type => type.PublicId == disciplinaryActionTypeId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid disciplinaryActionTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<DisciplinaryActionType>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(type => type.PublicId == disciplinaryActionTypeId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingDisciplinaryActionTypeId,
        CancellationToken cancellationToken) =>
        dbContext.Set<DisciplinaryActionType>().AnyAsync(
            type => type.TenantId == tenantId &&
                    type.IsActive &&
                    type.NormalizedCode == normalizedCode &&
                    (!excludingDisciplinaryActionTypeId.HasValue || type.PublicId != excludingDisciplinaryActionTypeId.Value),
            cancellationToken);

    // REQ-003 PR-4: the disciplinary-actions table now exists — a type is in use when an active disciplinary
    // action of the tenant references it (logical inactivation is then blocked with DISCIPLINARY_ACTION_TYPE_IN_USE).
    public Task<bool> IsInUseAsync(Guid tenantId, Guid disciplinaryActionTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<Domain.PersonnelFiles.PersonnelFileDisciplinaryAction>()
            .AsNoTracking()
            .AnyAsync(
                action => action.TenantId == tenantId
                    && action.IsActive
                    && action.DisciplinaryActionType!.PublicId == disciplinaryActionTypeId,
                cancellationToken);

    public async Task<PagedResponse<DisciplinaryActionTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        bool? appliesSuspension,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<DisciplinaryActionType>()
            .AsNoTracking()
            .Where(type => type.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(type => type.IsActive == isActive.Value);
        }

        if (appliesSuspension.HasValue)
        {
            query = query.Where(type => type.AppliesSuspension == appliesSuspension.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(type =>
                type.NormalizedCode.Contains(normalizedSearch) ||
                type.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(type => type.SortOrder)
            .ThenBy(type => type.Name)
            .ThenBy(type => type.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(type => new DisciplinaryActionTypeListItemResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.AppliesSuspension,
                type.SortOrder,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<DisciplinaryActionTypeListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<DisciplinaryActionTypeResponse?> GetResponseByIdAsync(Guid disciplinaryActionTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<DisciplinaryActionType>()
            .AsNoTracking()
            .Where(type => type.PublicId == disciplinaryActionTypeId)
            .Select(type => new DisciplinaryActionTypeResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.AppliesSuspension,
                type.SortOrder,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}
