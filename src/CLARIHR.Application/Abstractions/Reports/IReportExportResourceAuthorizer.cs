using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Abstractions.Reports;

/// <summary>
/// Enforces the per-resource READ permission for a report-export <c>ResourceKey</c>. This is the
/// SINGLE gate shared by creating an export and by reading / downloading / cancelling it, so the
/// read paths cannot under-authorize relative to create (finding REX-1): a caller who lacks the
/// underlying resource's read permission must not be able to inspect, download or cancel its export
/// artifact. Returns
/// <see cref="CLARIHR.Application.Features.Reports.Common.ReportPolicyErrors.ResourceNotSupported"/>
/// for unknown keys.
/// </summary>
public interface IReportExportResourceAuthorizer
{
    Task<Result> EnsureCanReadResourceAsync(
        string normalizedResourceKey,
        Guid companyId,
        CancellationToken cancellationToken);
}
