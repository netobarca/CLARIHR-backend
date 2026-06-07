using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.OrgUnits;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Application.Abstractions.SalaryTabulator;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.Reports.Common;

/// <summary>
/// Single source of truth for "who may read a report-export resource", shared by the create gate
/// and the read/download/cancel gates so they cannot drift (finding REX-1). The dispatch previously
/// lived only in <c>CreateReportExportJobCommandHandler</c>; the download path bypassed it, allowing
/// any tenant user to exfiltrate exports of resources they could not read.
/// </summary>
internal sealed class ReportExportResourceAuthorizer(
    IPersonnelFileAuthorizationService personnelFileAuthorizationService,
    IOrgUnitAuthorizationService orgUnitAuthorizationService,
    IPositionSlotAuthorizationService positionSlotAuthorizationService,
    ISalaryTabulatorAuthorizationService salaryTabulatorAuthorizationService,
    ICostCenterAuthorizationService costCenterAuthorizationService,
    ILegalRepresentativeAuthorizationService legalRepresentativeAuthorizationService,
    ICompetencyFrameworkAuthorizationService competencyFrameworkAuthorizationService,
    IJobProfileAuthorizationService jobProfileAuthorizationService)
    : IReportExportResourceAuthorizer
{
    public Task<Result> EnsureCanReadResourceAsync(
        string normalizedResourceKey,
        Guid companyId,
        CancellationToken cancellationToken) =>
        normalizedResourceKey switch
        {
            ReportExportResources.PersonnelFiles or
            ReportExportResources.PersonnelFilePersonnelActions or
            ReportExportResources.PersonnelFilePayrollTransactions =>
                personnelFileAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.OrgUnits => orgUnitAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.PositionSlots => positionSlotAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.SalaryTabulator => salaryTabulatorAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.CostCenters => costCenterAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.LegalRepresentatives => legalRepresentativeAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.JobProfileCompetencyMatrix => competencyFrameworkAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.JobProfilePdf => jobProfileAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            _ => Task.FromResult(Result.Failure(ReportPolicyErrors.ResourceNotSupported))
        };
}
