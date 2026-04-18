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
