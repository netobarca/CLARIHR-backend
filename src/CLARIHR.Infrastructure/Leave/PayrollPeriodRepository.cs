using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.Payroll;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Leave;

internal sealed class PayrollPeriodRepository(ApplicationDbContext dbContext) : IPayrollPeriodRepository
{
    public void Add(PayrollPeriodDefinition payrollPeriod) => dbContext.PayrollPeriodDefinitions.Add(payrollPeriod);

    public Task<PayrollPeriodDefinition?> GetByIdAsync(Guid payrollPeriodId, CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions.SingleOrDefaultAsync(period => period.PublicId == payrollPeriodId, cancellationToken);

    public Task<PayrollPeriodDefinition?> GetByInternalIdAsync(long payrollPeriodId, CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions.SingleOrDefaultAsync(period => period.Id == payrollPeriodId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid payrollPeriodId, CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(period => period.PublicId == payrollPeriodId, cancellationToken);

    // Legacy bucket only (payroll_definition_id IS NULL) — mirrors the partial unique index the probe backs.
    public Task<bool> PeriodExistsAsync(
        Guid tenantId,
        string payPeriodTypeCode,
        int year,
        int number,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions.AnyAsync(
            period => period.TenantId == tenantId &&
                      period.PayrollDefinitionId == null &&
                      period.PayPeriodTypeCode == payPeriodTypeCode &&
                      period.Year == year &&
                      period.Number == number &&
                      (!excludingPayrollPeriodId.HasValue || period.PublicId != excludingPayrollPeriodId.Value),
            cancellationToken);

    public Task<bool> PeriodExistsForDefinitionAsync(
        Guid tenantId,
        long payrollDefinitionId,
        int year,
        int number,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions.AnyAsync(
            period => period.TenantId == tenantId &&
                      period.PayrollDefinitionId == payrollDefinitionId &&
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
        long? payrollDefinitionId,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions.AnyAsync(
            period => period.TenantId == tenantId &&
                      period.PayrollDefinitionId == payrollDefinitionId &&
                      period.PayPeriodTypeCode == payPeriodTypeCode &&
                      period.Year == year &&
                      period.IsActive &&
                      startDate <= period.EndDate &&
                      endDate >= period.StartDate &&
                      (!excludingPayrollPeriodId.HasValue || period.PublicId != excludingPayrollPeriodId.Value),
            cancellationToken);

    public async Task<IReadOnlyCollection<int>> GetExistingNumbersForDefinitionAsync(
        Guid tenantId,
        long payrollDefinitionId,
        int year,
        CancellationToken cancellationToken) =>
        await dbContext.PayrollPeriodDefinitions
            .AsNoTracking()
            .Where(period => period.TenantId == tenantId &&
                             period.PayrollDefinitionId == payrollDefinitionId &&
                             period.Year == year)
            .Select(period => period.Number)
            .ToListAsync(cancellationToken);

    public Task<PayrollPeriodOvertimeWindow?> GetOvertimeWindowByPublicIdAsync(
        Guid tenantId,
        Guid payrollPeriodPublicId,
        CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions
            .AsNoTracking()
            .Where(period => period.TenantId == tenantId && period.PublicId == payrollPeriodPublicId)
            .Select(period => new PayrollPeriodOvertimeWindow(
                period.PayrollDefinitionId,
                period.AllowsOvertimeEntry,
                period.OvertimeEntryStart,
                period.OvertimeEntryEnd))
            .SingleOrDefaultAsync(cancellationToken);

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
                period.Code,
                period.PayrollDefinitionId == null
                    ? null
                    : dbContext.Set<PayrollDefinition>()
                        .Where(definition => definition.Id == period.PayrollDefinitionId)
                        .Select(definition => (Guid?)definition.PublicId)
                        .FirstOrDefault(),
                period.CutoffDate,
                period.PaymentDate,
                period.Month,
                period.StatusCode,
                period.AllowsOvertimeEntry,
                period.OvertimeEntryStart,
                period.OvertimeEntryEnd,
                period.AllowsAttendance,
                period.AttendanceEntryStart,
                period.AttendanceEntryEnd,
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
                period.Code,
                period.PayrollDefinitionId == null
                    ? null
                    : dbContext.Set<PayrollDefinition>()
                        .Where(definition => definition.Id == period.PayrollDefinitionId)
                        .Select(definition => (Guid?)definition.PublicId)
                        .FirstOrDefault(),
                period.CutoffDate,
                period.PaymentDate,
                period.Month,
                period.StatusCode,
                period.AllowsOvertimeEntry,
                period.OvertimeEntryStart,
                period.OvertimeEntryEnd,
                period.AllowsAttendance,
                period.AttendanceEntryStart,
                period.AttendanceEntryEnd,
                period.IsActive,
                period.ConcurrencyToken,
                period.CreatedUtc,
                period.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}
