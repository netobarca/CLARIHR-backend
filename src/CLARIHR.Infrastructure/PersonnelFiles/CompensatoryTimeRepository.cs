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
internal sealed class CompensatoryTimeRepository(
    ApplicationDbContext dbContext,
    IPersonnelFileIncapacityRepository incapacityRepository,
    IPersonnelFileVacationRepository vacationRepository) : ICompensatoryTimeRepository
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

    // ── Absences (PR-4) ───────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyCollection<PersonnelFileCompensatoryTimeAbsenceResponse>> GetAbsenceResponsesAsync(
        Guid personnelFilePublicId, CancellationToken cancellationToken)
    {
        var items = await QueryAbsencesWithIncludes()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId)
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.CreatedUtc)
            .ToListAsync(cancellationToken);
        return items.Select(MapAbsence).ToArray();
    }

    public async Task<PersonnelFileCompensatoryTimeAbsenceResponse?> GetAbsenceResponseAsync(
        Guid personnelFilePublicId, Guid absencePublicId, CancellationToken cancellationToken)
    {
        var item = await QueryAbsencesWithIncludes()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == absencePublicId)
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? null : MapAbsence(item);
    }

    public Task<PersonnelFileCompensatoryTimeAbsence?> GetAbsenceEntityAsync(
        Guid personnelFilePublicId, Guid absencePublicId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileCompensatoryTimeAbsences
            .SingleOrDefaultAsync(
                item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == absencePublicId,
                cancellationToken);

    public void AddAbsence(PersonnelFileCompensatoryTimeAbsence entity) =>
        dbContext.PersonnelFileCompensatoryTimeAbsences.Add(entity);

    public Task<bool> HasOverlappingAbsenceAsync(
        long personnelFileId, DateOnly startDate, DateOnly endDate, long? excludeAbsenceId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileCompensatoryTimeAbsences
            .AsNoTracking()
            .Where(absence => absence.PersonnelFileId == personnelFileId
                && absence.StatusCode == CompensatoryTimeStatuses.Registrada
                && (!excludeAbsenceId.HasValue || absence.Id != excludeAbsenceId.Value))
            // Two ranges overlap iff each starts on/before the other ends.
            .AnyAsync(absence => absence.StartDate <= endDate && startDate <= absence.EndDate, cancellationToken);

    public async Task<CompensatoryTimeCrossOverlap> CheckCrossModuleOverlapAsync(
        long personnelFileId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        // The two REQ-001 cross-module queries are isolated here (aclaración №6) — reuse the incapacity and
        // vacation overlap predicates rather than re-implementing the range logic.
        var incapacityOverlap = await incapacityRepository.HasOverlappingIncapacityAsync(
            personnelFileId, startDate, endDate, excludeIncapacityId: null, cancellationToken);
        var vacationOverlap = await vacationRepository.HasOverlappingRequestAsync(
            personnelFileId, startDate, endDate, excludeRequestId: null, cancellationToken);
        return new CompensatoryTimeCrossOverlap(incapacityOverlap, vacationOverlap);
    }

    public Task<bool> PayrollPeriodExistsAsync(Guid tenantId, Guid payrollPeriodPublicId, CancellationToken cancellationToken) =>
        dbContext.PayrollPeriodDefinitions
            .AsNoTracking()
            .AnyAsync(
                period => period.TenantId == tenantId && period.PublicId == payrollPeriodPublicId && period.IsActive,
                cancellationToken);

    public Task<DayOfWeek?> GetPrimaryPlazaRestDayAsync(long personnelFileId, CancellationToken cancellationToken) =>
        vacationRepository.GetPrimaryPlazaRestDayAsync(personnelFileId, cancellationToken);

    public Task<IReadOnlySet<DateOnly>> GetHolidaysInRangeAsync(
        Guid tenantId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken) =>
        vacationRepository.GetHolidaysInRangeAsync(tenantId, startDate, endDate, cancellationToken);

    public async Task<CompensatoryTimeStatementPage> GetStatementPageAsync(
        long personnelFileId,
        DateOnly? fromDate,
        DateOnly? toDate,
        Guid? compensatoryTimeTypePublicId,
        string? statusCode,
        bool includeAnnulled,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(statusCode) ? null : statusCode.Trim().ToUpperInvariant();

        var creditsQuery = dbContext.PersonnelFileCompensatoryTimeCredits
            .AsNoTracking()
            .Where(credit => credit.PersonnelFileId == personnelFileId);
        if (normalizedStatus is not null)
        {
            creditsQuery = creditsQuery.Where(credit => credit.StatusCode == normalizedStatus);
        }
        else if (!includeAnnulled)
        {
            creditsQuery = creditsQuery.Where(credit => credit.StatusCode == CompensatoryTimeStatuses.Registrada);
        }

        if (fromDate is { } creditFrom)
        {
            creditsQuery = creditsQuery.Where(credit => credit.WorkDate >= creditFrom);
        }

        if (toDate is { } creditTo)
        {
            creditsQuery = creditsQuery.Where(credit => credit.WorkDate <= creditTo);
        }

        if (compensatoryTimeTypePublicId is { } creditType)
        {
            creditsQuery = creditsQuery.Where(credit => credit.CompensatoryTimeType!.PublicId == creditType);
        }

        var creditSources = await creditsQuery
            .Select(credit => new StatementSource(
                credit.PublicId,
                credit.WorkDate,
                credit.CreatedUtc,
                CompensatoryTimeMovementKind.Credit,
                credit.HoursCredited,
                credit.StatusCode,
                credit.CompensatoryTimeType!.Code,
                credit.TypeNameSnapshot,
                credit.WorkDetail))
            .ToListAsync(cancellationToken);

        var absencesQuery = dbContext.PersonnelFileCompensatoryTimeAbsences
            .AsNoTracking()
            .Where(absence => absence.PersonnelFileId == personnelFileId);
        if (normalizedStatus is not null)
        {
            absencesQuery = absencesQuery.Where(absence => absence.StatusCode == normalizedStatus);
        }
        else if (!includeAnnulled)
        {
            absencesQuery = absencesQuery.Where(absence => absence.StatusCode == CompensatoryTimeStatuses.Registrada);
        }

        if (fromDate is { } absenceFrom)
        {
            absencesQuery = absencesQuery.Where(absence => absence.StartDate >= absenceFrom);
        }

        if (toDate is { } absenceTo)
        {
            absencesQuery = absencesQuery.Where(absence => absence.StartDate <= absenceTo);
        }

        if (compensatoryTimeTypePublicId is { } absenceType)
        {
            absencesQuery = absencesQuery.Where(absence => absence.CompensatoryTimeType!.PublicId == absenceType);
        }

        var absenceSources = await absencesQuery
            .Select(absence => new StatementSource(
                absence.PublicId,
                absence.StartDate,
                absence.CreatedUtc,
                CompensatoryTimeMovementKind.Absence,
                absence.HoursDebited,
                absence.StatusCode,
                absence.CompensatoryTimeType!.Code,
                absence.TypeNameSnapshot,
                absence.Reason))
            .ToListAsync(cancellationToken);

        var sources = creditSources.Concat(absenceSources).ToDictionary(source => source.PublicId);
        var statement = CompensatoryTimeRules.BuildStatement(
            sources.Values
                .Select(source => new CompensatoryTimeMovement(
                    source.PublicId, source.Date, source.CreatedUtc, source.Kind, source.Hours, source.StatusCode))
                .ToArray());

        var lines = statement.Lines
            .Select(line =>
            {
                var source = sources[line.PublicId];
                return new CompensatoryTimeStatementLineResponse(
                    line.PublicId,
                    line.Kind == CompensatoryTimeMovementKind.Credit ? "ACREDITACION" : "AUSENCIA",
                    line.Date,
                    source.TypeCode,
                    source.TypeName,
                    source.Detail,
                    line.SignedHours,
                    line.StatusCode,
                    line.IsAnnulled,
                    line.RunningBalance);
            })
            .ToList();

        var pageItems = lines
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new CompensatoryTimeStatementPage(
            pageItems,
            lines.Count,
            statement.TotalCredited,
            statement.TotalDebited,
            statement.Balance);
    }

    // ── Company-wide bandeja + exports (PR-5, §3.9) ───────────────────────────────────────────────

    public async Task<CompensatoryTimeMovementBandejaResponse> QueryMovementsAsync(
        QueryCompensatoryTimeMovementsQuery query, CancellationToken cancellationToken)
    {
        var operation = NormalizeOperation(query.OperationCode);
        var includeCredits = operation is null || operation == CompensatoryTimeMovementOperations.Acreditacion;
        var includeAbsences = operation is null || operation == CompensatoryTimeMovementOperations.Ausencia;

        // StatusCounts over the full (non-status) filter, so every status is represented even though the items
        // default to REGISTRADA.
        var statusCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (includeCredits)
        {
            await AccumulateStatusCountsAsync(
                FilteredCredits(query.EmployeeId, query.CompensatoryTimeTypePublicId, query.FromDate, query.ToDate)
                    .Select(credit => credit.StatusCode),
                statusCounts,
                cancellationToken);
        }

        if (includeAbsences)
        {
            await AccumulateStatusCountsAsync(
                FilteredAbsences(query.EmployeeId, query.CompensatoryTimeTypePublicId, query.FromDate, query.ToDate)
                    .Select(absence => absence.StatusCode),
                statusCounts,
                cancellationToken);
        }

        var (exactStatus, restrictToRegistrada) = ResolveItemStatusFilter(query.StatusCode, query.IncludeAnnulled);
        var movements = await LoadMovementRowsAsync(
            query.EmployeeId, query.CompensatoryTimeTypePublicId, query.FromDate, query.ToDate,
            includeCredits, includeAbsences, exactStatus, restrictToRegistrada, maxRows: null, cancellationToken);

        var ordered = movements
            .OrderByDescending(movement => movement.StartDate)
            .ThenByDescending(movement => movement.RegisteredUtc)
            .ToList();

        var items = ordered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToListItem)
            .ToArray();

        return new CompensatoryTimeMovementBandejaResponse(
            items, query.PageNumber, query.PageSize, ordered.Count, statusCounts);
    }

    public async Task<IReadOnlyCollection<MovimientoTiempoCompensatorioExportRow>> GetMovementExportRowsAsync(
        ExportCompensatoryTimeMovementsQuery query, CancellationToken cancellationToken)
    {
        var operation = NormalizeOperation(query.OperationCode);
        var includeCredits = operation is null || operation == CompensatoryTimeMovementOperations.Acreditacion;
        var includeAbsences = operation is null || operation == CompensatoryTimeMovementOperations.Ausencia;

        var (exactStatus, restrictToRegistrada) = ResolveItemStatusFilter(query.StatusCode, query.IncludeAnnulled);
        var movements = await LoadMovementRowsAsync(
            query.EmployeeId, query.CompensatoryTimeTypePublicId, query.FromDate, query.ToDate,
            includeCredits, includeAbsences, exactStatus, restrictToRegistrada, query.MaxRows, cancellationToken);

        return movements
            .OrderByDescending(movement => movement.StartDate)
            .ThenByDescending(movement => movement.RegisteredUtc)
            .Select(movement => new MovimientoTiempoCompensatorioExportRow(
                movement.EmployeeFullName,
                movement.EmployeeCode,
                movement.OperationCode,
                movement.TypeNameSnapshot,
                movement.StartDate,
                movement.EndDate,
                movement.HoursWorked,
                movement.Factor,
                movement.SignedHours,
                movement.Detail,
                movement.AuthorizedByText,
                movement.StatusCode,
                movement.PayrollPeriodLabel,
                movement.PayrollPeriodStart,
                movement.PayrollPeriodEnd,
                movement.RegisteredUtc))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<SaldoTiempoCompensatorioExportRow>> GetBalanceExportRowsAsync(
        ExportCompensatoryTimeBalancesQuery query, CancellationToken cancellationToken)
    {
        // Per-employee aggregation over the VIGENTE (REGISTRADA) movements only — matches GetBalanceAsync /
        // CompensatoryTimeRules.Balance by construction (an ANULADA movement never joins the fund).
        var creditAggregates = await FilteredCredits(query.EmployeeId, null, null, null)
            .Where(credit => credit.StatusCode == CompensatoryTimeStatuses.Registrada)
            .GroupBy(credit => credit.PersonnelFileId)
            .Select(group => new BalanceAggregate(
                group.Key,
                group.Sum(credit => credit.HoursCredited),
                group.Max(credit => (DateOnly?)credit.WorkDate)))
            .ToListAsync(cancellationToken);

        var absenceAggregates = await FilteredAbsences(query.EmployeeId, null, null, null)
            .Where(absence => absence.StatusCode == CompensatoryTimeStatuses.Registrada)
            .GroupBy(absence => absence.PersonnelFileId)
            .Select(group => new BalanceAggregate(
                group.Key,
                group.Sum(absence => absence.HoursDebited),
                group.Max(absence => (DateOnly?)absence.StartDate)))
            .ToListAsync(cancellationToken);

        var credited = creditAggregates.ToDictionary(aggregate => aggregate.PersonnelFileId);
        var debited = absenceAggregates.ToDictionary(aggregate => aggregate.PersonnelFileId);

        var fileIds = credited.Keys.Union(debited.Keys).ToArray();
        if (fileIds.Length == 0)
        {
            return [];
        }

        var employees = await (
            from file in dbContext.PersonnelFiles.AsNoTracking()
            where fileIds.Contains(file.Id)
            join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                on file.Id equals profileEntry.PersonnelFileId into profileGroup
            from profile in profileGroup.DefaultIfEmpty()
            select new { file.Id, file.FullName, EmployeeCode = profile != null ? profile.EmployeeCode : null })
            .ToListAsync(cancellationToken);

        var rows = employees
            .Select(employee =>
            {
                var totalCredited = credited.TryGetValue(employee.Id, out var creditAggregate) ? creditAggregate.Total : 0m;
                var totalDebited = debited.TryGetValue(employee.Id, out var debitAggregate) ? debitAggregate.Total : 0m;
                var lastCredit = credited.TryGetValue(employee.Id, out var lastCreditAggregate) ? lastCreditAggregate.LastDate : null;
                var lastAbsence = debited.TryGetValue(employee.Id, out var lastAbsenceAggregate) ? lastAbsenceAggregate.LastDate : null;
                return new SaldoTiempoCompensatorioExportRow(
                    employee.FullName,
                    employee.EmployeeCode,
                    totalCredited,
                    totalDebited,
                    CompensatoryTimeRules.Balance(totalCredited, totalDebited),
                    MaxDate(lastCredit, lastAbsence));
            })
            .OrderBy(row => row.Empleado)
            .ToList();

        return query.MaxRows is { } maxRows ? rows.Take(maxRows + 1).ToArray() : rows;
    }

    private async Task<List<MovementRow>> LoadMovementRowsAsync(
        Guid? employeeId,
        Guid? typePublicId,
        DateOnly? fromDate,
        DateOnly? toDate,
        bool includeCredits,
        bool includeAbsences,
        string? exactStatus,
        bool restrictToRegistrada,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var movements = new List<MovementRow>();

        if (includeCredits)
        {
            var creditsQuery =
                from credit in WithItemStatus(
                    FilteredCredits(employeeId, typePublicId, fromDate, toDate), exactStatus, restrictToRegistrada)
                join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                    on credit.PersonnelFileId equals profileEntry.PersonnelFileId into profileGroup
                from profile in profileGroup.DefaultIfEmpty()
                orderby credit.WorkDate descending, credit.CreatedUtc descending
                select new MovementRow
                {
                    MovementPublicId = credit.PublicId,
                    PersonnelFilePublicId = credit.PersonnelFile.PublicId,
                    EmployeeFullName = credit.PersonnelFile.FullName,
                    EmployeeCode = profile != null ? profile.EmployeeCode : null,
                    OperationCode = CompensatoryTimeMovementOperations.Acreditacion,
                    CompensatoryTimeTypePublicId = credit.CompensatoryTimeType!.PublicId,
                    CompensatoryTimeTypeCode = credit.CompensatoryTimeType!.Code,
                    TypeNameSnapshot = credit.TypeNameSnapshot,
                    StartDate = credit.WorkDate,
                    EndDate = null,
                    HoursWorked = credit.HoursWorked,
                    Factor = credit.FactorApplied,
                    SignedHours = credit.HoursCredited,
                    Detail = credit.WorkDetail,
                    AuthorizedByText = credit.AuthorizedByText,
                    StatusCode = credit.StatusCode,
                    PayrollPeriodLabel = null,
                    PayrollPeriodStart = null,
                    PayrollPeriodEnd = null,
                    RegisteredUtc = credit.CreatedUtc,
                };

            var creditsList = maxRows is { } creditMax ? creditsQuery.Take(creditMax + 1) : creditsQuery;
            movements.AddRange(await creditsList.ToListAsync(cancellationToken));
        }

        if (includeAbsences)
        {
            var absencesQuery =
                from absence in WithItemStatus(
                    FilteredAbsences(employeeId, typePublicId, fromDate, toDate), exactStatus, restrictToRegistrada)
                join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                    on absence.PersonnelFileId equals profileEntry.PersonnelFileId into profileGroup
                from profile in profileGroup.DefaultIfEmpty()
                join periodEntry in dbContext.PayrollPeriodDefinitions.AsNoTracking()
                    on absence.PayrollPeriodPublicId equals (Guid?)periodEntry.PublicId into periodGroup
                from period in periodGroup.DefaultIfEmpty()
                orderby absence.StartDate descending, absence.CreatedUtc descending
                select new MovementRow
                {
                    MovementPublicId = absence.PublicId,
                    PersonnelFilePublicId = absence.PersonnelFile.PublicId,
                    EmployeeFullName = absence.PersonnelFile.FullName,
                    EmployeeCode = profile != null ? profile.EmployeeCode : null,
                    OperationCode = CompensatoryTimeMovementOperations.Ausencia,
                    CompensatoryTimeTypePublicId = absence.CompensatoryTimeType!.PublicId,
                    CompensatoryTimeTypeCode = absence.CompensatoryTimeType!.Code,
                    TypeNameSnapshot = absence.TypeNameSnapshot,
                    StartDate = absence.StartDate,
                    EndDate = absence.EndDate,
                    HoursWorked = null,
                    Factor = null,
                    SignedHours = -absence.HoursDebited,
                    Detail = absence.Reason,
                    AuthorizedByText = null,
                    StatusCode = absence.StatusCode,
                    PayrollPeriodLabel = period != null ? period.Label : null,
                    PayrollPeriodStart = period != null ? period.StartDate : (DateOnly?)null,
                    PayrollPeriodEnd = period != null ? period.EndDate : (DateOnly?)null,
                    RegisteredUtc = absence.CreatedUtc,
                };

            var absencesList = maxRows is { } absenceMax ? absencesQuery.Take(absenceMax + 1) : absencesQuery;
            movements.AddRange(await absencesList.ToListAsync(cancellationToken));
        }

        return movements;
    }

    private IQueryable<PersonnelFileCompensatoryTimeCredit> FilteredCredits(
        Guid? employeeId, Guid? typePublicId, DateOnly? fromDate, DateOnly? toDate)
    {
        var query = dbContext.PersonnelFileCompensatoryTimeCredits.AsNoTracking();

        if (employeeId is { } employeePublicId)
        {
            query = query.Where(credit => credit.PersonnelFile.PublicId == employeePublicId);
        }

        if (typePublicId is { } typeId)
        {
            query = query.Where(credit => credit.CompensatoryTimeType!.PublicId == typeId);
        }

        if (fromDate is { } from)
        {
            query = query.Where(credit => credit.WorkDate >= from);
        }

        if (toDate is { } to)
        {
            query = query.Where(credit => credit.WorkDate <= to);
        }

        return query;
    }

    private IQueryable<PersonnelFileCompensatoryTimeAbsence> FilteredAbsences(
        Guid? employeeId, Guid? typePublicId, DateOnly? fromDate, DateOnly? toDate)
    {
        var query = dbContext.PersonnelFileCompensatoryTimeAbsences.AsNoTracking();

        if (employeeId is { } employeePublicId)
        {
            query = query.Where(absence => absence.PersonnelFile.PublicId == employeePublicId);
        }

        if (typePublicId is { } typeId)
        {
            query = query.Where(absence => absence.CompensatoryTimeType!.PublicId == typeId);
        }

        if (fromDate is { } from)
        {
            query = query.Where(absence => absence.StartDate >= from);
        }

        if (toDate is { } to)
        {
            query = query.Where(absence => absence.StartDate <= to);
        }

        return query;
    }

    private static IQueryable<PersonnelFileCompensatoryTimeCredit> WithItemStatus(
        IQueryable<PersonnelFileCompensatoryTimeCredit> query, string? exactStatus, bool restrictToRegistrada) =>
        exactStatus is not null ? query.Where(credit => credit.StatusCode == exactStatus)
        : restrictToRegistrada ? query.Where(credit => credit.StatusCode == CompensatoryTimeStatuses.Registrada)
        : query;

    private static IQueryable<PersonnelFileCompensatoryTimeAbsence> WithItemStatus(
        IQueryable<PersonnelFileCompensatoryTimeAbsence> query, string? exactStatus, bool restrictToRegistrada) =>
        exactStatus is not null ? query.Where(absence => absence.StatusCode == exactStatus)
        : restrictToRegistrada ? query.Where(absence => absence.StatusCode == CompensatoryTimeStatuses.Registrada)
        : query;

    private static async Task AccumulateStatusCountsAsync(
        IQueryable<string> statuses, Dictionary<string, int> counts, CancellationToken cancellationToken)
    {
        var grouped = await statuses
            .GroupBy(status => status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        foreach (var entry in grouped)
        {
            counts[entry.Status] = (counts.TryGetValue(entry.Status, out var existing) ? existing : 0) + entry.Count;
        }
    }

    /// <summary>Item filter (§3.9): an explicit status filters to exactly that status; otherwise the default
    /// excludes ANULADA unless <paramref name="includeAnnulled"/> is set.</summary>
    private static (string? ExactStatus, bool RestrictToRegistrada) ResolveItemStatusFilter(string? statusCode, bool includeAnnulled)
    {
        var normalized = string.IsNullOrWhiteSpace(statusCode) ? null : statusCode.Trim().ToUpperInvariant();
        return normalized is not null ? (normalized, false) : (null, !includeAnnulled);
    }

    private static string? NormalizeOperation(string? operationCode) =>
        string.IsNullOrWhiteSpace(operationCode) ? null : operationCode.Trim().ToUpperInvariant();

    private static DateOnly? MaxDate(DateOnly? left, DateOnly? right) =>
        left is null ? right : right is null ? left : left.Value >= right.Value ? left : right;

    private static CompensatoryTimeMovementListItemResponse ToListItem(MovementRow movement) =>
        new(
            movement.MovementPublicId,
            movement.PersonnelFilePublicId,
            movement.EmployeeFullName,
            movement.EmployeeCode,
            movement.OperationCode,
            movement.CompensatoryTimeTypePublicId,
            movement.CompensatoryTimeTypeCode,
            movement.TypeNameSnapshot,
            movement.StartDate,
            movement.EndDate,
            movement.HoursWorked,
            movement.Factor,
            movement.SignedHours,
            movement.Detail,
            movement.AuthorizedByText,
            movement.StatusCode,
            movement.PayrollPeriodLabel,
            movement.PayrollPeriodStart,
            movement.PayrollPeriodEnd,
            movement.RegisteredUtc);

    /// <summary>Common in-memory shape of a bandeja/export movement (a credit or an absence).</summary>
    private sealed class MovementRow
    {
        public Guid MovementPublicId { get; init; }

        public Guid PersonnelFilePublicId { get; init; }

        public string EmployeeFullName { get; init; } = string.Empty;

        public string? EmployeeCode { get; init; }

        public string OperationCode { get; init; } = string.Empty;

        public Guid CompensatoryTimeTypePublicId { get; init; }

        public string CompensatoryTimeTypeCode { get; init; } = string.Empty;

        public string TypeNameSnapshot { get; init; } = string.Empty;

        public DateOnly StartDate { get; init; }

        public DateOnly? EndDate { get; init; }

        public decimal? HoursWorked { get; init; }

        public decimal? Factor { get; init; }

        public decimal SignedHours { get; init; }

        public string Detail { get; init; } = string.Empty;

        public string? AuthorizedByText { get; init; }

        public string StatusCode { get; init; } = string.Empty;

        public string? PayrollPeriodLabel { get; init; }

        public DateOnly? PayrollPeriodStart { get; init; }

        public DateOnly? PayrollPeriodEnd { get; init; }

        public DateTime RegisteredUtc { get; init; }
    }

    private sealed record BalanceAggregate(long PersonnelFileId, decimal Total, DateOnly? LastDate);

    /// <summary>In-memory display projection of a fund movement fed to <see cref="CompensatoryTimeRules.BuildStatement"/>.</summary>
    private sealed record StatementSource(
        Guid PublicId,
        DateOnly Date,
        DateTime CreatedUtc,
        CompensatoryTimeMovementKind Kind,
        decimal Hours,
        string StatusCode,
        string TypeCode,
        string TypeName,
        string Detail);

    private IQueryable<PersonnelFileCompensatoryTimeAbsence> QueryAbsencesWithIncludes() =>
        dbContext.PersonnelFileCompensatoryTimeAbsences
            .AsNoTracking()
            .Include(item => item.CompensatoryTimeType);

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

    private static PersonnelFileCompensatoryTimeAbsenceResponse MapAbsence(PersonnelFileCompensatoryTimeAbsence item) =>
        new(
            item.PublicId,
            item.CompensatoryTimeType!.PublicId,
            item.CompensatoryTimeType!.Code,
            item.TypeNameSnapshot,
            item.StartDate,
            item.EndDate,
            item.HoursDebited,
            item.Reason,
            item.PayrollPeriodPublicId,
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
