namespace CLARIHR.Infrastructure.Reports;

internal sealed record ReportExportGeneratedFile(
    int RowCount,
    string FileName,
    string ContentType);

internal sealed class ReportExportLimitExceededException(int rowCount, int maxRows)
    : Exception($"Report export row count {rowCount} exceeds the maximum allowed row count {maxRows}.");

internal sealed class ReportExportInvalidParametersException(string message) : Exception(message);
