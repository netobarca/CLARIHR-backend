using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>
/// Persistence port of the compensatory-time fund (REQ-002 PR-2). The fund balance is a DERIVED aggregate
/// (Σ credited − Σ debited over VIGENTE movements) — <see cref="GetBalanceAsync"/> is its single source of
/// truth, consumed by the estado de cuenta, the profile balance, the write validations and the liquidation
/// line. <see cref="AcquireFundLockAsync"/> serializes every balance-reducing write per (tenant, employee)
/// so the never-negative invariant survives concurrent debits (RN-03; the default is a no-op for test fakes
/// that have no PostgreSQL — the EF repository takes the real advisory lock).
/// <para>The bandeja / export queries and the cross-module overlap queries are added in PR-3/PR-4/PR-5.</para>
/// </summary>
public interface ICompensatoryTimeRepository
{
    /// <summary>
    /// Fund balance for the employee: Σ credited − Σ debited over REGISTRADA movements (RN-03). Single
    /// source of truth of the fund arithmetic (delegates to <see cref="CompensatoryTimeRules.Balance"/>).
    /// </summary>
    Task<decimal> GetBalanceAsync(long personnelFileId, CancellationToken cancellationToken);

    /// <summary>
    /// Estado de cuenta with a running balance (credits + absences projected into a common movement shape,
    /// ordered chronologically by <see cref="CompensatoryTimeRules.BuildStatement"/>). When
    /// <paramref name="includeAnnulled"/> is false the ANULADA movements are excluded from the projection.
    /// </summary>
    Task<CompensatoryTimeStatement> GetStatementAsync(
        long personnelFileId,
        bool includeAnnulled,
        CancellationToken cancellationToken);

    /// <summary>
    /// Serializes balance-reducing writes (create/edit absence; edit/annul credit) per (tenant, employee)
    /// with a transaction-scoped PostgreSQL advisory lock, closing the check-then-act TOCTOU on the
    /// never-negative fund invariant (RN-03). Must run inside an open transaction (the handler opens one);
    /// the lock releases on commit/rollback. The default is a no-op (test fakes have no PostgreSQL); the EF
    /// repository takes the real advisory lock.
    /// </summary>
    Task AcquireFundLockAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    // ── Type reference resolution (public id → snapshot; null when inactive/foreign) ──────────────

    /// <summary>
    /// Resolves an ACTIVE compensatory-time type of the tenant to its internal id + code/name/operation/factor
    /// snapshot (null when the type is inactive or belongs to another tenant). The operation and factor are
    /// snapshotted on the credit at registration (RN-02/RN-04).
    /// </summary>
    Task<CompensatoryTimeTypeRef?> ResolveTypeAsync(Guid tenantId, Guid typePublicId, CancellationToken cancellationToken);

    /// <summary>True when the employee's profile is RETIRADO (the fund is frozen — aclaración №9).</summary>
    Task<bool> IsProfileRetiredAsync(long personnelFileId, CancellationToken cancellationToken);

    // ── Credit reads ──────────────────────────────────────────────────────────────────────────────
    Task<IReadOnlyCollection<PersonnelFileCompensatoryTimeCreditResponse>> GetCreditResponsesAsync(
        Guid personnelFilePublicId, CancellationToken cancellationToken);

    Task<PersonnelFileCompensatoryTimeCreditResponse?> GetCreditResponseAsync(
        Guid personnelFilePublicId, Guid creditPublicId, CancellationToken cancellationToken);

    /// <summary>Tracked entity for the domain guards to mutate.</summary>
    Task<PersonnelFileCompensatoryTimeCredit?> GetCreditEntityAsync(
        Guid personnelFilePublicId, Guid creditPublicId, CancellationToken cancellationToken);

    Task<long?> GetCreditInternalIdAsync(
        Guid personnelFilePublicId, Guid creditPublicId, CancellationToken cancellationToken);

    // ── Writes (added to the change tracker; the caller commits through IUnitOfWork) ──────────────
    void AddCredit(PersonnelFileCompensatoryTimeCredit entity);

    void AddDocument(PersonnelFileCompensatoryTimeCreditDocument entity);

    // ── Credit-document reads ─────────────────────────────────────────────────────────────────────
    Task<IReadOnlyCollection<CompensatoryTimeCreditDocumentResponse>> GetDocumentResponsesAsync(
        Guid creditPublicId, CancellationToken cancellationToken);

    Task<CompensatoryTimeCreditDocumentResponse?> GetDocumentResponseAsync(
        Guid creditPublicId, Guid documentPublicId, CancellationToken cancellationToken);

    Task<PersonnelFileCompensatoryTimeCreditDocument?> GetDocumentEntityAsync(
        Guid creditPublicId, Guid documentPublicId, Guid tenantId, CancellationToken cancellationToken);

    // ── Absence reads (PR-4) ──────────────────────────────────────────────────────────────────────
    Task<IReadOnlyCollection<PersonnelFileCompensatoryTimeAbsenceResponse>> GetAbsenceResponsesAsync(
        Guid personnelFilePublicId, CancellationToken cancellationToken);

    Task<PersonnelFileCompensatoryTimeAbsenceResponse?> GetAbsenceResponseAsync(
        Guid personnelFilePublicId, Guid absencePublicId, CancellationToken cancellationToken);

    /// <summary>Tracked entity for the domain guards to mutate.</summary>
    Task<PersonnelFileCompensatoryTimeAbsence?> GetAbsenceEntityAsync(
        Guid personnelFilePublicId, Guid absencePublicId, CancellationToken cancellationToken);

    void AddAbsence(PersonnelFileCompensatoryTimeAbsence entity);

    // ── Overlaps + imputation (RN-05 / P-14) ───────────────────────────────────────────────────────

    /// <summary>
    /// True when the employee already has a REGISTRADA compensatory-time absence whose date range overlaps
    /// [<paramref name="startDate"/>, <paramref name="endDate"/>], excluding <paramref name="excludeAbsenceId"/>
    /// (the record being edited). RN-05.
    /// </summary>
    Task<bool> HasOverlappingAbsenceAsync(
        long personnelFileId, DateOnly startDate, DateOnly endDate, long? excludeAbsenceId, CancellationToken cancellationToken);

    /// <summary>
    /// Isolates the two REQ-001 cross-module overlap queries (aclaración №6): whether the range overlaps a live
    /// incapacity (reuses <c>HasOverlappingIncapacityAsync</c>) and/or a live vacation request/enjoyment
    /// (reuses <c>HasOverlappingRequestAsync</c>). If REQ-001 were absent this is where the degraded mode toggles
    /// them off.
    /// </summary>
    Task<CompensatoryTimeCrossOverlap> CheckCrossModuleOverlapAsync(
        long personnelFileId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);

    /// <summary>True when the tenant has an ACTIVE payroll-period instance (REQ-001 master) with this public id (P-14).</summary>
    Task<bool> PayrollPeriodExistsAsync(Guid tenantId, Guid payrollPeriodPublicId, CancellationToken cancellationToken);

    // ── Absence-hours suggestion inputs (REQ-001; §3.5) ───────────────────────────────────────────

    /// <summary>The weekly rest day of the employee's primary active plaza (null → resolve against the company preference).</summary>
    Task<DayOfWeek?> GetPrimaryPlazaRestDayAsync(long personnelFileId, CancellationToken cancellationToken);

    /// <summary>The active tenant holidays that fall in the [start, end] range (suggestion exclusion).</summary>
    Task<IReadOnlySet<DateOnly>> GetHolidaysInRangeAsync(
        Guid tenantId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);

    // ── Estado de cuenta (PR-4, §3.9) ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A filtered, paginated estado-de-cuenta page: the running balance is computed by
    /// <see cref="CompensatoryTime.CompensatoryTimeRules.BuildStatement"/> over the WHOLE filtered set (so a
    /// page's running balance carries the accumulated offset — R-T9), then the requested page is sliced. The
    /// totals cover the whole filtered set; with no filters the balance equals <see cref="GetBalanceAsync"/>.
    /// </summary>
    Task<CompensatoryTimeStatementPage> GetStatementPageAsync(
        long personnelFileId,
        DateOnly? fromDate,
        DateOnly? toDate,
        Guid? compensatoryTimeTypePublicId,
        string? statusCode,
        bool includeAnnulled,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    // ── Company-wide bandeja + exports (PR-5, §3.9) ───────────────────────────────────────────────

    /// <summary>
    /// A paginated, filterable page of the company-wide movements bandeja: credits and absences projected into
    /// one movement stream (ordered newest-first by movement date), plus per-status counts. The StatusCounts
    /// cover EVERY status (respecting the non-status filters), while the items default to REGISTRADA when no
    /// status is supplied and <c>includeAnnulled</c> is false (ANULADA excluded by default).
    /// </summary>
    Task<CompensatoryTimeMovementBandejaResponse> QueryMovementsAsync(
        QueryCompensatoryTimeMovementsQuery query, CancellationToken cancellationToken);

    /// <summary>The filtered movement export rows (same filters as the bandeja; ANULADA excluded unless requested).</summary>
    Task<IReadOnlyCollection<MovimientoTiempoCompensatorioExportRow>> GetMovementExportRowsAsync(
        ExportCompensatoryTimeMovementsQuery query, CancellationToken cancellationToken);

    /// <summary>The per-employee fund balance export rows (only employees with at least one VIGENTE movement).</summary>
    Task<IReadOnlyCollection<SaldoTiempoCompensatorioExportRow>> GetBalanceExportRowsAsync(
        ExportCompensatoryTimeBalancesQuery query, CancellationToken cancellationToken);
}
