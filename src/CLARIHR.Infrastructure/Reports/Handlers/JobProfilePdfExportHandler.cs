using System.Text.Json;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Reports.Handlers;

internal sealed class JobProfilePdfExportHandler(
    IJobProfileRepository jobProfileRepository,
    IDocumentPdfRenderer<JobProfilePrintResponse> jobProfilePdfRenderer,
    IOptions<ReportPerformanceOptions> performanceOptions) : IReportExportHandler
{
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

        await jobProfilePdfRenderer.RenderAsync(payload, destination, cancellationToken);

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

        var fileName = $"job-profile-{job.PublicId:N}.pdf";
        return new ReportExportGeneratedFile(
            RowCount: 1,
            FileName: fileName,
            ContentType: ReportExportFormats.GetContentType(ReportExportFormats.Pdf));
    }
}
