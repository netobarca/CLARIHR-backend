using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.Reports.Common;

public static class ReportPolicyErrors
{
    public static readonly Error ResourceNotSupported = new(
        "REPORT_NOT_AVAILABLE",
        "Reporting is not available for the requested resource.",
        ErrorType.NotFound);

    public static readonly Error FormatNotSupported = new(
        "REPORT_FORMAT_NOT_SUPPORTED",
        "The requested report format is not supported.",
        ErrorType.Validation);

    public static readonly Error ExportTooLarge = new(
        "REPORT_EXPORT_TOO_LARGE",
        "The requested export is too large for synchronous generation.",
        ErrorType.PayloadTooLarge);

    public static readonly Error ExportLimitExceeded = new(
        "REPORT_EXPORT_LIMIT_EXCEEDED",
        "The requested export exceeds an enforced size limit (row count or document size).",
        ErrorType.PayloadTooLarge);

    public static readonly Error ExportJobNotFound = new(
        "REPORT_EXPORT_JOB_NOT_FOUND",
        "The report export job could not be found.",
        ErrorType.NotFound);

    public static readonly Error ExportJobNotReady = new(
        "REPORT_EXPORT_JOB_NOT_READY",
        "The report export job is not ready for download.",
        ErrorType.Conflict);

    public static readonly Error ExportJobExpired = new(
        "REPORT_EXPORT_JOB_EXPIRED",
        "The report export job artifact has expired.",
        ErrorType.Gone);

    public static readonly Error ExportStorageNotConfigured = new(
        "REPORT_EXPORT_STORAGE_NOT_CONFIGURED",
        "Report export storage is not configured.",
        ErrorType.ServiceUnavailable);

    public static readonly Error Forbidden = new(
        "REPORT_FORBIDDEN",
        "You do not have permission to access the requested report resource.",
        ErrorType.Forbidden);

    public static readonly Error ConcurrencyConflict = new(
        "REPORT_CONCURRENCY_CONFLICT",
        "The requested resource was modified by another process. Please reload and try again.",
        ErrorType.Conflict);
}
