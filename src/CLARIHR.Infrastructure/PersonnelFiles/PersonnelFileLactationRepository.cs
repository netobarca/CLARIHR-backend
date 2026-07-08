using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

/// <summary>
/// EF-backed persistence port of the lactation sub-resource (vacaciones/incapacidades PR-6). The LACTANCIA type
/// resolution only accepts the active incapacity type whose code is exactly <c>LACTANCIA</c>; the projected
/// responses are materialized and mapped in-memory (the schedule set is ordered by <c>SortOrder</c>) and the
/// tracked-entity load (with its schedules) feeds the domain guards.
/// </summary>
internal sealed class PersonnelFileLactationRepository(ApplicationDbContext dbContext) : IPersonnelFileLactationRepository
{
    private const string LactationTypeCode = "LACTANCIA";

    public async Task<long?> ResolveLactationTypeInternalIdAsync(
        Guid tenantId, Guid incapacityTypePublicId, CancellationToken cancellationToken) =>
        await dbContext.IncapacityTypes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.PublicId == incapacityTypePublicId
                && item.IsActive
                && item.Code == LactationTypeCode)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileLactationPeriodResponse>> GetResponsesAsync(
        Guid personnelFilePublicId, CancellationToken cancellationToken)
    {
        var items = await QueryWithIncludes()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId)
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.CreatedUtc)
            .ToListAsync(cancellationToken);
        return items.Select(MapResponse).ToArray();
    }

    public async Task<PersonnelFileLactationPeriodResponse?> GetResponseAsync(
        Guid personnelFilePublicId, Guid lactationPeriodPublicId, CancellationToken cancellationToken)
    {
        var item = await QueryWithIncludes()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == lactationPeriodPublicId)
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? null : MapResponse(item);
    }

    public async Task<PersonnelFileLactationPeriod?> GetEntityAsync(
        Guid personnelFilePublicId, Guid lactationPeriodPublicId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileLactationPeriods
            .Include(item => item.Schedules)
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == lactationPeriodPublicId)
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(PersonnelFileLactationPeriod entity) => dbContext.PersonnelFileLactationPeriods.Add(entity);

    private IQueryable<PersonnelFileLactationPeriod> QueryWithIncludes() =>
        dbContext.PersonnelFileLactationPeriods
            .AsNoTracking()
            .Include(item => item.IncapacityType)
            .Include(item => item.Schedules);

    private static PersonnelFileLactationPeriodResponse MapResponse(PersonnelFileLactationPeriod item) =>
        new(
            item.PublicId,
            item.RequesterFilePublicId,
            item.RequesterNameSnapshot,
            item.IncapacityType!.PublicId,
            item.IncapacityType!.Code,
            item.StartDate,
            item.EndDate,
            item.StatusCode,
            item.AnnulmentReason,
            item.Notes,
            item.Schedules
                .OrderBy(schedule => schedule.SortOrder)
                .Select(schedule => new LactationScheduleResponse(
                    schedule.PublicId,
                    schedule.StartDate,
                    schedule.EndDate,
                    schedule.DailyPermitsCount,
                    schedule.MinutesPerPermit,
                    schedule.SortOrder))
                .ToArray(),
            item.IsActive,
            item.ConcurrencyToken,
            item.CreatedUtc,
            item.ModifiedUtc);
}
