using System.Text.Json;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.SalaryTabulator;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Application.Features.SalaryTabulator;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports.Handlers;

internal sealed class SalaryTabulatorExportHandler(
    ISalaryTabulatorRepository salaryTabulatorRepository,
    IPositionCatalogLookup positionDescriptionCatalogRepository,
    ReportExportRowWriter rowWriter) : IReportExportHandler
{
    public string ResourceKey => ReportExportResources.SalaryTabulator;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        string? salaryClassCode = null;
        var salaryClassId = ReportExportParameters.ReadGuid(parameters, "salaryClassPublicId");
        if (salaryClassId.HasValue)
        {
            salaryClassCode = await positionDescriptionCatalogRepository.ResolveSalaryClassCodeByCatalogIdAsync(
                job.TenantId,
                salaryClassId.Value,
                cancellationToken);
        }

        IReadOnlyCollection<SalaryTabulatorLineExportRow> rows;
        if (salaryClassId.HasValue && salaryClassCode is null)
        {
            rows = Array.Empty<SalaryTabulatorLineExportRow>();
        }
        else
        {
            rows = await salaryTabulatorRepository.GetLineExportRowsAsync(
                job.TenantId,
                salaryClassCode,
                ReportExportParameters.ReadString(parameters, "salaryScale", "salaryScaleCode"),
                ReportExportParameters.ReadBool(parameters, "isActive"),
                ReportExportParameters.ReadString(parameters, "search", "q"),
                rowWriter.MaxRowsToRead,
                cancellationToken);
        }

        return await rowWriter.WriteAsync(
            job, destination, rows, "salary-tabulator", "SalaryTabulator", cancellationToken);
    }
}
