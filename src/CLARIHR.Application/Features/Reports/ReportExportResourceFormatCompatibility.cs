using CLARIHR.Application.Features.Reports.Common;

namespace CLARIHR.Application.Features.Reports;

/// <summary>
/// Declarative resource→formats compatibility table (technical-debt doc 01 §5.3).
/// Replaces the binary "document vs tabular" boolean so a new resource with a
/// different format set (DOCX-only, hybrid PDF+XLSX, JSON-only, …) is one
/// dictionary entry away — and the drift-proof guardrail test forces it to be
/// declared instead of silently defaulting.
/// </summary>
public static class ReportExportResourceFormatCompatibility
{
    private static readonly IReadOnlySet<string> TabularFormats = new HashSet<string>(StringComparer.Ordinal)
    {
        ReportExportFormats.Csv,
        ReportExportFormats.Xlsx,
        ReportExportFormats.Json,
    };

    private static readonly IReadOnlySet<string> DocumentPdfFormats = new HashSet<string>(StringComparer.Ordinal)
    {
        ReportExportFormats.Pdf,
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedFormatsByResource =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [ReportExportResources.PersonnelFiles] = TabularFormats,
            [ReportExportResources.PersonnelFilePersonnelActions] = TabularFormats,
            [ReportExportResources.CompanyPersonnelActions] = TabularFormats,
            [ReportExportResources.PersonnelFilePayrollTransactions] = TabularFormats,
            [ReportExportResources.OrgUnits] = TabularFormats,
            [ReportExportResources.PositionSlots] = TabularFormats,
            [ReportExportResources.SalaryTabulator] = TabularFormats,
            [ReportExportResources.CostCenters] = TabularFormats,
            [ReportExportResources.LegalRepresentatives] = TabularFormats,
            [ReportExportResources.JobProfileCompetencyMatrix] = TabularFormats,
            [ReportExportResources.JobProfilePdf] = DocumentPdfFormats,
        };

    public static bool IsCompatible(string normalizedResourceKey, string normalizedFormat) =>
        AllowedFormatsByResource.TryGetValue(normalizedResourceKey, out var formats)
        && formats.Contains(normalizedFormat);

    public static bool IsRegistered(string normalizedResourceKey) =>
        AllowedFormatsByResource.ContainsKey(normalizedResourceKey);
}
