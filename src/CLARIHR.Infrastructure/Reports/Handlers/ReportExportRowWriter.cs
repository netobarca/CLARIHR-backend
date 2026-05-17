using CLARIHR.Application.Features.Reports;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Reports.Handlers;

/// <summary>
/// Shared writer for tabular report-export handlers. Encapsulates the row-count
/// cap, file naming and format dispatch that every tabular branch of the former
/// <c>ReportExportJobGenerator</c> switch performed identically.
/// </summary>
internal sealed class ReportExportRowWriter(IOptions<ReportPerformanceOptions> options)
{
    private readonly ReportPerformanceOptions _options = options.Value;

    /// <summary>
    /// Upper bound passed to repository export queries: one past the async cap so
    /// the writer can detect (and reject) result sets that exceed the limit.
    /// </summary>
    public int MaxRowsToRead => _options.NormalizedMaxAsyncExportRows + 1;

    public async Task<ReportExportGeneratedFile> WriteAsync<TRow>(
        ReportExportJob job,
        Stream destination,
        IReadOnlyCollection<TRow> rows,
        string fileNamePrefix,
        string sheetName,
        CancellationToken cancellationToken)
    {
        var maxRows = _options.NormalizedMaxAsyncExportRows;
        if (rows.Count > maxRows)
        {
            throw ReportExportLimitExceededException.ForRowCount(rows.Count, maxRows);
        }

        var normalizedFormat = job.Format;
        var fileName = $"{fileNamePrefix}-{job.PublicId:N}.{normalizedFormat}";
        await ReportExportFileWriter.WriteAsync(destination, rows, normalizedFormat, sheetName, cancellationToken);

        return new ReportExportGeneratedFile(
            rows.Count,
            fileName,
            ReportExportFormats.GetContentType(normalizedFormat));
    }
}
