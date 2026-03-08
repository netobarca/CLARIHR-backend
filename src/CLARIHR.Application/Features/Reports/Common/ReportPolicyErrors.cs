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

    public static readonly Error Forbidden = new(
        "REPORT_FORBIDDEN",
        "You do not have permission to access the requested report resource.",
        ErrorType.Forbidden);
}
