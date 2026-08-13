using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.SalaryTabulator;
using CLARIHR.Domain.SalaryTabulator;

namespace CLARIHR.Application.Abstractions.SalaryTabulator;

public interface ISalaryTabulatorRepository
{
    void AddLine(SalaryTabulatorLine line);

    void AddChangeRequest(SalaryTabulatorChangeRequest request);

    Task<SalaryTabulatorLine?> GetLineByIdAsync(Guid lineId, CancellationToken cancellationToken);

    Task<bool> LineExistsOutsideTenantAsync(Guid lineId, CancellationToken cancellationToken);

    Task<SalaryTabulatorLineResponse?> GetLineResponseByIdAsync(Guid lineId, CancellationToken cancellationToken);

    Task<PagedResponse<SalaryTabulatorLineListItemResponse>> SearchLinesAsync(
        Guid tenantId,
        string? salaryClassCode,
        string? salaryScaleCode,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SalaryTabulatorLineExportRow>> GetLineExportRowsAsync(
        Guid tenantId,
        string? salaryClassCode,
        string? salaryScaleCode,
        bool? isActive,
        string? search,
        int? maxRows,
        CancellationToken cancellationToken);

    Task<SalaryTabulatorLineSnapshot?> GetActiveLineSnapshotAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string normalizedSalaryScaleCode,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken);

    Task<SalaryTabulatorLine?> GetActiveLineEntityAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string normalizedSalaryScaleCode,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken);

    Task<SalaryTabulatorLine?> FindActiveLineForLegacyCompensationAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string? currencyCode,
        decimal? minAmount,
        decimal? maxAmount,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        CancellationToken cancellationToken);

    Task<bool> HasLineWithEffectiveFromOnOrAfterAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string normalizedSalaryScaleCode,
        DateTime effectiveFromUtc,
        long? excludingLineId,
        CancellationToken cancellationToken);

    Task<bool> HasUncoveredJobProfileCompensationReferenceAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string normalizedSalaryScaleCode,
        DateTime fallbackEffectiveAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// H-14 — plazas whose configured base salary no longer fits the ACTIVE band of the given
    /// (salary class, salary scale) pairs. Used to REPORT after an approval, never to block it: an approved
    /// change is a salary-policy decision and the state of existing plazas must not veto it. Scoped to the
    /// pairs the approval actually touched, so it does not surface pre-existing drift unrelated to the change.
    /// <para>No default implementation on purpose: a report that silently returns nothing fails open.</para>
    /// </summary>
    Task<IReadOnlyCollection<SalaryTabulatorOutOfBandPositionSlot>> GetPositionSlotsOutsideBandAsync(
        Guid tenantId,
        IReadOnlyCollection<(string NormalizedSalaryClassCode, string NormalizedSalaryScaleCode)> affectedKeys,
        CancellationToken cancellationToken);

    Task<SalaryTabulatorChangeRequest?> GetChangeRequestByIdAsync(Guid requestId, CancellationToken cancellationToken);

    Task<bool> ChangeRequestExistsOutsideTenantAsync(Guid requestId, CancellationToken cancellationToken);

    Task<SalaryTabulatorChangeRequestResponse?> GetChangeRequestResponseByIdAsync(Guid requestId, CancellationToken cancellationToken);

    Task<SalaryTabulatorChangeRequestImpactResponse?> GetChangeRequestImpactByIdAsync(Guid requestId, CancellationToken cancellationToken);

    Task<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>> SearchChangeRequestsAsync(
        Guid tenantId,
        SalaryTabulatorChangeRequestStatus? status,
        Guid? requestedByUserId,
        DateTime? effectiveFromUtc,
        DateTime? effectiveToUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}
