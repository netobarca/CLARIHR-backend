using System.Globalization;
using System.Text.Json;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.OrgUnits;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Abstractions.SalaryTabulator;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Application.Features.SalaryTabulator;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Reports;

internal sealed record ReportExportGeneratedFile(
    int RowCount,
    string FileName,
    string ContentType);

internal sealed class ReportExportLimitExceededException(int rowCount, int maxRows)
    : Exception($"Report export row count {rowCount} exceeds the maximum allowed row count {maxRows}.");

internal sealed class ReportExportInvalidParametersException(string message) : Exception(message);

internal sealed class ReportExportJobGenerator(
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository personnelFileEmployeeRepository,
    IOrgUnitRepository orgUnitRepository,
    IPositionSlotRepository positionSlotRepository,
    ISalaryTabulatorRepository salaryTabulatorRepository,
    ICostCenterRepository costCenterRepository,
    ILegalRepresentativeRepository legalRepresentativeRepository,
    ICompetencyFrameworkRepository competencyFrameworkRepository,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
    IJobProfileRepository jobProfileRepository,
    IDocumentPdfRenderer<JobProfilePrintResponse> jobProfilePdfRenderer,
    IOptions<ReportPerformanceOptions> options) : IReportExportJobGenerator
{
    private readonly ReportPerformanceOptions _options = options.Value;

    public async Task<ReportExportGeneratedFile> GenerateAsync(
        ReportExportJob job,
        Stream destination,
        CancellationToken cancellationToken)
    {
        using var document = ParseParameters(job.ParametersJson);
        var parameters = document.RootElement;
        var maxRowsToRead = _options.NormalizedMaxAsyncExportRows + 1;

        return job.ResourceKey switch
        {
            ReportExportResources.PersonnelFiles => await WriteRowsAsync(
                job,
                destination,
                await personnelFileRepository.GetExportRowsAsync(
                    job.TenantId,
                    ReadBool(parameters, "isActive"),
                    ReadEnum<PersonnelFileRecordType>(parameters, null, "recordType"),
                    ReadGuid(parameters, "orgUnitId"),
                    ReadInt(parameters, "minAge"),
                    ReadInt(parameters, "maxAge"),
                    ReadString(parameters, "maritalStatus"),
                    ReadString(parameters, "nationality"),
                    ReadString(parameters, "profession"),
                    ReadDateTime(parameters, "createdFromUtc"),
                    ReadDateTime(parameters, "createdToUtc"),
                    ReadString(parameters, "search", "q"),
                    ReadString(parameters, "sortBy"),
                    ReadEnum(parameters, PersonnelFileSortDirection.Asc, "sortDirection"),
                    maxRowsToRead,
                    cancellationToken),
                "personnel-files",
                "PersonnelFiles",
                cancellationToken),

            ReportExportResources.PersonnelFilePersonnelActions => await WriteRowsAsync(
                job,
                destination,
                await personnelFileEmployeeRepository.ExportPersonnelActionsAsync(
                    RequireGuid(parameters, "personnelFileId"),
                    ReadDateTime(parameters, "fromUtc"),
                    ReadDateTime(parameters, "toUtc"),
                    ReadString(parameters, "type"),
                    ReadString(parameters, "status"),
                    ReadString(parameters, "search", "q"),
                    ReadString(parameters, "sortBy"),
                    ReadEnum(parameters, PersonnelFileSortDirection.Desc, "sortDirection"),
                    maxRowsToRead,
                    cancellationToken),
                "personnel-actions",
                "PersonnelActions",
                cancellationToken),

            ReportExportResources.PersonnelFilePayrollTransactions => await WriteRowsAsync(
                job,
                destination,
                await personnelFileEmployeeRepository.ExportPayrollTransactionsAsync(
                    RequireGuid(parameters, "personnelFileId"),
                    ReadDateTime(parameters, "fromUtc"),
                    ReadDateTime(parameters, "toUtc"),
                    ReadString(parameters, "type"),
                    ReadString(parameters, "status"),
                    ReadString(parameters, "search", "q"),
                    ReadString(parameters, "sortBy"),
                    ReadEnum(parameters, PersonnelFileSortDirection.Desc, "sortDirection"),
                    maxRowsToRead,
                    cancellationToken),
                "payroll-transactions",
                "PayrollTransactions",
                cancellationToken),

            ReportExportResources.OrgUnits => await WriteRowsAsync(
                job,
                destination,
                await orgUnitRepository.GetExportRowsAsync(
                    job.TenantId,
                    ReadBool(parameters, "isActive"),
                    ReadString(parameters, "search", "q"),
                    ReadGuid(parameters, "orgUnitTypeId"),
                    ReadGuid(parameters, "functionalAreaId"),
                    ReadGuid(parameters, "parentId"),
                    maxRowsToRead,
                    cancellationToken),
                "org-units",
                "OrgUnits",
                cancellationToken),

            ReportExportResources.PositionSlots => await WriteRowsAsync(
                job,
                destination,
                await positionSlotRepository.GetExportRowsAsync(
                    job.TenantId,
                    ReadEnum<PositionSlotStatus>(parameters, null, "status"),
                    ReadGuid(parameters, "jobProfileId"),
                    ReadGuid(parameters, "orgUnitId"),
                    ReadGuid(parameters, "workCenterId"),
                    ReadGuid(parameters, "contractTypeId"),
                    ReadString(parameters, "search", "q"),
                    maxRowsToRead,
                    cancellationToken),
                "position-slots",
                "PositionSlots",
                cancellationToken),

            ReportExportResources.SalaryTabulator => await GenerateSalaryTabulatorAsync(job, destination, parameters, maxRowsToRead, cancellationToken),

            ReportExportResources.CostCenters => await WriteRowsAsync(
                job,
                destination,
                await costCenterRepository.GetExportRowsAsync(
                    job.TenantId,
                    ReadEnum<CostCenterType>(parameters, null, "type"),
                    ReadBool(parameters, "isActive"),
                    ReadString(parameters, "search", "q"),
                    maxRowsToRead,
                    cancellationToken),
                "cost-centers",
                "CostCenters",
                cancellationToken),

            ReportExportResources.LegalRepresentatives => await WriteRowsAsync(
                job,
                destination,
                await legalRepresentativeRepository.GetExportRowsAsync(
                    job.TenantId,
                    ReadBool(parameters, "isActive"),
                    ReadBool(parameters, "isPrimary"),
                    ReadEnum<LegalRepresentativeRepresentationType>(parameters, null, "representationType"),
                    ReadString(parameters, "search", "q"),
                    maxRowsToRead,
                    cancellationToken),
                "legal-representatives",
                "LegalRepresentatives",
                cancellationToken),

            ReportExportResources.JobProfileCompetencyMatrix => await WriteRowsAsync(
                job,
                destination,
                await competencyFrameworkRepository.GetJobProfileCompetencyMatrixExportRowsAsync(
                    RequireGuid(parameters, "jobProfileId"),
                    maxRowsToRead,
                    cancellationToken),
                "job-profile-competency-matrix",
                "CompetencyMatrix",
                cancellationToken),

            ReportExportResources.JobProfilePdf => await GenerateJobProfilePdfAsync(
                job,
                destination,
                parameters,
                cancellationToken),

            _ => throw new NotSupportedException($"Report resource '{job.ResourceKey}' is not supported.")
        };
    }

    private async Task<ReportExportGeneratedFile> GenerateJobProfilePdfAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var jobProfileId = RequireGuid(parameters, "jobProfileId");

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

    private async Task<ReportExportGeneratedFile> GenerateSalaryTabulatorAsync(
        ReportExportJob job,
        Stream destination,
        JsonElement parameters,
        int maxRowsToRead,
        CancellationToken cancellationToken)
    {
        string? salaryClassCode = null;
        var salaryClassId = ReadGuid(parameters, "salaryClassId");
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
                ReadString(parameters, "salaryScale", "salaryScaleCode"),
                ReadBool(parameters, "isActive"),
                ReadString(parameters, "search", "q"),
                maxRowsToRead,
                cancellationToken);
        }

        return await WriteRowsAsync(job, destination, rows, "salary-tabulator", "SalaryTabulator", cancellationToken);
    }

    private async Task<ReportExportGeneratedFile> WriteRowsAsync<TRow>(
        ReportExportJob job,
        Stream destination,
        IReadOnlyCollection<TRow> rows,
        string fileNamePrefix,
        string sheetName,
        CancellationToken cancellationToken)
    {
        var maxRows = _options.NormalizedMaxAsyncExportRows;
        if (rows.Count > maxRows)
        {
            throw new ReportExportLimitExceededException(rows.Count, maxRows);
        }

        var normalizedFormat = job.Format;
        var fileName = $"{fileNamePrefix}-{job.PublicId:N}.{normalizedFormat}";
        await ReportExportFileWriter.WriteAsync(destination, rows, normalizedFormat, sheetName, cancellationToken);

        return new ReportExportGeneratedFile(
            rows.Count,
            fileName,
            ReportExportFormats.GetContentType(normalizedFormat));
    }

    private static JsonDocument ParseParameters(string parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return JsonDocument.Parse("{}");
        }

        try
        {
            var document = JsonDocument.Parse(parametersJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document
                : JsonDocument.Parse("{}");
        }
        catch (JsonException)
        {
            throw new ReportExportInvalidParametersException("Report export parameters must be a valid JSON object.");
        }
    }

    private static string? ReadString(JsonElement parameters, params string[] names)
    {
        if (!TryGetProperty(parameters, out var value, names))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? Normalize(value.GetString())
            : value.ToString();
    }

    private static Guid? ReadGuid(JsonElement parameters, params string[] names)
    {
        var value = ReadString(parameters, names);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static Guid RequireGuid(JsonElement parameters, params string[] names) =>
        ReadGuid(parameters, names) ??
        throw new ReportExportInvalidParametersException($"Report export parameter '{names[0]}' is required.");

    private static bool? ReadBool(JsonElement parameters, params string[] names)
    {
        if (!TryGetProperty(parameters, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static int? ReadInt(JsonElement parameters, params string[] names)
    {
        if (!TryGetProperty(parameters, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var parsed) => parsed,
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static DateTime? ReadDateTime(JsonElement parameters, params string[] names)
    {
        var value = ReadString(parameters, names);
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    private static TEnum ReadEnum<TEnum>(JsonElement parameters, TEnum defaultValue, params string[] names)
        where TEnum : struct, Enum =>
        ReadEnum<TEnum>(parameters, null, names) ?? defaultValue;

    private static TEnum? ReadEnum<TEnum>(JsonElement parameters, TEnum? defaultValue, params string[] names)
        where TEnum : struct, Enum
    {
        var value = ReadString(parameters, names);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static bool TryGetProperty(JsonElement parameters, out JsonElement value, params string[] names)
    {
        foreach (var property in parameters.EnumerateObject())
        {
            if (names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
