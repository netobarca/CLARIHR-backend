using System.Text.Json;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports.Handlers;

internal sealed class PositionSlotsExportHandler(
    IPositionSlotRepository positionSlotRepository,
    ReportExportRowWriter rowWriter) : IReportExportHandler
{
    public string ResourceKey => ReportExportResources.PositionSlots;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var rows = await positionSlotRepository.GetExportRowsAsync(
            job.TenantId,
            ReportExportParameters.ReadEnum<PositionSlotStatus>(parameters, null, "status"),
            ReportExportParameters.ReadGuid(parameters, "jobProfilePublicId"),
            ReportExportParameters.ReadGuid(parameters, "orgUnitPublicId"),
            ReportExportParameters.ReadGuid(parameters, "workCenterPublicId"),
            ReportExportParameters.ReadGuid(parameters, "contractTypePublicId"),
            ReportExportParameters.ReadString(parameters, "search", "q"),
            rowWriter.MaxRowsToRead,
            cancellationToken);

        return await rowWriter.WriteAsync(
            job, destination, rows, "position-slots", "PositionSlots", cancellationToken);
    }
}
