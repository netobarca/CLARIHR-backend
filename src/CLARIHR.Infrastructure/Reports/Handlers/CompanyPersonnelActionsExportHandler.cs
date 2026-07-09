using System.Text.Json;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Reporting;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Reports;

namespace CLARIHR.Infrastructure.Reports.Handlers;

/// <summary>
/// Asynchronous export handler for the company-wide personnel-actions bandeja (RF-016). Mirror of
/// <see cref="PersonnelFilePersonnelActionsExportHandler"/> but at the TENANT scope: it rebuilds the bandeja
/// filter from the job parameters and streams the same <see cref="AsientoPersonalExportRow"/> the synchronous
/// endpoint produces (Spanish headers, SIN MONTOS — aclaración №8). The job's tenant is the company; the worker
/// pushes it onto the ambient tenant context. Authorization was enforced by <c>ReportExportResourceAuthorizer</c>
/// (COMPANY_PERSONNEL_ACTIONS → <c>ViewReports</c>) at create/download time.
/// </summary>
internal sealed class CompanyPersonnelActionsExportHandler(
    IPersonnelFileDashboardRepository dashboardRepository,
    ReportExportRowWriter rowWriter) : IReportExportHandler
{
    public string ResourceKey => ReportExportResources.CompanyPersonnelActions;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var filter = new PersonnelActionBandejaFilter(
            ReportExportParameters.ReadString(parameters, "actionTypeCode", "type"),
            ReportExportParameters.ReadString(parameters, "actionStatusCode", "status"),
            ReportExportParameters.ReadBool(parameters, "isSystemGenerated"),
            ReportExportParameters.ReadInt(parameters, "year"),
            ReportExportParameters.ReadInt(parameters, "month"),
            ReportExportParameters.ReadDateTime(parameters, "fromUtc"),
            ReportExportParameters.ReadDateTime(parameters, "toUtc"),
            ReportExportParameters.ReadGuid(parameters, "employeePublicId", "employeeId"),
            ReportExportParameters.ReadGuid(parameters, "functionalAreaPublicId", "functionalAreaId"),
            ReportExportParameters.ReadGuid(parameters, "orgUnitPublicId", "orgUnitId"),
            ReportExportParameters.ReadGuid(parameters, "positionCategoryPublicId", "positionCategoryId"),
            ReportExportParameters.ReadGuid(parameters, "jobProfilePublicId", "jobProfileId"),
            ReportExportParameters.ReadGuid(parameters, "workCenterPublicId", "workCenterId"),
            ReportExportParameters.ReadString(parameters, "payrollTypeCode"),
            ReportExportParameters.ReadGuid(parameters, "costCenterPublicId", "costCenterId"));

        var rows = await dashboardRepository.GetPersonnelActionExportRowsAsync(
            job.TenantId, filter, rowWriter.MaxRowsToRead, cancellationToken);

        return await rowWriter.WriteAsync(
            job, destination, rows, "personnel-actions", "PersonnelActions", cancellationToken);
    }
}
