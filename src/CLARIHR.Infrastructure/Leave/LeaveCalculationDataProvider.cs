using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Compensation;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Leave;

/// <summary>
/// One-stop resolver of the incapacity engine's inputs (pattern: <see cref="CLARIHR.Infrastructure.PersonnelFiles.SettlementRepository"/> —
/// the engine stays pure, every read has a single auditable source). The base-salary criterion REPLICATES
/// the settlement one: referred plaza or the principal (IsPrimary among active assignments; the oldest
/// StartDate when none), country base-salary concept types (IsBaseSalary flag, literal SALARIO_BASE as
/// fallback), plaza-scoped instances plus the employee-level ones when the plaza is the principal.
/// </summary>
internal sealed class LeaveCalculationDataProvider(ApplicationDbContext dbContext) : ILeaveCalculationDataProvider
{
    private const string LegacyBaseSalaryConceptTypeCode = "SALARIO_BASE";

    /// <summary>Legal defaults of the employer cap (D-27): 9 covered days + 0 benefit days per year.</summary>
    private const int DefaultEmployerCoveredDaysPerYear = 9;
    private const int DefaultAdditionalBenefitDaysPerYear = 0;

    /// <summary>Holiday window for an open-ended incapacity: one year plus a leap-day of slack.</summary>
    private const int OpenEndedHolidayWindowDays = 366;

    /// <summary>Hard stop for the extension-chain walk (defensive against a corrupted self-reference).</summary>
    private const int MaxChainLinks = 200;

    public async Task<LeaveCalculationContext?> GetCalculationContextAsync(
        Guid tenantId,
        long personnelFileId,
        Guid? assignedPositionPublicId,
        long riskId,
        DateOnly startDate,
        DateOnly? endDate,
        long? excludeIncapacityId,
        long? extendsIncapacityId,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .Where(item => item.PublicId == tenantId)
            .Select(item => new { item.CountryCatalogItemId })
            .SingleOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            return null;
        }

        var risk = await dbContext.IncapacityRisks
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Id == riskId)
            .Select(item => new
            {
                item.Id,
                item.PublicId,
                item.Code,
                item.Name,
                item.CountsSeventhDay,
                item.CountsSaturday,
                item.CountsHoliday,
                item.UsesWorkSchedule,
                item.AllowsIndefinite,
                item.AllowsExtension,
                item.UsesFund,
                item.HasSubsidy,
                item.IsActive,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (risk is null)
        {
            return null;
        }

        var tranches = await dbContext.IncapacityRiskParameters
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IncapacityRiskId == riskId)
            .OrderBy(item => item.SortOrder)
            .Select(item => new LeaveRiskTrancheDto(item.DayFrom, item.DayTo, item.SubsidyPercent, item.PayerCode))
            .ToListAsync(cancellationToken);

        var assignments = await dbContext.PersonnelFileEmploymentAssignments
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId)
            .Select(item => new
            {
                item.PublicId,
                item.StartDate,
                item.IsActive,
                item.IsPrimary,
                item.RestDayOfWeek,
            })
            .ToListAsync(cancellationToken);

        var referred = assignedPositionPublicId is { } referredPublicId
            ? assignments.FirstOrDefault(item => item.PublicId == referredPublicId)
            : null;
        var primary = assignments
            .Where(item => item.IsActive && item.IsPrimary)
            .OrderBy(item => item.StartDate)
            .FirstOrDefault();

        // Salary plaza (settlement criterion): referred → primary among actives → oldest active → oldest.
        var salaryPlaza = referred
            ?? primary
            ?? assignments.Where(item => item.IsActive).OrderBy(item => item.StartDate).FirstOrDefault()
            ?? assignments.OrderBy(item => item.StartDate).FirstOrDefault();

        decimal? monthlyBaseSalary = null;
        if (salaryPlaza is not null)
        {
            // Base-salary type codes of the country (IsBaseSalary flag; the literal SALARIO_BASE stays as fallback).
            var baseSalaryCodes = await dbContext.Set<CompensationConceptTypeCatalogItem>()
                .AsNoTracking()
                .Where(item => item.CountryCatalogItemId == company.CountryCatalogItemId && item.IsBaseSalary)
                .Select(item => item.NormalizedCode)
                .ToListAsync(cancellationToken);
            if (baseSalaryCodes.Count == 0)
            {
                baseSalaryCodes.Add(LegacyBaseSalaryConceptTypeCode);
            }

            var salaryPlazaPublicId = salaryPlaza.PublicId;
            var salaryPlazaIsPrimary = salaryPlaza.IsPrimary;
            var instances = await dbContext.Set<PersonnelFileCompensationConcept>()
                .AsNoTracking()
                .Where(item => item.PersonnelFileId == personnelFileId && item.IsActive)
                .Where(item => item.AssignedPositionPublicId == salaryPlazaPublicId
                    || (salaryPlazaIsPrimary && item.AssignedPositionPublicId == null))
                .Select(item => new { item.ConceptTypeCode, item.Nature, item.CalculationType, item.Value })
                .ToListAsync(cancellationToken);

            monthlyBaseSalary = instances
                .Where(item => item.Nature == CompensationNature.Ingreso
                    && item.CalculationType == CompensationCalculationType.Fixed
                    && baseSalaryCodes.Contains(item.ConceptTypeCode.ToUpperInvariant()))
                .Select(item => (decimal?)item.Value)
                .FirstOrDefault();
        }

        var preference = await dbContext.CompanyPreferences
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => new
            {
                item.CompanyRestDayOfWeek,
                item.EmployerCoveredIncapacityDaysPerYear,
                item.AdditionalIncapacityBenefitDaysPerYear,
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Rest day (D-26): referred plaza → principal plaza → company preference → Sunday.
        var restDay = referred?.RestDayOfWeek
            ?? primary?.RestDayOfWeek
            ?? (preference?.CompanyRestDayOfWeek is { } configuredRestDay
                ? (DayOfWeek)configuredRestDay
                : DayOfWeek.Sunday);

        // Active holidays inside the calculation window ([start, start + 366d] while open-ended).
        var holidayRangeEnd = endDate ?? startDate.AddDays(OpenEndedHolidayWindowDays);
        var holidays = (await dbContext.CompanyHolidays
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .Where(item => item.Date >= startDate && item.Date <= holidayRangeEnd)
            .Select(item => item.Date)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        // Remaining employer cap of the start year (D-27): only REGISTRADA incapacities consume it (R-T6);
        // an EN_REVISION or ANULADA record never joins the aggregate. Floor at 0.
        var capDays = (preference?.EmployerCoveredIncapacityDaysPerYear ?? DefaultEmployerCoveredDaysPerYear)
            + (preference?.AdditionalIncapacityBenefitDaysPerYear ?? DefaultAdditionalBenefitDaysPerYear);
        var startYear = startDate.Year;
        var employerDaysConsumed = await dbContext.PersonnelFileIncapacities
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId
                && item.StatusCode == IncapacityStatuses.Registrada
                && item.StartDate.Year == startYear
                && (!excludeIncapacityId.HasValue || item.Id != excludeIncapacityId.Value))
            .SumAsync(item => (int?)item.EmployerDays, cancellationToken) ?? 0;
        var employerCapRemaining = Math.Max(0, capDays - employerDaysConsumed);

        // Chain offset (RN-03): Σ ComputableDays walking ExtendsIncapacityId backwards, skipping annulled links.
        var chainOffsetDays = 0;
        var nextLinkId = extendsIncapacityId;
        var visited = new HashSet<long>();
        while (nextLinkId is { } linkId && visited.Add(linkId) && visited.Count <= MaxChainLinks)
        {
            var link = await dbContext.PersonnelFileIncapacities
                .AsNoTracking()
                .Where(item => item.PersonnelFileId == personnelFileId && item.Id == linkId)
                .Select(item => new { item.StatusCode, item.ComputableDays, item.ExtendsIncapacityId })
                .SingleOrDefaultAsync(cancellationToken);
            if (link is null)
            {
                break;
            }

            if (link.StatusCode != IncapacityStatuses.Anulada)
            {
                chainOffsetDays += link.ComputableDays;
            }

            nextLinkId = link.ExtendsIncapacityId;
        }

        return new LeaveCalculationContext(
            monthlyBaseSalary,
            restDay,
            holidays,
            employerCapRemaining,
            chainOffsetDays,
            new LeaveRiskSnapshotDto(
                risk.Id,
                risk.PublicId,
                risk.Code,
                risk.Name,
                risk.CountsSeventhDay,
                risk.CountsSaturday,
                risk.CountsHoliday,
                risk.UsesWorkSchedule,
                risk.AllowsIndefinite,
                risk.AllowsExtension,
                risk.UsesFund,
                risk.HasSubsidy,
                risk.IsActive,
                tranches));
    }
}
