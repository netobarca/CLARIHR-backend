using System.Text.Json;
using CLARIHR.Application.Abstractions.OrgUnits;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports.Handlers;

internal sealed class OrgUnitsExportHandler(
    IOrgUnitRepository orgUnitRepository,
    ReportExportRowWriter rowWriter) : IReportExportHandler
{
    public string ResourceKey => ReportExportResources.OrgUnits;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var rows = await orgUnitRepository.GetExportRowsAsync(
            job.TenantId,
            ReportExportParameters.ReadBool(parameters, "isActive"),
            ReportExportParameters.ReadString(parameters, "search", "q"),
            ReportExportParameters.ReadGuid(parameters, "orgUnitTypePublicId"),
            ReportExportParameters.ReadGuid(parameters, "functionalAreaPublicId"),
            ReportExportParameters.ReadGuid(parameters, "parentPublicId"),
            rowWriter.MaxRowsToRead,
            cancellationToken);

        return await rowWriter.WriteAsync(
            job, destination, rows, "org-units", "OrgUnits", cancellationToken);
    }
}
