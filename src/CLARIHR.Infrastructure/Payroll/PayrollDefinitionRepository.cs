using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Domain.Payroll;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Payroll;

internal sealed class PayrollDefinitionRepository(ApplicationDbContext dbContext) : IPayrollDefinitionRepository
{
    public void Add(PayrollDefinition definition) => dbContext.Set<PayrollDefinition>().Add(definition);

    public Task<PayrollDefinition?> GetByIdAsync(Guid payrollDefinitionId, CancellationToken cancellationToken) =>
        dbContext.Set<PayrollDefinition>()
            .SingleOrDefaultAsync(definition => definition.PublicId == payrollDefinitionId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid payrollDefinitionId, CancellationToken cancellationToken) =>
        dbContext.Set<PayrollDefinition>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(definition => definition.PublicId == payrollDefinitionId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingPayrollDefinitionId,
        CancellationToken cancellationToken) =>
        dbContext.Set<PayrollDefinition>().AnyAsync(
            definition => definition.TenantId == tenantId &&
                          definition.IsActive &&
                          definition.NormalizedCode == normalizedCode &&
                          (!excludingPayrollDefinitionId.HasValue || definition.PublicId != excludingPayrollDefinitionId.Value),
            cancellationToken);

    // PR-1 has neither the period FK (M2/PR-2) nor the run table (M4/PR-4) yet, so nothing can reference a
    // payroll definition — the real reference probes are wired as those PRs land.
    public Task<bool> IsInUseAsync(Guid tenantId, Guid payrollDefinitionId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public async Task<PagedResponse<PayrollDefinitionListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<PayrollDefinition>()
            .AsNoTracking()
            .Where(definition => definition.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(definition => definition.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(definition =>
                definition.NormalizedCode.Contains(normalizedSearch) ||
                definition.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(definition => definition.Code)
            .ThenBy(definition => definition.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(definition => new PayrollDefinitionListItemResponse(
                definition.PublicId,
                definition.Code,
                definition.Name,
                definition.PayrollTypeCode,
                definition.PayPeriodCode,
                definition.TotalPeriods,
                definition.GuaranteesMinimumIncome,
                definition.CurrencyCode,
                definition.OvertimeWindowEnabled,
                definition.OvertimeWindowOffsetDays,
                definition.AttendanceWindowEnabled,
                definition.AttendanceWindowOffsetDays,
                definition.IsActive,
                definition.ConcurrencyToken,
                definition.CreatedUtc,
                definition.ModifiedUtc,
                null))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PayrollDefinitionListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<PayrollDefinitionResponse?> GetResponseByIdAsync(Guid payrollDefinitionId, CancellationToken cancellationToken) =>
        dbContext.Set<PayrollDefinition>()
            .AsNoTracking()
            .Where(definition => definition.PublicId == payrollDefinitionId)
            .Select(definition => new PayrollDefinitionResponse(
                definition.PublicId,
                definition.Code,
                definition.Name,
                definition.PayrollTypeCode,
                definition.PayPeriodCode,
                definition.TotalPeriods,
                definition.GuaranteesMinimumIncome,
                definition.CurrencyCode,
                definition.OvertimeWindowEnabled,
                definition.OvertimeWindowOffsetDays,
                definition.AttendanceWindowEnabled,
                definition.AttendanceWindowOffsetDays,
                definition.IsActive,
                definition.ConcurrencyToken,
                definition.CreatedUtc,
                definition.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}
