using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

/// <summary>
/// EF-backed persistence port of the compensatory-time fund (REQ-002 PR-2). The balance is the aggregate
/// of the VIGENTE credits minus the VIGENTE absences (single source of truth); the estado de cuenta
/// projects both sub-resources into the common movement shape and delegates the running balance to
/// <see cref="CompensatoryTimeRules.BuildStatement"/>; the advisory lock serializes balance-reducing writes
/// per (tenant, employee) so the never-negative fund invariant survives concurrent debits (RN-03).
/// </summary>
internal sealed class CompensatoryTimeRepository(ApplicationDbContext dbContext) : ICompensatoryTimeRepository
{
    // RA-1: a fixed class id namespaces this advisory lock against any other advisory-lock use; the object
    // id is derived deterministically from (tenant, personnel file) so every balance-reducing mutation of one
    // employee's fund contends on the same lock. Executed on the context's current transaction (the handler
    // opens one), pg_advisory_xact_lock holds until that transaction commits/rolls back.
    private const int FundMutationLockClassId = 0x43_54_46_44; // "CTFD" — compensatory-time fund

    public async Task<decimal> GetBalanceAsync(long personnelFileId, CancellationToken cancellationToken)
    {
        var credited = await dbContext.PersonnelFileCompensatoryTimeCredits
            .AsNoTracking()
            .Where(credit => credit.PersonnelFileId == personnelFileId
                && credit.StatusCode == CompensatoryTimeStatuses.Registrada)
            .SumAsync(credit => (decimal?)credit.HoursCredited, cancellationToken) ?? 0m;

        var debited = await dbContext.PersonnelFileCompensatoryTimeAbsences
            .AsNoTracking()
            .Where(absence => absence.PersonnelFileId == personnelFileId
                && absence.StatusCode == CompensatoryTimeStatuses.Registrada)
            .SumAsync(absence => (decimal?)absence.HoursDebited, cancellationToken) ?? 0m;

        return CompensatoryTimeRules.Balance(credited, debited);
    }

    public async Task<CompensatoryTimeStatement> GetStatementAsync(
        long personnelFileId,
        bool includeAnnulled,
        CancellationToken cancellationToken)
    {
        var credits = await dbContext.PersonnelFileCompensatoryTimeCredits
            .AsNoTracking()
            .Where(credit => credit.PersonnelFileId == personnelFileId)
            .Where(credit => includeAnnulled || credit.StatusCode == CompensatoryTimeStatuses.Registrada)
            .Select(credit => new CompensatoryTimeMovement(
                credit.PublicId,
                credit.WorkDate,
                credit.CreatedUtc,
                CompensatoryTimeMovementKind.Credit,
                credit.HoursCredited,
                credit.StatusCode))
            .ToListAsync(cancellationToken);

        var absences = await dbContext.PersonnelFileCompensatoryTimeAbsences
            .AsNoTracking()
            .Where(absence => absence.PersonnelFileId == personnelFileId)
            .Where(absence => includeAnnulled || absence.StatusCode == CompensatoryTimeStatuses.Registrada)
            .Select(absence => new CompensatoryTimeMovement(
                absence.PublicId,
                absence.StartDate,
                absence.CreatedUtc,
                CompensatoryTimeMovementKind.Absence,
                absence.HoursDebited,
                absence.StatusCode))
            .ToListAsync(cancellationToken);

        return CompensatoryTimeRules.BuildStatement([.. credits, .. absences]);
    }

    public Task AcquireFundLockAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken)
    {
        var tenantKey = BitConverter.ToInt32(tenantId.ToByteArray(), 0);
        var objectKey = unchecked(tenantKey ^ (int)personnelFileId);
        return dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}, {1})",
            new object[] { FundMutationLockClassId, objectKey },
            cancellationToken);
    }
}
