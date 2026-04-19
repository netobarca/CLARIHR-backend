namespace CLARIHR.Domain.Reports;

public enum ReportExportJobStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    Expired = 5
}
