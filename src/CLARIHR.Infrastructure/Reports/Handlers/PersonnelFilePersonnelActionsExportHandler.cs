using System.Text.Json;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports.Handlers;

internal sealed class PersonnelFilePersonnelActionsExportHandler(
    IPersonnelFileEmployeeRepository personnelFileEmployeeRepository,
    ReportExportRowWriter rowWriter) : IReportExportHandler
{
    public string ResourceKey => ReportExportResources.PersonnelFilePersonnelActions;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var rows = await personnelFileEmployeeRepository.ExportPersonnelActionsAsync(
            ReportExportParameters.RequireGuid(parameters, "personnelFilePublicId"),
            ReportExportParameters.ReadDateTime(parameters, "fromUtc"),
            ReportExportParameters.ReadDateTime(parameters, "toUtc"),
            ReportExportParameters.ReadString(parameters, "type"),
            ReportExportParameters.ReadString(parameters, "status"),
            ReportExportParameters.ReadString(parameters, "search", "q"),
            ReportExportParameters.ReadString(parameters, "sortBy"),
            ReportExportParameters.ReadEnum(parameters, PersonnelFileSortDirection.Desc, "sortDirection"),
            rowWriter.MaxRowsToRead,
            cancellationToken);

        return await rowWriter.WriteAsync(
            job, destination, rows, "personnel-actions", "PersonnelActions", cancellationToken);
    }
}
