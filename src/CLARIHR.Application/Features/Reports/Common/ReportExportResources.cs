namespace CLARIHR.Application.Features.Reports.Common;

public static class ReportExportResources
{
    public const string PersonnelFiles = "PERSONNEL_FILES";
    public const string PersonnelFilePersonnelActions = "PERSONNEL_FILE_PERSONNEL_ACTIONS";
    public const string PersonnelFilePayrollTransactions = "PERSONNEL_FILE_PAYROLL_TRANSACTIONS";
    public const string OrgUnits = "ORG_UNITS";
    public const string PositionSlots = "POSITION_SLOTS";
    public const string SalaryTabulator = "SALARY_TABULATOR";
    public const string CostCenters = "COST_CENTERS";
    public const string LegalRepresentatives = "LEGAL_REPRESENTATIVES";
    public const string JobProfileCompetencyMatrix = "JOB_PROFILE_COMPETENCY_MATRIX";
    public const string JobProfilePdf = "JOB_PROFILE_PDF";

    public static bool IsSupported(string resourceKey) =>
        Normalize(resourceKey) is
            PersonnelFiles or
            PersonnelFilePersonnelActions or
            PersonnelFilePayrollTransactions or
            OrgUnits or
            PositionSlots or
            SalaryTabulator or
            CostCenters or
            LegalRepresentatives or
            JobProfileCompetencyMatrix or
            JobProfilePdf;

    public static bool IsDocumentResource(string normalizedResourceKey) =>
        normalizedResourceKey is JobProfilePdf;

    public static string Normalize(string resourceKey) =>
        resourceKey.Trim().ToUpperInvariant();
}
