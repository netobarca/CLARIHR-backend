using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Reports.Handlers;

namespace CLARIHR.Infrastructure.Reports;

/// <summary>
/// Dispatcher for report-export generation. Resolves the <see cref="IReportExportHandler"/>
/// registered for the job's <c>ResourceKey</c> and delegates to it. Replaces the former
/// monolithic switch; new resources are added by registering a handler, not by editing
/// this type (strategy pattern — see technical-debt doc 01 §2.1).
/// </summary>
internal sealed class ReportExportJobGenerator : IReportExportJobGenerator
{
    private readonly IReadOnlyDictionary<string, IReportExportHandler> _handlers;

    public ReportExportJobGenerator(IEnumerable<IReportExportHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var map = new Dictionary<string, IReportExportHandler>(StringComparer.Ordinal);
        foreach (var handler in handlers)
        {
            if (!map.TryAdd(handler.ResourceKey, handler))
            {
                throw new InvalidOperationException(
                    $"Duplicate report export handler registered for resource '{handler.ResourceKey}'.");
            }
        }

        _handlers = map;
    }

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        CancellationToken cancellationToken)
    {
        using var document = ReportExportParameters.Parse(job.ParametersJson);
        var parameters = document.RootElement;

        if (!_handlers.TryGetValue(job.ResourceKey, out var handler))
        {
            throw new NotSupportedException($"Report resource '{job.ResourceKey}' is not supported.");
        }

        return await handler.GenerateAsync(job, destination, parameters, cancellationToken);
    }
}
