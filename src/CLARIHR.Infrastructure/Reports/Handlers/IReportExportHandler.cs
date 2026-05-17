using System.Text.Json;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports.Handlers;

/// <summary>
/// Strategy contract: one handler per <c>ReportExportResources</c> key. Replaces
/// the monolithic switch in <c>ReportExportJobGenerator.GenerateAsync</c>; handlers
/// are discovered via DI and dispatched by <see cref="ResourceKey"/>.
/// </summary>
internal interface IReportExportHandler
{
    /// <summary>
    /// The <c>ReportExportResources</c> constant this handler serves. Must be
    /// unique across all registered handlers.
    /// </summary>
    string ResourceKey { get; }

    Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken);
}
