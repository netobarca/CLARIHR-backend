using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CLARIHR.Api.Common;

public sealed class ReportExportDeliveryService(
    IAuditService auditService,
    IUnitOfWork unitOfWork,
    IOptions<ReportPerformanceOptions> options)
{
    private readonly ReportPerformanceOptions _options = options.Value;

    public int SynchronousReadLimit => _options.NormalizedMaxSynchronousExportRows + 1;

    public int MaxDiagramNodes => _options.NormalizedMaxDiagramNodes;

    public async Task<IActionResult> CreateFileResultAsync<TRow>(
        ControllerBase controller,
        IReadOnlyCollection<TRow> rows,
        string requestedFormat,
        string fileNamePrefix,
        string sheetName,
        string auditEntityType,
        string resourceKey,
        string auditSummary,
        object filters,
        Error invalidFormatError,
        CancellationToken cancellationToken)
    {
        if (!ReportExportFormats.TryNormalize(requestedFormat, out var normalizedFormat))
        {
            return ProblemDetailsFactory.Create(controller.HttpContext, invalidFormatError);
        }

        if (rows.Count > _options.NormalizedMaxSynchronousExportRows)
        {
            return ProblemDetailsFactory.Create(controller.HttpContext, ReportPolicyErrors.ExportTooLarge);
        }

        var stream = new MemoryStream();
        await ReportExportFileWriter.WriteAsync(stream, rows, normalizedFormat, sheetName, cancellationToken);
        stream.Position = 0;

        await auditService.LogAsync(
            BuildExportAuditEntry(auditEntityType, resourceKey, auditSummary, normalizedFormat, filters, rows.Count),
            cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return controller.File(
            stream,
            ReportExportFormats.GetContentType(normalizedFormat),
            $"{fileNamePrefix}.{normalizedFormat}");
    }

    public async Task LogExportAsync(
        string auditEntityType,
        string resourceKey,
        string auditSummary,
        string format,
        object filters,
        int rowCount,
        CancellationToken cancellationToken)
    {
        await auditService.LogAsync(
            BuildExportAuditEntry(auditEntityType, resourceKey, auditSummary, format, filters, rowCount),
            cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static AuditLogEntry BuildExportAuditEntry(
        string auditEntityType,
        string resourceKey,
        string auditSummary,
        string format,
        object filters,
        int rowCount) =>
        new(
            AuditEventTypes.ReportExported,
            auditEntityType,
            null,
            resourceKey,
            AuditActions.Export,
            auditSummary,
            After: new
            {
                resourceKey,
                format,
                filters,
                rowCount
            });
}
