using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Reports;

public sealed class ReportExportJob : TenantEntity
{
    private ReportExportJob()
    {
    }

    private ReportExportJob(
        Guid tenantId,
        string resourceKey,
        string format,
        string parametersJson,
        string requestedByUserId,
        DateTime queuedUtc)
    {
        SetTenantId(tenantId);
        ResourceKey = NormalizeResourceKey(resourceKey);
        Format = NormalizeFormat(format);
        ParametersJson = string.IsNullOrWhiteSpace(parametersJson) ? "{}" : parametersJson;
        RequestedByUserId = requestedByUserId.Trim();
        Status = ReportExportJobStatus.Queued;
        QueuedUtc = queuedUtc;
    }

    public string ResourceKey { get; private set; } = string.Empty;

    public string Format { get; private set; } = string.Empty;

    public string ParametersJson { get; private set; } = "{}";

    public string RequestedByUserId { get; private set; } = string.Empty;

    public ReportExportJobStatus Status { get; private set; }

    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    public DateTime QueuedUtc { get; private set; }

    public DateTime? StartedUtc { get; private set; }

    public DateTime? CompletedUtc { get; private set; }

    public DateTime? ExpiresUtc { get; private set; }

    public DateTime? LeaseUntilUtc { get; private set; }

    public string? WorkerId { get; private set; }

    public int Attempts { get; private set; }

    public int? RowCount { get; private set; }

    public string? ArtifactBlobName { get; private set; }

    public string? ArtifactFileName { get; private set; }

    public string? ArtifactContentType { get; private set; }

    public long? ArtifactSizeBytes { get; private set; }

    public string? LastErrorCode { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public static ReportExportJob Create(
        Guid tenantId,
        string resourceKey,
        string format,
        string parametersJson,
        string requestedByUserId,
        DateTime queuedUtc)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedByUserId);

        return new ReportExportJob(tenantId, resourceKey, format, parametersJson, requestedByUserId, queuedUtc);
    }

    public bool CanBeClaimed(DateTime utcNow) =>
        Status == ReportExportJobStatus.Queued ||
        (Status == ReportExportJobStatus.Running && LeaseUntilUtc.HasValue && LeaseUntilUtc.Value <= utcNow);

    public bool CanBeCancelled() =>
        Status is ReportExportJobStatus.Queued or ReportExportJobStatus.Running;

    public void MarkRunning(string workerId, DateTime utcNow, DateTime leaseUntilUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        Status = ReportExportJobStatus.Running;
        StartedUtc ??= utcNow;
        LeaseUntilUtc = leaseUntilUtc;
        WorkerId = workerId.Trim();
        Attempts++;
        LastErrorCode = null;
        LastErrorMessage = null;
        RefreshConcurrencyToken();
    }

    public void MarkSucceeded(
        int rowCount,
        string blobName,
        string fileName,
        string contentType,
        long sizeBytes,
        DateTime completedUtc,
        DateTime expiresUtc)
    {
        if (rowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowCount), "Row count cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        Status = ReportExportJobStatus.Succeeded;
        RowCount = rowCount;
        ArtifactBlobName = blobName.Trim();
        ArtifactFileName = fileName.Trim();
        ArtifactContentType = contentType.Trim();
        ArtifactSizeBytes = sizeBytes;
        CompletedUtc = completedUtc;
        ExpiresUtc = expiresUtc;
        LeaseUntilUtc = null;
        WorkerId = null;
        LastErrorCode = null;
        LastErrorMessage = null;
        RefreshConcurrencyToken();
    }

    public void MarkProcessingFailed(
        string errorCode,
        string errorMessage,
        DateTime failedUtc,
        int maxAttempts)
    {
        LastErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "REPORT_EXPORT_FAILED" : errorCode.Trim();
        LastErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Report export failed." : errorMessage.Trim();
        CompletedUtc = failedUtc;
        LeaseUntilUtc = null;
        WorkerId = null;
        Status = Attempts < Math.Max(1, maxAttempts)
            ? ReportExportJobStatus.Queued
            : ReportExportJobStatus.Failed;
        RefreshConcurrencyToken();
    }

    public void Cancel(DateTime cancelledUtc)
    {
        if (!CanBeCancelled())
        {
            return;
        }

        Status = ReportExportJobStatus.Cancelled;
        CompletedUtc = cancelledUtc;
        LeaseUntilUtc = null;
        WorkerId = null;
        RefreshConcurrencyToken();
    }

    public void MarkExpired(DateTime expiredUtc)
    {
        if (Status != ReportExportJobStatus.Succeeded)
        {
            return;
        }

        Status = ReportExportJobStatus.Expired;
        CompletedUtc = expiredUtc;
        LeaseUntilUtc = null;
        WorkerId = null;
        RefreshConcurrencyToken();
    }

    private static string NormalizeResourceKey(string resourceKey) =>
        resourceKey.Trim().ToUpperInvariant();

    private static string NormalizeFormat(string format) =>
        format.Trim().ToLowerInvariant();

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
