using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
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

    public async Task<CompensatoryTimeTypeRef?> ResolveTypeAsync(Guid tenantId, Guid typePublicId, CancellationToken cancellationToken) =>
        await dbContext.CompensatoryTimeTypes
            .AsNoTracking()
            .Where(type => type.TenantId == tenantId && type.PublicId == typePublicId && type.IsActive)
            .Select(type => new CompensatoryTimeTypeRef(type.Id, type.Code, type.Name, type.OperationCode, type.CreditFactor))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsProfileRetiredAsync(long personnelFileId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileEmployeeProfiles
            .AsNoTracking()
            .AnyAsync(
                profile => profile.PersonnelFileId == personnelFileId
                    && profile.EmploymentStatusCode == PersonnelFileEmployeeProfile.RetiredEmploymentStatusCode,
                cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileCompensatoryTimeCreditResponse>> GetCreditResponsesAsync(
        Guid personnelFilePublicId, CancellationToken cancellationToken)
    {
        var items = await QueryCreditsWithIncludes()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId)
            .OrderByDescending(item => item.WorkDate)
            .ThenByDescending(item => item.CreatedUtc)
            .ToListAsync(cancellationToken);
        return items.Select(MapCredit).ToArray();
    }

    public async Task<PersonnelFileCompensatoryTimeCreditResponse?> GetCreditResponseAsync(
        Guid personnelFilePublicId, Guid creditPublicId, CancellationToken cancellationToken)
    {
        var item = await QueryCreditsWithIncludes()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == creditPublicId)
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? null : MapCredit(item);
    }

    public Task<PersonnelFileCompensatoryTimeCredit?> GetCreditEntityAsync(
        Guid personnelFilePublicId, Guid creditPublicId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileCompensatoryTimeCredits
            .SingleOrDefaultAsync(
                item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == creditPublicId,
                cancellationToken);

    public Task<long?> GetCreditInternalIdAsync(
        Guid personnelFilePublicId, Guid creditPublicId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileCompensatoryTimeCredits
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == creditPublicId)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public void AddCredit(PersonnelFileCompensatoryTimeCredit entity) =>
        dbContext.PersonnelFileCompensatoryTimeCredits.Add(entity);

    public void AddDocument(PersonnelFileCompensatoryTimeCreditDocument entity) =>
        dbContext.PersonnelFileCompensatoryTimeCreditDocuments.Add(entity);

    public async Task<IReadOnlyCollection<CompensatoryTimeCreditDocumentResponse>> GetDocumentResponsesAsync(
        Guid creditPublicId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileCompensatoryTimeCreditDocuments
            .AsNoTracking()
            .Where(document => document.Credit.PublicId == creditPublicId && document.IsActive)
            .OrderByDescending(document => document.CreatedUtc)
            .Select(document => MapDocument(document))
            .ToArrayAsync(cancellationToken);

    public Task<CompensatoryTimeCreditDocumentResponse?> GetDocumentResponseAsync(
        Guid creditPublicId, Guid documentPublicId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileCompensatoryTimeCreditDocuments
            .AsNoTracking()
            .Where(document => document.Credit.PublicId == creditPublicId && document.PublicId == documentPublicId)
            .Select(document => MapDocument(document))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PersonnelFileCompensatoryTimeCreditDocument?> GetDocumentEntityAsync(
        Guid creditPublicId, Guid documentPublicId, Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileCompensatoryTimeCreditDocuments
            .SingleOrDefaultAsync(
                document => document.Credit.PublicId == creditPublicId
                    && document.PublicId == documentPublicId
                    && document.TenantId == tenantId,
                cancellationToken);

    private IQueryable<PersonnelFileCompensatoryTimeCredit> QueryCreditsWithIncludes() =>
        dbContext.PersonnelFileCompensatoryTimeCredits
            .AsNoTracking()
            .Include(item => item.CompensatoryTimeType);

    private static PersonnelFileCompensatoryTimeCreditResponse MapCredit(PersonnelFileCompensatoryTimeCredit item) =>
        new(
            item.PublicId,
            item.CompensatoryTimeType!.PublicId,
            item.CompensatoryTimeType!.Code,
            item.TypeNameSnapshot,
            item.WorkDate,
            item.StartTime,
            item.EndTime,
            item.HoursWorked,
            item.FactorApplied,
            item.HoursCredited,
            item.IsOverridden,
            item.OverrideNote,
            item.WorkDetail,
            item.AuthorizedByText,
            item.AuthorizerFilePublicId,
            item.AssignedPositionPublicId,
            item.OvertimeRecordPublicId,
            item.StatusCode,
            item.AnnulmentReason,
            item.Notes,
            item.IsActive,
            item.ConcurrencyToken,
            item.CreatedUtc,
            item.ModifiedUtc);

    private static CompensatoryTimeCreditDocumentResponse MapDocument(PersonnelFileCompensatoryTimeCreditDocument document) =>
        new(
            document.PublicId,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.PublicId : (Guid?)null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Code : null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Name : null,
            document.Observations,
            document.FilePublicId,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.IsActive,
            document.ConcurrencyToken,
            document.CreatedUtc,
            document.ModifiedUtc);
}
