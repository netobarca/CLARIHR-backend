namespace CLARIHR.Infrastructure.Reports;

internal sealed record ReportExportGeneratedFile(
    int RowCount,
    string FileName,
    string ContentType);

internal sealed class ReportExportLimitExceededException : Exception
{
    private ReportExportLimitExceededException(string message) : base(message)
    {
    }

    public static ReportExportLimitExceededException ForRowCount(int rowCount, int maxRows) =>
        new($"Report export row count {rowCount} exceeds the maximum allowed row count {maxRows}.");

    public static ReportExportLimitExceededException ForDocumentSize(long documentBytes, long maxDocumentBytes) =>
        new($"Report export document size {documentBytes} bytes exceeds the maximum allowed size {maxDocumentBytes} bytes.");
}

internal sealed class ReportExportInvalidParametersException(string message) : Exception(message);
