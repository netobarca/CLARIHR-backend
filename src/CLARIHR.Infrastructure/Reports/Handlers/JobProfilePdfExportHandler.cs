using System.Text.Json;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports.Handlers;

internal sealed class JobProfilePdfExportHandler(
    IJobProfileRepository jobProfileRepository,
    IDocumentPdfRenderer<JobProfilePrintResponse> jobProfilePdfRenderer) : IReportExportHandler
{
    public string ResourceKey => ReportExportResources.JobProfilePdf;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var jobProfileId = ReportExportParameters.RequireGuid(parameters, "jobProfilePublicId");

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

        await jobProfilePdfRenderer.RenderAsync(payload, destination, cancellationToken);

        var fileName = $"job-profile-{job.PublicId:N}.pdf";
        return new ReportExportGeneratedFile(
            RowCount: 1,
            FileName: fileName,
            ContentType: ReportExportFormats.GetContentType(ReportExportFormats.Pdf));
    }
}
