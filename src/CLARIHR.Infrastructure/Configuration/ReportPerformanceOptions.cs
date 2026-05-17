namespace CLARIHR.Infrastructure.Configuration;

public sealed class ReportPerformanceOptions
{
    public const string SectionName = "Reporting:Performance";

    public int MaxSynchronousExportRows { get; init; } = 5_000;

    public int MaxAsyncExportRows { get; init; } = 100_000;

    public int ExportBatchSize { get; init; } = 1_000;

    public int WorkerBatchSize { get; init; } = 2;

    public int WorkerPollIntervalSeconds { get; init; } = 15;

    public int ClaimLeaseMinutes { get; init; } = 15;

    public int MaxAttempts { get; init; } = 3;

    public int ArtifactRetentionHours { get; init; } = 24;

    public int MaxDiagramNodes { get; init; } = 5_000;

    /// <summary>
    /// Maximum rendered document size (bytes) a single export artifact may reach
    /// before generation fails fast with a typed limit error — checked at the
    /// generator, ahead of the downstream storage cap
    /// (<c>Storage:Purposes:ReportExport:MaxSizeBytes</c>). Defense in depth for a
    /// pathological document (technical-debt doc 01 §3.3). Default 50 MB: well
    /// above any legitimate profile PDF, comfortably below the 100 MB storage cap.
    /// </summary>
    public long MaxDocumentBytes { get; init; } = 52_428_800;

    public int NormalizedMaxSynchronousExportRows => Math.Max(1, MaxSynchronousExportRows);

    public int NormalizedMaxAsyncExportRows => Math.Max(NormalizedMaxSynchronousExportRows, MaxAsyncExportRows);

    public int NormalizedExportBatchSize => Math.Max(1, ExportBatchSize);

    public int NormalizedWorkerBatchSize => Math.Max(1, WorkerBatchSize);

    public TimeSpan NormalizedWorkerPollInterval => TimeSpan.FromSeconds(Math.Max(1, WorkerPollIntervalSeconds));

    public TimeSpan NormalizedClaimLease => TimeSpan.FromMinutes(Math.Max(1, ClaimLeaseMinutes));

    public int NormalizedMaxAttempts => Math.Max(1, MaxAttempts);

    public TimeSpan NormalizedArtifactRetention => TimeSpan.FromHours(Math.Max(1, ArtifactRetentionHours));

    public int NormalizedMaxDiagramNodes => Math.Max(1, MaxDiagramNodes);

    public long NormalizedMaxDocumentBytes => Math.Max(1, MaxDocumentBytes);
}
