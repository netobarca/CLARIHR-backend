using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>
/// Persistence port of the lactation sub-resource (vacaciones/incapacidades PR-6 — lactancia end-to-end).
/// Keeps the EF reads/writes out of the handlers: the LACTANCIA incapacity-type resolution (only the active
/// type whose code is LACTANCIA resolves), the projected responses (with the ordered schedule set) and the
/// tracked-entity load that the domain guards mutate. Lactation is HR-only (D-18) — there is no self-service
/// write path — and reuses the <c>IncapacityStatuses</c> codes without EN_REVISION.
/// </summary>
public interface IPersonnelFileLactationRepository
{
    /// <summary>
    /// Resolves the LACTANCIA incapacity-type internal id: returns it only when the referenced type is active
    /// AND its code is exactly <c>LACTANCIA</c> (the lactation template — plan §3.4); null otherwise, which the
    /// handler maps to 422 <c>LACTATION_TYPE_INVALID</c>.
    /// </summary>
    Task<long?> ResolveLactationTypeInternalIdAsync(
        Guid tenantId, Guid incapacityTypePublicId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PersonnelFileLactationPeriodResponse>> GetResponsesAsync(
        Guid personnelFilePublicId, CancellationToken cancellationToken);

    Task<PersonnelFileLactationPeriodResponse?> GetResponseAsync(
        Guid personnelFilePublicId, Guid lactationPeriodPublicId, CancellationToken cancellationToken);

    /// <summary>Tracked entity (with the schedule set) for the domain guards to mutate.</summary>
    Task<PersonnelFileLactationPeriod?> GetEntityAsync(
        Guid personnelFilePublicId, Guid lactationPeriodPublicId, CancellationToken cancellationToken);

    void Add(PersonnelFileLactationPeriod entity);
}
