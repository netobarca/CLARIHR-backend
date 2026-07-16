using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Domain.Payroll;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Payroll;

internal sealed class WorkScheduleRepository(ApplicationDbContext dbContext) : IWorkScheduleRepository
{
    public void Add(WorkSchedule schedule) => dbContext.Set<WorkSchedule>().Add(schedule);

    public Task<WorkSchedule?> GetByIdAsync(Guid workScheduleId, CancellationToken cancellationToken) =>
        dbContext.Set<WorkSchedule>()
            .Include(schedule => schedule.Days)
            .SingleOrDefaultAsync(schedule => schedule.PublicId == workScheduleId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid workScheduleId, CancellationToken cancellationToken) =>
        dbContext.Set<WorkSchedule>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(schedule => schedule.PublicId == workScheduleId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingWorkScheduleId,
        CancellationToken cancellationToken) =>
        dbContext.Set<WorkSchedule>().AnyAsync(
            schedule => schedule.TenantId == tenantId &&
                        schedule.IsActive &&
                        schedule.NormalizedCode == normalizedCode &&
                        (!excludingWorkScheduleId.HasValue || schedule.PublicId != excludingWorkScheduleId.Value),
            cancellationToken);

    // The plaza's WorkdayCode IS the link (no FK — REQ-012 §3.3), so the reference probe scans the ACTIVE
    // employment assignments carrying the code. The assignment stores the code as typed (CleanOptional —
    // trimmed, case preserved), so the comparison uppercases the stored side to match the normalized key.
    public Task<bool> IsInUseAsync(Guid tenantId, string normalizedCode, CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFileEmploymentAssignment>().AnyAsync(
            assignment => assignment.TenantId == tenantId &&
                          assignment.IsActive &&
                          assignment.WorkdayCode != null &&
                          assignment.WorkdayCode.ToUpper() == normalizedCode,
            cancellationToken);

    public Task<bool> ActiveCodeExistsAsync(Guid tenantId, string normalizedCode, CancellationToken cancellationToken) =>
        dbContext.Set<WorkSchedule>().AnyAsync(
            schedule => schedule.TenantId == tenantId &&
                        schedule.IsActive &&
                        schedule.NormalizedCode == normalizedCode,
            cancellationToken);

    public async Task<PagedResponse<WorkScheduleListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<WorkSchedule>()
            .AsNoTracking()
            .Where(schedule => schedule.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(schedule => schedule.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(schedule =>
                schedule.NormalizedCode.Contains(normalizedSearch) ||
                schedule.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(schedule => schedule.Code)
            .ThenBy(schedule => schedule.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(schedule => new WorkScheduleListItemResponse(
                schedule.PublicId,
                schedule.Code,
                schedule.Name,
                schedule.ScheduleLabel,
                schedule.AttendanceDateAnchor,
                schedule.ScheduleClass,
                schedule.TotalWeeklyHours,
                schedule.Days.Count,
                schedule.IsActive,
                schedule.ConcurrencyToken,
                schedule.CreatedUtc,
                schedule.ModifiedUtc,
                null))
            .ToListAsync(cancellationToken);

        return new PagedResponse<WorkScheduleListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<WorkScheduleResponse?> GetResponseByIdAsync(Guid workScheduleId, CancellationToken cancellationToken)
    {
        var schedule = await dbContext.Set<WorkSchedule>()
            .AsNoTracking()
            .Include(item => item.Days)
            .SingleOrDefaultAsync(item => item.PublicId == workScheduleId, cancellationToken);

        if (schedule is null)
        {
            return null;
        }

        return new WorkScheduleResponse(
            schedule.PublicId,
            schedule.Code,
            schedule.Name,
            schedule.ScheduleLabel,
            schedule.AttendanceDateAnchor,
            schedule.ScheduleClass,
            schedule.TotalWeeklyHours,
            schedule.Days
                .OrderBy(day => day.DayOfWeek)
                .Select(day => new WorkScheduleDayResponse(
                    day.DayOfWeek,
                    day.StartTime,
                    day.EndTime,
                    day.MealStart,
                    day.MealEnd,
                    day.NetHours))
                .ToArray(),
            schedule.IsActive,
            schedule.ConcurrencyToken,
            schedule.CreatedUtc,
            schedule.ModifiedUtc,
            null);
    }
}
