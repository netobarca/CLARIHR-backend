using System.Text.Json;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports.Handlers;

internal sealed class CostCentersExportHandler(
    ICostCenterRepository costCenterRepository,
    ReportExportRowWriter rowWriter) : IReportExportHandler
{
    public string ResourceKey => ReportExportResources.CostCenters;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var rows = await costCenterRepository.GetExportRowsAsync(
            job.TenantId,
            ReportExportParameters.ReadEnum<CostCenterType>(parameters, null, "type"),
            ReportExportParameters.ReadBool(parameters, "isActive"),
            ReportExportParameters.ReadString(parameters, "search", "q"),
            rowWriter.MaxRowsToRead,
            cancellationToken);

        return await rowWriter.WriteAsync(
            job, destination, rows, "cost-centers", "CostCenters", cancellationToken);
    }
}
