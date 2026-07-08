using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EmployeeRelations;
using CLARIHR.Domain.EmployeeRelations;

namespace CLARIHR.Application.Abstractions.EmployeeRelations;

public interface IDisciplinaryActionCauseRepository
{
    void Add(DisciplinaryActionCause cause);

    Task<DisciplinaryActionCause?> GetByIdAsync(Guid disciplinaryActionCauseId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid disciplinaryActionCauseId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the filtered (TenantId, NormalizedCode) WHERE is_active unique key. Only
    /// ACTIVE causes conflict; pass the public id of the cause being edited in
    /// <paramref name="excludingDisciplinaryActionCauseId"/> to exclude itself.
    /// </summary>
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingDisciplinaryActionCauseId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the cause is referenced by an active disciplinary-action record (logical inactivation is
    /// blocked with <c>DISCIPLINARY_ACTION_CAUSE_IN_USE</c>). PR-1 has no disciplinary-action table yet
    /// (M2/PR-2), so this always returns <c>false</c> today; the real query is wired in PR-4.
    /// </summary>
    Task<bool> IsInUseAsync(Guid tenantId, Guid disciplinaryActionCauseId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="normalizedConceptCode"/> is an active egreso concept in the tenant's
    /// country <c>compensation-concept-types</c> catalog (existe, activo, <c>Nature = Egreso</c>).
    /// Backs the <c>DEDUCTION_CONCEPT_INVALID</c> validation on the cause's optional deduction concept.
    /// </summary>
    Task<bool> IsDeductionConceptValidAsync(
        Guid tenantId,
        string normalizedConceptCode,
        CancellationToken cancellationToken);

    Task<PagedResponse<DisciplinaryActionCauseListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<DisciplinaryActionCauseResponse?> GetResponseByIdAsync(Guid disciplinaryActionCauseId, CancellationToken cancellationToken);
}
