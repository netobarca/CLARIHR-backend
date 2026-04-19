using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports;

internal interface IReportExportJobGenerator
{
    Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        CancellationToken cancellationToken);
}
