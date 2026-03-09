using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Application.Features.SalaryTabulator.Common;

namespace CLARIHR.Infrastructure.Reports;

internal sealed class ReportCapabilityRegistry : IReportCapabilityRegistry
{
    private static readonly IReadOnlyDictionary<string, ReportCapabilityDefinition> Definitions =
        new Dictionary<string, ReportCapabilityDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [OrgUnitPermissionCodes.ResourceKey] = new(
                new ReportCapabilitiesResponse(
                    OrgUnitPermissionCodes.ResourceKey,
                    SupportsPrint: false,
                    SupportsExport: true,
                    SupportedTableFormats: ["csv", "xlsx"],
                    SupportedGraphFormats: ["graphml", "json", "dot"]),
                OrgUnitPermissionCodes.Read,
                OrgUnitPermissionCodes.Admin,
                PrintPermissionCode: null,
                ExportPermissionCode: OrgUnitPermissionCodes.Read),

            [PositionDescriptionCatalogPermissionCodes.ResourceKey] = new(
                new ReportCapabilitiesResponse(
                    PositionDescriptionCatalogPermissionCodes.ResourceKey,
                    SupportsPrint: false,
                    SupportsExport: true,
                    SupportedTableFormats: ["csv", "xlsx"],
                    SupportedGraphFormats: []),
                PositionDescriptionCatalogPermissionCodes.Read,
                PositionDescriptionCatalogPermissionCodes.Admin,
                PrintPermissionCode: null,
                ExportPermissionCode: PositionDescriptionCatalogPermissionCodes.Read),

            [JobProfilePermissionCodes.ResourceKey] = new(
                new ReportCapabilitiesResponse(
                    JobProfilePermissionCodes.ResourceKey,
                    SupportsPrint: true,
                    SupportsExport: true,
                    SupportedTableFormats: ["json", "csv"],
                    SupportedGraphFormats: []),
                JobProfilePermissionCodes.Read,
                JobProfilePermissionCodes.Admin,
                PrintPermissionCode: JobProfilePermissionCodes.Read,
                ExportPermissionCode: JobProfilePermissionCodes.Read),

            [PositionSlotPermissionCodes.ResourceKey] = new(
                new ReportCapabilitiesResponse(
                    PositionSlotPermissionCodes.ResourceKey,
                    SupportsPrint: false,
                    SupportsExport: true,
                    SupportedTableFormats: ["csv", "xlsx"],
                    SupportedGraphFormats: ["graphml", "json", "dot"]),
                PositionSlotPermissionCodes.Read,
                PositionSlotPermissionCodes.Admin,
                PrintPermissionCode: null,
                ExportPermissionCode: PositionSlotPermissionCodes.Read),

            [SalaryTabulatorPermissionCodes.ResourceKey] = new(
                new ReportCapabilitiesResponse(
                    SalaryTabulatorPermissionCodes.ResourceKey,
                    SupportsPrint: false,
                    SupportsExport: true,
                    SupportedTableFormats: ["csv", "xlsx"],
                    SupportedGraphFormats: []),
                SalaryTabulatorPermissionCodes.Read,
                SalaryTabulatorPermissionCodes.Admin,
                PrintPermissionCode: null,
                ExportPermissionCode: SalaryTabulatorPermissionCodes.Read),

            [CostCenterPermissionCodes.ResourceKey] = new(
                new ReportCapabilitiesResponse(
                    CostCenterPermissionCodes.ResourceKey,
                    SupportsPrint: false,
                    SupportsExport: true,
                    SupportedTableFormats: ["csv", "xlsx"],
                    SupportedGraphFormats: []),
                CostCenterPermissionCodes.Read,
                CostCenterPermissionCodes.Admin,
                PrintPermissionCode: null,
                ExportPermissionCode: CostCenterPermissionCodes.Read),

            [LegalRepresentativePermissionCodes.ResourceKey] = new(
                new ReportCapabilitiesResponse(
                    LegalRepresentativePermissionCodes.ResourceKey,
                    SupportsPrint: false,
                    SupportsExport: true,
                    SupportedTableFormats: ["csv", "xlsx"],
                    SupportedGraphFormats: []),
                LegalRepresentativePermissionCodes.Read,
                LegalRepresentativePermissionCodes.Admin,
                PrintPermissionCode: null,
                ExportPermissionCode: LegalRepresentativePermissionCodes.Read),

            [PersonnelFilePermissionCodes.ResourceKey] = new(
                new ReportCapabilitiesResponse(
                    PersonnelFilePermissionCodes.ResourceKey,
                    SupportsPrint: true,
                    SupportsExport: true,
                    SupportedTableFormats: ["csv", "xlsx"],
                    SupportedGraphFormats: []),
                PersonnelFilePermissionCodes.Read,
                PersonnelFilePermissionCodes.Admin,
                PrintPermissionCode: PersonnelFilePermissionCodes.Read,
                ExportPermissionCode: PersonnelFilePermissionCodes.Read),

            [CompetencyFrameworkPermissionCodes.ResourceKey] = new(
                new ReportCapabilitiesResponse(
                    CompetencyFrameworkPermissionCodes.ResourceKey,
                    SupportsPrint: false,
                    SupportsExport: true,
                    SupportedTableFormats: ["json", "csv", "xlsx"],
                    SupportedGraphFormats: []),
                CompetencyFrameworkPermissionCodes.Read,
                CompetencyFrameworkPermissionCodes.Admin,
                PrintPermissionCode: null,
                ExportPermissionCode: CompetencyFrameworkPermissionCodes.Read)
        };

    public bool TryGet(string resourceKey, out ReportCapabilityDefinition definition) =>
        Definitions.TryGetValue(resourceKey.Trim(), out definition!);
}
