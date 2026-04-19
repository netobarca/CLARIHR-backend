using CLARIHR.Application.Features.Reports;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Application.UnitTests;

public sealed class ReportExportJobDomainTests
{
    [Fact]
    public void Create_ShouldNormalizeResourceAndFormat_AndStartQueued()
    {
        var tenantId = Guid.NewGuid();
        var queuedUtc = new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc);

        var job = ReportExportJob.Create(
            tenantId,
            " personnel_files ",
            " XLSX ",
            "{}",
            "user-1",
            queuedUtc);

        Assert.Equal(tenantId, job.TenantId);
        Assert.Equal("PERSONNEL_FILES", job.ResourceKey);
        Assert.Equal("xlsx", job.Format);
        Assert.Equal(ReportExportJobStatus.Queued, job.Status);
        Assert.True(job.CanBeClaimed(queuedUtc));
    }

    [Fact]
    public void MarkRunning_ThenProcessingFailed_ShouldRetryUntilMaxAttempts()
    {
        var job = CreateJob();
        var firstToken = job.ConcurrencyToken;
        var utcNow = new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc);

        job.MarkRunning("worker-1", utcNow, utcNow.AddMinutes(15));
        job.MarkProcessingFailed("REPORT_EXPORT_LIMIT_EXCEEDED", "Too many rows.", utcNow.AddMinutes(1), maxAttempts: 2);

        Assert.Equal(ReportExportJobStatus.Queued, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.NotEqual(firstToken, job.ConcurrencyToken);

        job.MarkRunning("worker-1", utcNow.AddMinutes(2), utcNow.AddMinutes(17));
        job.MarkProcessingFailed("REPORT_EXPORT_LIMIT_EXCEEDED", "Too many rows.", utcNow.AddMinutes(3), maxAttempts: 2);

        Assert.Equal(ReportExportJobStatus.Failed, job.Status);
        Assert.Equal(2, job.Attempts);
        Assert.Equal("REPORT_EXPORT_LIMIT_EXCEEDED", job.LastErrorCode);
    }

    [Fact]
    public void MarkSucceeded_ThenExpired_ShouldClearLeaseAndExposeArtifactMetadata()
    {
        var job = CreateJob();
        var utcNow = new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc);

        job.MarkRunning("worker-1", utcNow, utcNow.AddMinutes(15));
        job.MarkSucceeded(
            rowCount: 12,
            blobName: "tenant/job.xlsx",
            fileName: "report.xlsx",
            contentType: ReportExportFormats.GetContentType(ReportExportFormats.Xlsx),
            sizeBytes: 1024,
            completedUtc: utcNow.AddMinutes(1),
            expiresUtc: utcNow.AddHours(24));

        Assert.Equal(ReportExportJobStatus.Succeeded, job.Status);
        Assert.Null(job.LeaseUntilUtc);
        Assert.Null(job.WorkerId);
        Assert.Equal(12, job.RowCount);
        Assert.Equal("tenant/job.xlsx", job.ArtifactBlobName);

        job.MarkExpired(utcNow.AddHours(25));

        Assert.Equal(ReportExportJobStatus.Expired, job.Status);
    }

    [Fact]
    public void Cancel_WhenQueued_ShouldMarkCancelled()
    {
        var job = CreateJob();
        var cancelledUtc = new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc);

        job.Cancel(cancelledUtc);

        Assert.Equal(ReportExportJobStatus.Cancelled, job.Status);
        Assert.Equal(cancelledUtc, job.CompletedUtc);
    }

    private static ReportExportJob CreateJob() =>
        ReportExportJob.Create(
            Guid.NewGuid(),
            "PERSONNEL_FILES",
            "csv",
            "{}",
            "user-1",
            new DateTime(2026, 4, 19, 9, 0, 0, DateTimeKind.Utc));
}
