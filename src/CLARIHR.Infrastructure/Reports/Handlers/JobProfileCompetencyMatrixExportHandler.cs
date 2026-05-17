using System.Text.Json;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports.Handlers;

internal sealed class JobProfileCompetencyMatrixExportHandler(
    ICompetencyFrameworkRepository competencyFrameworkRepository,
    ReportExportRowWriter rowWriter) : IReportExportHandler
{
    public string ResourceKey => ReportExportResources.JobProfileCompetencyMatrix;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var rows = await competencyFrameworkRepository.GetJobProfileCompetencyMatrixExportRowsAsync(
            ReportExportParameters.RequireGuid(parameters, "jobProfilePublicId"),
            rowWriter.MaxRowsToRead,
            cancellationToken);

        return await rowWriter.WriteAsync(
            job, destination, rows, "job-profile-competency-matrix", "CompetencyMatrix", cancellationToken);
    }
}
