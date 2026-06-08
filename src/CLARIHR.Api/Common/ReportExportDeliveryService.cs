using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CLARIHR.Api.Common;

public sealed class ReportExportDeliveryService(
    IAuditService auditService,
    IUnitOfWork unitOfWork,
    IFileStorageProviderResolver providerResolver,
    IFilePurposeRuleProvider ruleProvider,
    IOptions<ReportPerformanceOptions> options)
{
    private readonly ReportPerformanceOptions _options = options.Value;

    public int SynchronousReadLimit => _options.NormalizedMaxSynchronousExportRows + 1;

    public int MaxDiagramNodes => _options.NormalizedMaxDiagramNodes;

    // REX-G: opens the stored export artifact, encapsulating the storage rule/provider/container
    // resolution previously inlined in ReportExportJobsController.Download. Returns
    // ExportStorageNotConfigured (503) if the file-purpose rule is missing and ExportJobExpired (410)
    // if the blob is gone, so the controller only maps the result to an HTTP response.
    public async Task<Result<Stream>> OpenArtifactStreamAsync(string blobName, CancellationToken cancellationToken)
    {
        var rule = ruleProvider.GetRule(FilePurpose.ReportExport);
        if (rule is null)
        {
            return Result<Stream>.Failure(ReportPolicyErrors.ExportStorageNotConfigured);
        }

        var provider = providerResolver.Resolve(rule.DefaultProvider);
        var containerName = rule.ContainerOverride ?? "clarihr-files";
        var stream = await provider.OpenReadStreamAsync(containerName, blobName, cancellationToken);

        return stream is null
            ? Result<Stream>.Failure(ReportPolicyErrors.ExportJobExpired)
            : Result<Stream>.Success(stream);
    }

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
