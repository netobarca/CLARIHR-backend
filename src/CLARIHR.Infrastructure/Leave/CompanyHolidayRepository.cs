using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Leave;

internal sealed class CompanyHolidayRepository(ApplicationDbContext dbContext) : ICompanyHolidayRepository
{
    public void Add(CompanyHoliday companyHoliday) => dbContext.CompanyHolidays.Add(companyHoliday);

    public Task<CompanyHoliday?> GetByIdAsync(Guid companyHolidayId, CancellationToken cancellationToken) =>
        dbContext.CompanyHolidays.SingleOrDefaultAsync(holiday => holiday.PublicId == companyHolidayId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid companyHolidayId, CancellationToken cancellationToken) =>
        dbContext.CompanyHolidays
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(holiday => holiday.PublicId == companyHolidayId, cancellationToken);

    public Task<bool> DateExistsAsync(
        Guid tenantId,
        DateOnly date,
        Guid? excludingCompanyHolidayId,
        CancellationToken cancellationToken) =>
        dbContext.CompanyHolidays.AnyAsync(
            holiday => holiday.TenantId == tenantId &&
                       holiday.Date == date &&
                       (!excludingCompanyHolidayId.HasValue || holiday.PublicId != excludingCompanyHolidayId.Value),
            cancellationToken);

    public async Task<PagedResponse<CompanyHolidayListItemResponse>> SearchAsync(
        Guid tenantId,
        int? year,
        string? scopeCode,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CompanyHolidays
            .AsNoTracking()
            .Where(holiday => holiday.TenantId == tenantId);

        if (year.HasValue)
        {
            query = query.Where(holiday => holiday.Date.Year == year.Value);
        }

        if (!string.IsNullOrWhiteSpace(scopeCode))
        {
            var normalizedScopeCode = scopeCode.Trim().ToUpperInvariant();
            query = query.Where(holiday => holiday.ScopeCode == normalizedScopeCode);
        }

        if (isActive.HasValue)
        {
            query = query.Where(holiday => holiday.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(holiday => holiday.Date)
            .ThenBy(holiday => holiday.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(holiday => new CompanyHolidayListItemResponse(
                holiday.PublicId,
                holiday.Date,
                holiday.Description,
                holiday.ScopeCode,
                holiday.IsActive,
                holiday.ConcurrencyToken,
                holiday.CreatedUtc,
                holiday.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CompanyHolidayListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<CompanyHolidayResponse?> GetResponseByIdAsync(Guid companyHolidayId, CancellationToken cancellationToken) =>
        dbContext.CompanyHolidays
            .AsNoTracking()
            .Where(holiday => holiday.PublicId == companyHolidayId)
            .Select(holiday => new CompanyHolidayResponse(
                holiday.PublicId,
                holiday.Date,
                holiday.Description,
                holiday.ScopeCode,
                holiday.IsActive,
                holiday.ConcurrencyToken,
                holiday.CreatedUtc,
                holiday.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}
