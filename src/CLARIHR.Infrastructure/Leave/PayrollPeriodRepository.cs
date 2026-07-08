using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Leave;

internal sealed class PayrollPeriodRepository(ApplicationDbContext dbContext) : IPayrollPeriodRepository
{
    public void Add(PayrollPeriodDefinition payrollPeriod) => dbContext.PayrollPeriodDefinitions.Add(payrollPeriod);

    public Task<PayrollPeriodDefinition?> GetByIdAsync(Guid payrollPeriodId, CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions.SingleOrDefaultAsync(period => period.PublicId == payrollPeriodId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid payrollPeriodId, CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(period => period.PublicId == payrollPeriodId, cancellationToken);

    public Task<bool> PeriodExistsAsync(
        Guid tenantId,
        string payPeriodTypeCode,
        int year,
        int number,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions.AnyAsync(
            period => period.TenantId == tenantId &&
                      period.PayPeriodTypeCode == payPeriodTypeCode &&
                      period.Year == year &&
                      period.Number == number &&
                      (!excludingPayrollPeriodId.HasValue || period.PublicId != excludingPayrollPeriodId.Value),
            cancellationToken);

    public Task<bool> HasOverlapAsync(
        Guid tenantId,
        string payPeriodTypeCode,
        int year,
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions.AnyAsync(
            period => period.TenantId == tenantId &&
                      period.PayPeriodTypeCode == payPeriodTypeCode &&
                      period.Year == year &&
                      period.IsActive &&
                      startDate <= period.EndDate &&
                      endDate >= period.StartDate &&
                      (!excludingPayrollPeriodId.HasValue || period.PublicId != excludingPayrollPeriodId.Value),
            cancellationToken);

    public async Task<PagedResponse<PayrollPeriodListItemResponse>> SearchAsync(
        Guid tenantId,
        string? payPeriodTypeCode,
        int? year,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.PayrollPeriodDefinitions
            .AsNoTracking()
            .Where(period => period.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(payPeriodTypeCode))
        {
            var normalizedTypeCode = payPeriodTypeCode.Trim().ToUpperInvariant();
            query = query.Where(period => period.PayPeriodTypeCode == normalizedTypeCode);
        }

        if (year.HasValue)
        {
            query = query.Where(period => period.Year == year.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(period => period.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(period => period.Year)
            .ThenBy(period => period.Number)
            .ThenBy(period => period.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(period => new PayrollPeriodListItemResponse(
                period.PublicId,
                period.PayPeriodTypeCode,
                period.Year,
                period.Number,
                period.Label,
                period.StartDate,
                period.EndDate,
                period.IsActive,
                period.ConcurrencyToken,
                period.CreatedUtc,
                period.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PayrollPeriodListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<PayrollPeriodResponse?> GetResponseByIdAsync(Guid payrollPeriodId, CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions
            .AsNoTracking()
            .Where(period => period.PublicId == payrollPeriodId)
            .Select(period => new PayrollPeriodResponse(
                period.PublicId,
                period.PayPeriodTypeCode,
                period.Year,
                period.Number,
                period.Label,
                period.StartDate,
                period.EndDate,
                period.IsActive,
                period.ConcurrencyToken,
                period.CreatedUtc,
                period.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}
