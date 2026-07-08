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
}
