using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialAddons;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class CommercialAddonRepository(ApplicationDbContext dbContext) : ICommercialAddonRepository
{
    public void Add(CommercialAddon addon) => dbContext.CommercialAddons.Add(addon);

    public async Task<IReadOnlyCollection<CommercialAddon>> ListActiveAsync(CancellationToken cancellationToken) =>
        await dbContext.CommercialAddons
            .AsNoTracking()
            .Include(addon => addon.Entitlements)
            .Where(addon => addon.Status == CommercialAddonStatus.Active)
            .OrderBy(addon => addon.Name)
            .ThenBy(addon => addon.Code)
            .ToListAsync(cancellationToken);

    public Task<CommercialAddon?> GetByIdAsync(Guid commercialAddonId, CancellationToken cancellationToken) =>
        dbContext.CommercialAddons
            .Include(addon => addon.Entitlements)
            .SingleOrDefaultAsync(addon => addon.PublicId == commercialAddonId, cancellationToken);

    public Task<bool> CodeExistsAsync(string normalizedCode, long? excludingId, CancellationToken cancellationToken) =>
        dbContext.CommercialAddons.AnyAsync(
            addon => addon.NormalizedCode == normalizedCode &&
                     (!excludingId.HasValue || addon.Id != excludingId.Value),
            cancellationToken);

    public async Task<PagedResponse<CommercialAddonSummaryResponse>> SearchAsync(
        CommercialAddonType? type,
        CommercialAddonBillingModel? billingModel,
        CommercialAddonStatus? status,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CommercialAddons.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(addon => addon.Status == status.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(addon => addon.Type == type.Value);
        }

        if (billingModel.HasValue)
        {
            query = query.Where(addon => addon.BillingModel == billingModel.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(addon =>
                addon.NormalizedCode.Contains(normalizedSearch) ||
                addon.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(addon => addon.Name)
            .ThenBy(addon => addon.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(addon => new CommercialAddonSummaryResponse(
                addon.PublicId,
                addon.Code,
                addon.Name,
                addon.Description,
                addon.Type,
                addon.BillingModel,
                addon.MeasurementUnit,
                addon.UnitPrice,
                addon.MinimumQuantity,
                addon.MinimumMonthlyFee,
                addon.Periodicity,
                addon.Status,
                addon.Entitlements.Count(entitlement => entitlement.IsEnabled),
                addon.CreatedUtc,
                addon.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CommercialAddonSummaryResponse>(items, pageNumber, pageSize, totalCount);
    }
}
