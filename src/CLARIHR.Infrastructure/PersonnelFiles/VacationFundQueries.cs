using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

/// <summary>
/// Single source of truth for the "available enjoyment days of the active vacation fund" derivation
/// (RF-018/RF-019): Σ over the employee's active periods with <c>GeneratesEnjoymentDays</c> of
/// (granted − net consumed), via <see cref="VacationRules.AvailableDays"/> and
/// <see cref="VacationFundMath.NetConsumedByPeriod"/>. Both the profile's <c>VacationDaysAvailable</c>
/// (<see cref="PersonnelFileEmployeeRepository"/>) and the settlement's pending-days input
/// (<see cref="SettlementRepository"/>) route through here so the two figures never diverge.
/// Returns <c>null</c> when the module has no fund for the employee yet.
/// Tenant isolation rides on the EF global query filter (the caller runs inside a tenant scope).
/// </summary>
internal static class VacationFundQueries
{
    public static async Task<decimal?> GetAvailableEnjoymentDaysAsync(
        ApplicationDbContext dbContext,
        long personnelFileId,
        CancellationToken cancellationToken)
    {
        var periods = await dbContext.PersonnelFileVacationPeriods
            .AsNoTracking()
            .Where(period => period.PersonnelFileId == personnelFileId
                && period.IsActive
                && period.GeneratesEnjoymentDays)
            .ToListAsync(cancellationToken);
        if (periods.Count == 0)
        {
            return null;
        }

        var consuming = VacationRequestStatuses.ConsumesFund.ToArray();
        var allocations = await dbContext.VacationRequestAllocations
            .AsNoTracking()
            .Join(
                dbContext.PersonnelFileVacationRequests.AsNoTracking(),
                allocation => allocation.VacationRequestId,
                request => request.Id,
                (allocation, request) => new { allocation.VacationPeriodId, allocation.Days, request.PersonnelFileId, request.StatusCode })
            .Where(row => row.PersonnelFileId == personnelFileId && consuming.Contains(row.StatusCode))
            .Select(row => new { row.VacationPeriodId, row.Days })
            .ToListAsync(cancellationToken);

        var returnDistributions = await dbContext.VacationReturns
            .AsNoTracking()
            .Join(
                dbContext.PersonnelFileVacationRequests.AsNoTracking(),
                entry => entry.VacationRequestId,
                request => request.Id,
                (entry, request) => new { entry.DistributionJson, request.PersonnelFileId, request.StatusCode })
            .Where(row => row.PersonnelFileId == personnelFileId && consuming.Contains(row.StatusCode))
            .Select(row => row.DistributionJson)
            .ToListAsync(cancellationToken);

        var net = VacationFundMath.NetConsumedByPeriod(
            allocations.Select(row => (row.VacationPeriodId, row.Days)),
            returnDistributions);

        var available = 0;
        foreach (var period in periods)
        {
            var consumed = Math.Max(0, net.GetValueOrDefault(period.Id));
            available += VacationRules.AvailableDays(period, [consumed]);
        }

        return available;
    }
}
