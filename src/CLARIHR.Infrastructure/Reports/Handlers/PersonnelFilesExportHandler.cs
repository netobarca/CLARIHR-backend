using System.Text.Json;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports.Handlers;

internal sealed class PersonnelFilesExportHandler(
    IPersonnelFileRepository personnelFileRepository,
    ReportExportRowWriter rowWriter) : IReportExportHandler
{
    public string ResourceKey => ReportExportResources.PersonnelFiles;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var rows = await personnelFileRepository.GetExportRowsAsync(
            job.TenantId,
            ReportExportParameters.ReadBool(parameters, "isActive"),
            ReportExportParameters.ReadEnum<PersonnelFileRecordType>(parameters, null, "recordType"),
            ReportExportParameters.ReadGuid(parameters, "orgUnitPublicId"),
            ReportExportParameters.ReadInt(parameters, "minAge"),
            ReportExportParameters.ReadInt(parameters, "maxAge"),
            ReportExportParameters.ReadString(parameters, "maritalStatus"),
            ReportExportParameters.ReadString(parameters, "nationality"),
            ReportExportParameters.ReadString(parameters, "profession"),
            ReportExportParameters.ReadDateTime(parameters, "createdFromUtc"),
            ReportExportParameters.ReadDateTime(parameters, "createdToUtc"),
            ReportExportParameters.ReadString(parameters, "search", "q"),
            ReportExportParameters.ReadString(parameters, "sortBy"),
            ReportExportParameters.ReadEnum(parameters, PersonnelFileSortDirection.Asc, "sortDirection"),
            rowWriter.MaxRowsToRead,
            cancellationToken);

        return await rowWriter.WriteAsync(
            job, destination, rows, "personnel-files", "PersonnelFiles", cancellationToken);
    }
}
