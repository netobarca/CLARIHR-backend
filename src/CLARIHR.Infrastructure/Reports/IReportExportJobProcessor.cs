namespace CLARIHR.Infrastructure.Reports;

internal interface IReportExportJobProcessor
{
    Task<ReportExportWorkerCycleResult> ProcessDueJobsAsync(CancellationToken cancellationToken);
}
