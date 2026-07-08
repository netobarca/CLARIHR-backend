using CLARIHR.Application.Abstractions.EmployeeRelations;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EmployeeRelations;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Compensation;
using CLARIHR.Domain.EmployeeRelations;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.EmployeeRelations;

internal sealed class DisciplinaryActionCauseRepository(ApplicationDbContext dbContext) : IDisciplinaryActionCauseRepository
{
    public void Add(DisciplinaryActionCause cause) => dbContext.Set<DisciplinaryActionCause>().Add(cause);

    public Task<DisciplinaryActionCause?> GetByIdAsync(Guid disciplinaryActionCauseId, CancellationToken cancellationToken) =>
        dbContext.Set<DisciplinaryActionCause>().SingleOrDefaultAsync(cause => cause.PublicId == disciplinaryActionCauseId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid disciplinaryActionCauseId, CancellationToken cancellationToken) =>
        dbContext.Set<DisciplinaryActionCause>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(cause => cause.PublicId == disciplinaryActionCauseId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingDisciplinaryActionCauseId,
        CancellationToken cancellationToken) =>
        dbContext.Set<DisciplinaryActionCause>().AnyAsync(
            cause => cause.TenantId == tenantId &&
                    cause.IsActive &&
                    cause.NormalizedCode == normalizedCode &&
                    (!excludingDisciplinaryActionCauseId.HasValue || cause.PublicId != excludingDisciplinaryActionCauseId.Value),
            cancellationToken);

    // PR-1 has no disciplinary-action record table yet (M2/PR-2), so nothing can reference a cause — the
    // real reference probe is wired in PR-4 once that table exists.
    public Task<bool> IsInUseAsync(Guid tenantId, Guid disciplinaryActionCauseId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public async Task<bool> IsDeductionConceptValidAsync(
        Guid tenantId,
        string normalizedConceptCode,
        CancellationToken cancellationToken)
    {
        // Resolve the tenant's country, then require an ACTIVE egreso concept with that normalized code
        // in the country-scoped compensation-concept-types catalog (RN-04 / DEDUCTION_CONCEPT_INVALID).
        var companyCountryId = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.PublicId == tenantId)
            .Select(company => (long?)company.CountryCatalogItemId)
            .SingleOrDefaultAsync(cancellationToken);
        if (companyCountryId is null)
        {
            return false;
        }

        return await dbContext.CompensationConceptTypeCatalogItems
            .AsNoTracking()
            .AnyAsync(
                item => item.CountryCatalogItemId == companyCountryId.Value &&
                    item.IsActive &&
                    item.Nature == CompensationNature.Egreso &&
                    item.NormalizedCode == normalizedConceptCode,
                cancellationToken);
    }

    public async Task<PagedResponse<DisciplinaryActionCauseListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<DisciplinaryActionCause>()
            .AsNoTracking()
            .Where(cause => cause.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(cause => cause.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(cause =>
                cause.NormalizedCode.Contains(normalizedSearch) ||
                cause.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(cause => cause.SortOrder)
            .ThenBy(cause => cause.Name)
            .ThenBy(cause => cause.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(cause => new DisciplinaryActionCauseListItemResponse(
                cause.PublicId,
                cause.Code,
                cause.Name,
                cause.DeductionConceptTypeCode,
                cause.SortOrder,
                cause.IsActive,
                cause.ConcurrencyToken,
                cause.CreatedUtc,
                cause.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<DisciplinaryActionCauseListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<DisciplinaryActionCauseResponse?> GetResponseByIdAsync(Guid disciplinaryActionCauseId, CancellationToken cancellationToken) =>
        dbContext.Set<DisciplinaryActionCause>()
            .AsNoTracking()
            .Where(cause => cause.PublicId == disciplinaryActionCauseId)
            .Select(cause => new DisciplinaryActionCauseResponse(
                cause.PublicId,
                cause.Code,
                cause.Name,
                cause.DeductionConceptTypeCode,
                cause.SortOrder,
                cause.IsActive,
                cause.ConcurrencyToken,
                cause.CreatedUtc,
                cause.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}
