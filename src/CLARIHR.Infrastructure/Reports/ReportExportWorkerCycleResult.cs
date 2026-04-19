namespace CLARIHR.Infrastructure.Reports;

internal readonly record struct ReportExportWorkerCycleResult(
    int ClaimedCount,
    int ProcessedCount,
    int SucceededCount,
    int RetriedCount,
    int FailedCount,
    int ConcurrencySkippedCount,
    int ExpiredCount,
    int CleanupDeleteFailureCount)
{
    public bool HadWork =>
        ClaimedCount > 0 ||
        ProcessedCount > 0 ||
        ExpiredCount > 0 ||
        CleanupDeleteFailureCount > 0;
}
