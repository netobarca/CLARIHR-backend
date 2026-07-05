using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Compensation;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

/// <summary>
/// Dedicated persistence of the settlement module (pattern: <see cref="ExitInterviewRepository"/>).
/// The calculation context is resolved in one place so the engine stays pure and the snapshot-at-create
/// semantics (pre-development clarification №2) have a single, auditable source.
/// </summary>
internal sealed class SettlementRepository(ApplicationDbContext dbContext) : ISettlementRepository
{
    private const string MonthlyPayPeriodCode = "MENSUAL";
    private const string IsssConceptTypeCode = "ISSS";
    private const string AfpConceptTypeCode = "AFP";
    private const string BonusConceptTypeCode = "BONO";
    private const string CommissionConceptTypeCode = "COMISION";
    private const string LegacyBaseSalaryConceptTypeCode = "SALARIO_BASE";

    public async Task<SettlementCalculationContext?> GetCalculationContextAsync(
        Guid tenantId,
        long personnelFileId,
        Guid assignedPositionPublicId,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .Where(item => item.PublicId == tenantId)
            .Select(item => new { item.CountryCatalogItemId, item.CountryCode })
            .SingleOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            return null;
        }

        var assignment = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId && item.PublicId == assignedPositionPublicId)
            .Select(item => new
            {
                item.PublicId,
                item.StartDate,
                item.EndDate,
                item.IsActive,
                item.IsPrimary,
                item.PositionSlotPublicId,
                item.CostCenterPublicId,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (assignment is null)
        {
            return null;
        }

        string? positionTitle = null;
        if (assignment.PositionSlotPublicId is { } slotPublicId)
        {
            positionTitle = await dbContext.PositionSlots
                .AsNoTracking()
                .Where(item => item.PublicId == slotPublicId)
                .Select(item => item.Title ?? item.Code)
                .SingleOrDefaultAsync(cancellationToken);
        }

        string? costCenterName = null;
        if (assignment.CostCenterPublicId is { } costCenterPublicId)
        {
            costCenterName = await dbContext.CostCenters
                .AsNoTracking()
                .Where(item => item.PublicId == costCenterPublicId)
                .Select(item => item.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var profile = await dbContext.Set<PersonnelFileEmployeeProfile>()
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId)
            .Select(item => new { item.HireDate, item.MinimumMonthlyWage, item.RetirementDate })
            .SingleOrDefaultAsync(cancellationToken);

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

        // Compensation concepts of the plaza — plus the employee-level ones (null plaza) when the plaza is
        // the principal (P-03 ratified).
        var instances = await dbContext.Set<PersonnelFileCompensationConcept>()
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId && item.IsActive)
            .Where(item => item.AssignedPositionPublicId == assignedPositionPublicId
                || (assignment.IsPrimary && item.AssignedPositionPublicId == null))
            .Select(item => new
            {
                item.ConceptTypeCode,
                item.Nature,
                item.DeductionClass,
                item.CalculationType,
                item.Value,
                item.EmployerRate,
                item.ContributionCap,
                item.CounterpartyName,
                item.Notes,
            })
            .ToListAsync(cancellationToken);

        var monthlyBaseSalary = instances
            .Where(item => item.Nature == CompensationNature.Ingreso
                && item.CalculationType == CompensationCalculationType.Fixed
                && baseSalaryCodes.Contains(item.ConceptTypeCode.ToUpperInvariant()))
            .Select(item => (decimal?)item.Value)
            .FirstOrDefault();

        var suggested = new List<SettlementSuggestedItemDto>();
        foreach (var item in instances)
        {
            if (item.Nature == CompensationNature.Ingreso && item.CalculationType == CompensationCalculationType.Fixed)
            {
                var code = item.ConceptTypeCode.ToUpperInvariant() switch
                {
                    BonusConceptTypeCode => SettlementConceptCodes.BonoPendiente,
                    CommissionConceptTypeCode => SettlementConceptCodes.ComisionPendiente,
                    _ => null,
                };
                if (code is not null)
                {
                    suggested.Add(new SettlementSuggestedItemDto(code, item.Notes ?? item.ConceptTypeCode, item.Value, null));
                }
            }
            else if (item.Nature == CompensationNature.Egreso
                && item.DeductionClass == DeductionClass.Externo
                && item.CalculationType == CompensationCalculationType.Fixed)
            {
                suggested.Add(new SettlementSuggestedItemDto(
                    SettlementConceptCodes.DescuentoExterno,
                    item.Notes ?? item.ConceptTypeCode,
                    item.Value,
                    item.CounterpartyName));
            }
        }

        var isssInstance = instances.FirstOrDefault(item => item.ConceptTypeCode.ToUpperInvariant() == IsssConceptTypeCode);
        var afpInstance = instances.FirstOrDefault(item => item.ConceptTypeCode.ToUpperInvariant() == AfpConceptTypeCode);
        var isss = await ResolveSchemeAsync(
            company.CountryCatalogItemId,
            isssInstance is null ? null : (isssInstance.Value, isssInstance.EmployerRate, isssInstance.ContributionCap),
            IsssConceptTypeCode,
            cancellationToken);
        var afp = await ResolveSchemeAsync(
            company.CountryCatalogItemId,
            afpInstance is null ? null : (afpInstance.Value, afpInstance.EmployerRate, afpInstance.ContributionCap),
            AfpConceptTypeCode,
            cancellationToken);

        var brackets = await dbContext.Set<IncomeTaxWithholdingBracket>()
            .AsNoTracking()
            .Where(item => item.PayPeriodCode == MonthlyPayPeriodCode && item.IsActive)
            .Where(item => item.EffectiveFromUtc <= asOfUtc && (item.EffectiveToUtc == null || item.EffectiveToUtc >= asOfUtc))
            .OrderBy(item => item.BracketOrder)
            .Select(item => new SettlementTaxBracketDto(item.LowerBound, item.UpperBound, item.FixedFee, item.RatePercent, item.ExcessOver))
            .ToListAsync(cancellationToken);

        var concepts = await dbContext.SettlementConceptCatalogItems
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == company.CountryCatalogItemId && item.IsActive)
            .OrderBy(item => item.SortOrder)
            .Select(item => new SettlementConceptResponse(
                item.PublicId, item.Code, item.Name, item.ConceptClass, item.AffectsIsss, item.AffectsAfp,
                item.AffectsRenta, item.ExemptionRule, item.ExemptionMultiplier, item.IsSystemCalculated,
                item.DefaultRatePercent, item.IsActive, item.SortOrder))
            .ToListAsync(cancellationToken);

        var currency = await dbContext.Set<CLARIHR.Domain.Preferences.CompanyPreference>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.CurrencyCode)
            .SingleOrDefaultAsync(cancellationToken) ?? "USD";

        return new SettlementCalculationContext(
            new SettlementPlazaContext(
                assignment.PublicId, assignment.StartDate, assignment.EndDate, assignment.IsActive,
                assignment.IsPrimary, positionTitle, assignment.CostCenterPublicId, costCenterName),
            monthlyBaseSalary,
            profile?.HireDate,
            profile?.MinimumMonthlyWage,
            profile?.RetirementDate,
            suggested,
            isss,
            afp,
            brackets,
            concepts,
            currency);
    }

    public async Task<RetirementSeparationType?> GetSeparationTypeAsync(
        Guid tenantId,
        string retirementCategoryCode,
        CancellationToken cancellationToken)
    {
        var normalizedCode = retirementCategoryCode.Trim().ToUpperInvariant();
        var countryCatalogItemId = await dbContext.Companies
            .AsNoTracking()
            .Where(item => item.PublicId == tenantId)
            .Select(item => (long?)item.CountryCatalogItemId)
            .SingleOrDefaultAsync(cancellationToken);
        if (countryCatalogItemId is null)
        {
            return null;
        }

        return await dbContext.Set<RetirementCategoryCatalogItem>()
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == countryCatalogItemId && item.NormalizedCode == normalizedCode)
            .Select(item => (RetirementSeparationType?)item.SeparationType)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<SettlementRequesterLookup?> GetRequesterLookupAsync(
        Guid tenantId,
        Guid personnelFilePublicId,
        CancellationToken cancellationToken) =>
        (from file in dbContext.PersonnelFiles.AsNoTracking()
         where file.TenantId == tenantId && file.PublicId == personnelFilePublicId
         join preference in dbContext.Set<CLARIHR.Domain.Preferences.CompanyPreference>().AsNoTracking()
             on file.TenantId equals preference.TenantId into preferences
         from preference in preferences.DefaultIfEmpty()
         select new SettlementRequesterLookup(
             file.PublicId,
             file.FullName,
             file.IsActive,
             preference != null ? preference.HrFunctionalAreaCode : null,
             (from assignment in dbContext.Set<PersonnelFileEmploymentAssignment>()
              where assignment.PersonnelFileId == file.Id && assignment.IsActive && assignment.IsPrimary
              join orgUnit in dbContext.OrgUnits on assignment.OrgUnitPublicId equals orgUnit.PublicId
              join area in dbContext.FunctionalAreaCatalogItems on orgUnit.FunctionalAreaCatalogItemId equals area.Id
              select area.Code).FirstOrDefault()))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<SettlementRetirementLookup?> GetLatestRetirementAsync(
        long personnelFileId,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.PersonnelFileRetirementRequests
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId && item.IsActive)
            .OrderByDescending(item => item.ExecutionDateUtc ?? DateTime.MinValue)
            .ThenByDescending(item => item.CreatedUtc)
            .Select(item => new
            {
                item.Id,
                item.PublicId,
                item.RequestStatusCode,
                item.RetirementDate,
                item.RetirementCategoryCode,
                item.RetirementCategoryNameSnapshot,
                item.RetirementReasonCode,
                item.RetirementReasonNameSnapshot,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (request is null)
        {
            return null;
        }

        var closedAssignments = await dbContext.RetirementRequestClosedRecords
            .AsNoTracking()
            .Where(item => item.RetirementRequestId == request.Id && item.EntityKind == RetirementClosedRecordKinds.Assignment)
            .Select(item => item.EntityPublicId)
            .ToListAsync(cancellationToken);

        return new SettlementRetirementLookup(
            request.Id,
            request.PublicId,
            request.RequestStatusCode,
            request.RetirementDate,
            request.RetirementCategoryCode,
            request.RetirementCategoryNameSnapshot,
            request.RetirementReasonCode,
            request.RetirementReasonNameSnapshot,
            closedAssignments);
    }

    public Task<bool> HasLiveSettlementAsync(
        long retirementRequestId,
        Guid assignedPositionPublicId,
        CancellationToken cancellationToken) =>
        dbContext.PersonnelFileSettlements
            .AsNoTracking()
            .AnyAsync(
                item => item.RetirementRequestId == retirementRequestId
                    && item.AssignedPositionPublicId == assignedPositionPublicId
                    && item.Kind == SettlementKind.Liquidacion
                    && item.StatusCode != SettlementStatuses.Anulada
                    && item.IsActive,
                cancellationToken);

    public Task AddAsync(PersonnelFileSettlement settlement, CancellationToken cancellationToken)
    {
        dbContext.PersonnelFileSettlements.Add(settlement);
        return Task.CompletedTask;
    }

    public Task<PersonnelFileSettlement?> GetTrackedAsync(
        long personnelFileId,
        Guid settlementPublicId,
        CancellationToken cancellationToken) =>
        dbContext.PersonnelFileSettlements
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(
                item => item.PersonnelFileId == personnelFileId && item.PublicId == settlementPublicId,
                cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileSettlement>> GetByFileAsync(
        long personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileSettlements
            .AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.PersonnelFileId == personnelFileId && item.IsActive)
            .OrderByDescending(item => item.RequestDate)
            .ThenByDescending(item => item.CreatedUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileSettlement>> GetLiveSettlementsForRetirementAsync(
        long retirementRequestId,
        CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileSettlements
            .Include(item => item.Lines)
            .Where(item => item.RetirementRequestId == retirementRequestId
                && item.Kind == SettlementKind.Liquidacion
                && item.StatusCode != SettlementStatuses.Anulada
                && item.IsActive)
            .ToArrayAsync(cancellationToken);

    /// <summary>Instance rates win; the country type-catalog defaults are the fallback (D-12).</summary>
    private async Task<SettlementSchemeDto> ResolveSchemeAsync(
        long countryCatalogItemId,
        (decimal EmployeeRate, decimal? EmployerRate, decimal? Cap)? instance,
        string conceptTypeCode,
        CancellationToken cancellationToken)
    {
        var defaults = await dbContext.Set<CompensationConceptTypeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == countryCatalogItemId && item.NormalizedCode == conceptTypeCode)
            .Select(item => new { item.DefaultEmployeeRate, item.DefaultEmployerRate, item.ContributionCap })
            .SingleOrDefaultAsync(cancellationToken);

        return new SettlementSchemeDto(
            instance?.EmployeeRate ?? defaults?.DefaultEmployeeRate ?? 0m,
            instance?.EmployerRate ?? defaults?.DefaultEmployerRate ?? 0m,
            instance?.Cap ?? defaults?.ContributionCap);
    }
}
