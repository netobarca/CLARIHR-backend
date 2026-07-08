using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>
/// Persistence port of the incapacities sub-resource (vacaciones/incapacidades PR-5). Keeps the EF reads/writes
/// out of the handlers: master public-id → internal-id resolution, the tracked-entity loads that the domain
/// guards mutate, the projected responses (tranche json deserialized in-memory), the employer-cap consumption
/// re-read used both by the balance and by the in-transaction anti-race, and the attachment CRUD.
/// </summary>
public interface IPersonnelFileIncapacityRepository
{
    // ── Master reference resolution (public id → internal id; null when inactive/foreign) ─────────
    Task<long?> ResolveRiskInternalIdAsync(Guid tenantId, Guid riskPublicId, CancellationToken cancellationToken);

    Task<long?> ResolveIncapacityTypeInternalIdAsync(Guid tenantId, Guid typePublicId, CancellationToken cancellationToken);

    Task<long?> ResolveMedicalClinicInternalIdAsync(Guid tenantId, Guid clinicPublicId, CancellationToken cancellationToken);

    Task<long?> ResolvePayrollPeriodInternalIdAsync(Guid tenantId, Guid payrollPeriodPublicId, CancellationToken cancellationToken);

    // ── Incapacity reads ─────────────────────────────────────────────────────────────────────────
    Task<IReadOnlyCollection<PersonnelFileIncapacityResponse>> GetResponsesAsync(
        Guid personnelFilePublicId, CancellationToken cancellationToken);

    Task<PersonnelFileIncapacityResponse?> GetResponseAsync(
        Guid personnelFilePublicId, Guid incapacityPublicId, CancellationToken cancellationToken);

    /// <summary>Tracked entity (with the source-chain navigation) for the domain guards to mutate.</summary>
    Task<PersonnelFileIncapacity?> GetEntityAsync(
        Guid personnelFilePublicId, Guid incapacityPublicId, CancellationToken cancellationToken);

    /// <summary>True when a non-annulled incapacity already extends the given one (chain-lock guard).</summary>
    Task<bool> HasActiveExtensionsAsync(long incapacityId, CancellationToken cancellationToken);

    /// <summary>
    /// True when the employee already has a non-annulled incapacity whose date range overlaps
    /// [<paramref name="startDate"/>, <paramref name="endDate"/>] (open-ended when null), excluding
    /// <paramref name="excludeIncapacityId"/> (the record being edited). RN-14. Contiguous extensions
    /// (start == previous end + 1) do not overlap and are therefore never flagged.
    /// </summary>
    Task<bool> HasOverlappingIncapacityAsync(
        long personnelFileId, DateOnly startDate, DateOnly? endDate, long? excludeIncapacityId, CancellationToken cancellationToken);

    /// <summary>
    /// Σ EmployerDays of the employee's REGISTRADA incapacities whose start year is <paramref name="year"/>
    /// (excluding <paramref name="excludeIncapacityId"/> when recalculating a record). Only REGISTRADA counts
    /// (R-T6). Used both by the balance and by the in-transaction employer-cap re-read (R-T2).
    /// </summary>
    Task<int> GetRegisteredEmployerDaysConsumedAsync(
        long personnelFileId, int year, long? excludeIncapacityId, CancellationToken cancellationToken);

    Task<long?> GetInternalIdAsync(
        Guid personnelFilePublicId, Guid incapacityPublicId, CancellationToken cancellationToken);

    // ── Writes (added to the change tracker; the caller commits through IUnitOfWork) ──────────────
    void Add(PersonnelFileIncapacity entity);

    void AddDocument(PersonnelFileIncapacityDocument entity);

    // ── Document reads ────────────────────────────────────────────────────────────────────────────
    Task<IReadOnlyCollection<IncapacityDocumentResponse>> GetDocumentResponsesAsync(
        Guid incapacityPublicId, CancellationToken cancellationToken);

    Task<IncapacityDocumentResponse?> GetDocumentResponseAsync(
        Guid incapacityPublicId, Guid documentPublicId, CancellationToken cancellationToken);

    Task<PersonnelFileIncapacityDocument?> GetDocumentEntityAsync(
        Guid incapacityPublicId, Guid documentPublicId, Guid tenantId, CancellationToken cancellationToken);
}
