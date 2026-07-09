namespace CLARIHR.Application.Features.Reports.Common;

public static class ReportExportResources
{
    public const string PersonnelFiles = "PERSONNEL_FILES";
    public const string PersonnelFilePersonnelActions = "PERSONNEL_FILE_PERSONNEL_ACTIONS";
    // REQ-004 PR-5: the COMPANY-WIDE personnel-actions bandeja export (tenant scope, gated by ViewReports),
    // distinct from the per-expediente PersonnelFilePersonnelActions resource.
    public const string CompanyPersonnelActions = "COMPANY_PERSONNEL_ACTIONS";
    public const string PersonnelFilePayrollTransactions = "PERSONNEL_FILE_PAYROLL_TRANSACTIONS";
    public const string OrgUnits = "ORG_UNITS";
    public const string PositionSlots = "POSITION_SLOTS";
    public const string SalaryTabulator = "SALARY_TABULATOR";
    public const string CostCenters = "COST_CENTERS";
    public const string LegalRepresentatives = "LEGAL_REPRESENTATIVES";
    public const string JobProfileCompetencyMatrix = "JOB_PROFILE_COMPETENCY_MATRIX";
    public const string JobProfilePdf = "JOB_PROFILE_PDF";

    // REX-A: the full set of normalized resource keys a job can carry, used by the Search handler to
    // resolve which resources the current user may read before listing their export-job metadata.
    public static readonly IReadOnlyList<string> All =
    [
        PersonnelFiles,
        PersonnelFilePersonnelActions,
        CompanyPersonnelActions,
        PersonnelFilePayrollTransactions,
        OrgUnits,
        PositionSlots,
        SalaryTabulator,
        CostCenters,
        LegalRepresentatives,
        JobProfileCompetencyMatrix,
        JobProfilePdf,
    ];

    public static bool IsSupported(string resourceKey) =>
        Normalize(resourceKey) is
            PersonnelFiles or
            PersonnelFilePersonnelActions or
            CompanyPersonnelActions or
            PersonnelFilePayrollTransactions or
            OrgUnits or
            PositionSlots or
            SalaryTabulator or
            CostCenters or
            LegalRepresentatives or
            JobProfileCompetencyMatrix or
            JobProfilePdf;

    public static string Normalize(string resourceKey) =>
        resourceKey.Trim().ToUpperInvariant();
}
