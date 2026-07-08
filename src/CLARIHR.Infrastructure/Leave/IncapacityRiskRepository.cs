using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Leave;

internal sealed class IncapacityRiskRepository(ApplicationDbContext dbContext) : IIncapacityRiskRepository
{
    public void Add(IncapacityRisk incapacityRisk) => dbContext.IncapacityRisks.Add(incapacityRisk);

    public Task<IncapacityRisk?> GetByIdAsync(Guid incapacityRiskId, CancellationToken cancellationToken) =>
        dbContext.IncapacityRisks
            // The parameters travel with the aggregate: the domain guards in Update/ReplaceParameters
            // reason over the loaded child set, and EF needs the tracked children to diff the
            // replace-set (delete removed rows, insert new ones).
            .Include(risk => risk.Parameters)
            .SingleOrDefaultAsync(risk => risk.PublicId == incapacityRiskId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid incapacityRiskId, CancellationToken cancellationToken) =>
        dbContext.IncapacityRisks
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(risk => risk.PublicId == incapacityRiskId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingIncapacityRiskId,
        CancellationToken cancellationToken) =>
        dbContext.IncapacityRisks.AnyAsync(
            risk => risk.TenantId == tenantId &&
                    risk.NormalizedCode == normalizedCode &&
                    (!excludingIncapacityRiskId.HasValue || risk.PublicId != excludingIncapacityRiskId.Value),
            cancellationToken);

    public async Task<PagedResponse<IncapacityRiskListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.IncapacityRisks
            .AsNoTracking()
            .Where(risk => risk.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(risk => risk.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(risk =>
                risk.NormalizedCode.Contains(normalizedSearch) ||
                risk.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(risk => risk.Name)
            .ThenBy(risk => risk.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(risk => new IncapacityRiskListItemResponse(
                risk.PublicId,
                risk.Code,
                risk.Name,
                risk.CountsSeventhDay,
                risk.CountsSaturday,
                risk.CountsHoliday,
                risk.UsesWorkSchedule,
                risk.AllowsIndefinite,
                risk.AllowsExtension,
                risk.UsesFund,
                risk.HasSubsidy,
                risk.Parameters.Count,
                risk.IsActive,
                risk.ConcurrencyToken,
                risk.CreatedUtc,
                risk.ModifiedUtc,
                null))
            .ToListAsync(cancellationToken);

        return new PagedResponse<IncapacityRiskListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IncapacityRiskResponse?> GetResponseByIdAsync(Guid incapacityRiskId, CancellationToken cancellationToken)
    {
        // Loads the aggregate and maps in memory instead of projecting the correlated child
        // collection inside a positional-record constructor (a translation shape EF has bitten us
        // on before); a single aggregate with a handful of tranches makes the round-trip trivial.
        var risk = await dbContext.IncapacityRisks
            .AsNoTracking()
            .Include(entity => entity.Parameters)
            .SingleOrDefaultAsync(entity => entity.PublicId == incapacityRiskId, cancellationToken);

        if (risk is null)
        {
            return null;
        }

        var parameters = risk.Parameters
            .OrderBy(parameter => parameter.SortOrder)
            .Select(parameter => new IncapacityRiskParameterResponse(
                parameter.DayFrom,
                parameter.DayTo,
                parameter.SubsidyPercent,
                parameter.PayerCode,
                parameter.SortOrder))
            .ToArray();

        return new IncapacityRiskResponse(
            risk.PublicId,
            risk.Code,
            risk.Name,
            risk.CountsSeventhDay,
            risk.CountsSaturday,
            risk.CountsHoliday,
            risk.UsesWorkSchedule,
            risk.AllowsIndefinite,
            risk.AllowsExtension,
            risk.UsesFund,
            risk.HasSubsidy,
            parameters,
            risk.IsActive,
            risk.ConcurrencyToken,
            risk.CreatedUtc,
            risk.ModifiedUtc,
            null);
    }
}
