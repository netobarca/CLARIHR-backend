using System.Text.Json;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports.Handlers;

internal sealed class LegalRepresentativesExportHandler(
    ILegalRepresentativeRepository legalRepresentativeRepository,
    ReportExportRowWriter rowWriter) : IReportExportHandler
{
    public string ResourceKey => ReportExportResources.LegalRepresentatives;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var rows = await legalRepresentativeRepository.GetExportRowsAsync(
            job.TenantId,
            ReportExportParameters.ReadBool(parameters, "isActive"),
            ReportExportParameters.ReadBool(parameters, "isPrimary"),
            ReportExportParameters.ReadEnum<LegalRepresentativeRepresentationType>(parameters, null, "representationType"),
            ReportExportParameters.ReadString(parameters, "search", "q"),
            rowWriter.MaxRowsToRead,
            cancellationToken);

        return await rowWriter.WriteAsync(
            job, destination, rows, "legal-representatives", "LegalRepresentatives", cancellationToken);
    }
}
