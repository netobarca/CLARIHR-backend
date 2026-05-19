using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Reports.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Reports.Handlers;

internal sealed class JobProfilePdfExportHandler(
    IJobProfileRepository jobProfileRepository,
    IDocumentPdfRenderer<JobProfilePrintResponse> jobProfilePdfRenderer,
    IOptions<ReportPerformanceOptions> performanceOptions,
    ILogger<JobProfilePdfExportHandler> logger) : IReportExportHandler
{
    private const string RendererName = nameof(JobProfilePdfRenderer);
    private readonly ReportPerformanceOptions _performanceOptions = performanceOptions.Value;

    public string ResourceKey => ReportExportResources.JobProfilePdf;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        // §N4: clients/UI send "jobProfileId"; accept it as an alias of the
        // canonical "jobProfilePublicId" (error message keeps names[0]).
        var jobProfileId = ReportExportParameters.RequireGuid(parameters, "jobProfilePublicId", "jobProfileId");

        var payload = await jobProfileRepository.GetPrintByIdAsync(jobProfileId, cancellationToken);
        if (payload is null)
        {
            throw new ReportExportInvalidParametersException(
                $"Job profile '{jobProfileId}' was not found for the current tenant.");
        }

        if (payload.Profile.CompanyId != job.TenantId)
        {
            throw new ReportExportInvalidParametersException(
                $"Job profile '{jobProfileId}' does not belong to the requesting tenant.");
        }

        // Confidentiality gate (doc 01 §N2): drop salary data unless the
        // requester was authorized for it at request time. Fail-closed.
        payload = JobProfileCompensationGate.Apply(payload, parameters);

        // Document Generation subdomain telemetry (doc 01 §6.1): emit dedicated
        // render_started / render_succeeded events with renderer + duration +
        // size dimensions so dashboards can compute renderer-specific p95s
        // independently of the generic Job* events (which lose this signal
        // because row_count is always 1 for PDF and artifact_size_bytes is
        // measured post-upload, not post-render).
        logger.LogInformation(
            ReportExportTelemetryEvents.PdfRenderStarted,
            "Job-profile PDF render started. job_id {job_id} tenant_id {tenant_id} job_profile_id {job_profile_id} resource_key {resource_key} renderer {renderer} outcome {outcome}",
            job.PublicId,
            job.TenantId,
            jobProfileId,
            job.ResourceKey,
            RendererName,
            "render_started");

        var renderStopwatch = Stopwatch.StartNew();
        await jobProfilePdfRenderer.RenderAsync(payload, destination, cancellationToken);
        renderStopwatch.Stop();

        var pdfSizeBytes = destination.CanSeek ? destination.Length : 0L;
        logger.LogInformation(
            ReportExportTelemetryEvents.PdfRenderSucceeded,
            "Job-profile PDF render succeeded. job_id {job_id} tenant_id {tenant_id} job_profile_id {job_profile_id} resource_key {resource_key} renderer {renderer} render_duration_ms {render_duration_ms} pdf_size_bytes {pdf_size_bytes} outcome {outcome}",
            job.PublicId,
            job.TenantId,
            jobProfileId,
            job.ResourceKey,
            RendererName,
            renderStopwatch.ElapsedMilliseconds,
            pdfSizeBytes,
            "render_succeeded");

        // Defense in depth (doc 01 §3.3): reject a pathological document with a
        // typed limit error *before* the downstream upload, instead of letting
        // the generic 100 MB storage cap reject it after the network round-trip.
        // The processor renders into a seekable temp FileStream, so Length is the
        // rendered size here; if the stream is not seekable we defer to the
        // storage cap (still enforced at upload).
        if (destination.CanSeek && destination.Length > _performanceOptions.NormalizedMaxDocumentBytes)
        {
            throw ReportExportLimitExceededException.ForDocumentSize(
                destination.Length,
                _performanceOptions.NormalizedMaxDocumentBytes);
        }

        var fileName = BuildFileName(payload.Profile, job.PublicId);
        return new ReportExportGeneratedFile(
            RowCount: 1,
            FileName: fileName,
            ContentType: ReportExportFormats.GetContentType(ReportExportFormats.Pdf));
    }

    // User-friendly filename (technical-debt doc 01 §7.1): the previous GUID-only
    // name (`job-profile-{guid}.pdf`) forced every client to override it from
    // metadata they had to fetch separately. Build a slug from the profile's
    // Code + Title so direct downloads land with a recognizable filename, and
    // fall back to the job's public id when both fields slugify to empty (e.g.
    // all-non-ASCII title with no code) so we never produce `job-profile-.pdf`.
    private static string BuildFileName(JobProfileResponse profile, Guid jobPublicId)
    {
        const int CodeSlugMaxLength = 40;
        const int TitleSlugMaxLength = 80;

        var codeSlug = Slugify(profile.Code, CodeSlugMaxLength);
        var titleSlug = Slugify(profile.Title, TitleSlugMaxLength);

        var slug = (codeSlug.Length, titleSlug.Length) switch
        {
            (0, 0) => jobPublicId.ToString("N"),
            (0, _) => titleSlug,
            (_, 0) => codeSlug,
            _ => $"{codeSlug}-{titleSlug}"
        };

        return $"job-profile-{slug}.pdf";
    }

    private static string Slugify(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var ascii = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                ascii.Append(character);
            }
        }

        var slug = new StringBuilder(ascii.Length);
        foreach (var character in ascii.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant())
        {
            if ((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9'))
            {
                slug.Append(character);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        while (slug.Length > 0 && slug[^1] == '-')
        {
            slug.Length--;
        }

        if (slug.Length > maxLength)
        {
            slug.Length = maxLength;
        }

        while (slug.Length > 0 && slug[^1] == '-')
        {
            slug.Length--;
        }

        return slug.ToString();
    }
}
