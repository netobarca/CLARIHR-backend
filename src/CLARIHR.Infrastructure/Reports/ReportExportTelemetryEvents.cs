using Microsoft.Extensions.Logging;

namespace CLARIHR.Infrastructure.Reports;

internal static class ReportExportTelemetryEvents
{
    public static readonly EventId WorkerCycleCompleted = new(40001, "report_export_worker_cycle_completed");
    public static readonly EventId WorkerCycleEmpty = new(40002, "report_export_worker_cycle_empty");
    public static readonly EventId WorkerCycleFailed = new(40003, "report_export_worker_cycle_failed");
    public static readonly EventId JobStarted = new(40004, "report_export_job_started");
    public static readonly EventId JobSucceeded = new(40005, "report_export_job_succeeded");
    public static readonly EventId JobRetryScheduled = new(40006, "report_export_job_retry_scheduled");
    public static readonly EventId JobFailedTerminal = new(40007, "report_export_job_failed_terminal");
    public static readonly EventId JobClaimConflict = new(40008, "report_export_job_claim_conflict");
    public static readonly EventId ArtifactExpired = new(40009, "report_export_artifact_expired");
    public static readonly EventId ArtifactDeleteFailed = new(40010, "report_export_artifact_delete_failed");

    // Document Generation subdomain (technical-debt doc 01 §6.1). Distinct from
    // the generic Job* events so dashboards can compute renderer-specific p95s
    // (render_duration_ms) and size distributions (pdf_size_bytes) per resource.
    public static readonly EventId PdfRenderStarted = new(40011, "report_export_pdf_render_started");
    public static readonly EventId PdfRenderSucceeded = new(40012, "report_export_pdf_render_succeeded");
}
