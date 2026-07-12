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
internal sealed class SettlementRepository(
    ApplicationDbContext dbContext,
    ICompensatoryTimeRepository compensatoryTimeRepository) : ISettlementRepository
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

        var preference = await dbContext.Set<CLARIHR.Domain.Preferences.CompanyPreference>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => new
            {
                item.CurrencyCode,
                item.CompensatoryTimeStandardDailyHours,
                item.CompensatoryTimeSettlementRateFactor,
            })
            .SingleOrDefaultAsync(cancellationToken);
        var currency = preference?.CurrencyCode ?? "USD";

        // Pending fund days feed the VACACION_PROPORCIONAL suggestion (RF-019); null → legacy anniversary default.
        var pendingVacationDays = await GetPendingVacationDaysAsync(personnelFileId, cancellationToken);

        // Compensatory-time balance feeds the automatic HORAS_EXTRAS_PENDIENTES pay-off line (REQ-002 RF-013/D-19),
        // but ONLY when this settlement's plaza is the employee's principal plaza: the fund is per-employee while
        // the settlement is per-plaza, so restricting to the principal avoids a double suggestion on multi-plaza
        // retirements. No positive balance → null → the engine emits no line (retrocompatible).
        var isPrincipalPlaza = await IsPrincipalPlazaAsync(personnelFileId, assignedPositionPublicId, cancellationToken);

        CompensatoryTimeContext? compensatoryTime = null;
        if (isPrincipalPlaza)
        {
            var compensatoryBalance = await compensatoryTimeRepository.GetBalanceAsync(personnelFileId, cancellationToken);
            if (compensatoryBalance > 0m)
            {
                compensatoryTime = new CompensatoryTimeContext(
                    compensatoryBalance,
                    preference?.CompensatoryTimeStandardDailyHours ?? 8m,
                    preference?.CompensatoryTimeSettlementRateFactor ?? 1.00m);
            }
        }

        // Cyclic incomes with PAGAR_SALDO feed the INGRESO_CICLICO_PENDIENTE settlement SUGGESTION (REQ-005 §3.5):
        // the amount is KNOWN (the plan balance = total − Σ applied installments), so it travels through the
        // existing SuggestedItems channel as an editable/excludable manual line — NOT an engine-calculated line.
        // Restricted to the principal plaza (per-employee fund vs per-plaza settlement → no double suggestion on
        // multi-plaza retirements, same criterion as compensatory time). No positive balance → no suggestion.
        if (isPrincipalPlaza)
        {
            var pendingCyclicIncomes = await dbContext.Set<PersonnelFileRecurringIncome>()
                .AsNoTracking()
                .Where(item => item.PersonnelFileId == personnelFileId
                    && item.StatusCode == RecurringIncomeStatuses.Vigente
                    && item.SettlementActionCode == RecurringIncomeSettlementActions.PagarSaldo)
                .Select(item => new
                {
                    item.Reference,
                    item.ConceptNameSnapshot,
                    item.TotalAmount,
                    AppliedSum = item.Installments
                        .Where(inst => inst.IsActive && inst.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada)
                        .Sum(inst => (decimal?)inst.Amount) ?? 0m,
                })
                .ToListAsync(cancellationToken);
            foreach (var income in pendingCyclicIncomes)
            {
                var remaining = (income.TotalAmount ?? 0m) - income.AppliedSum;
                if (remaining > 0m)
                {
                    suggested.Add(new SettlementSuggestedItemDto(
                        SettlementConceptCodes.IngresoCiclicoPendiente,
                        income.Reference ?? income.ConceptNameSnapshot,
                        remaining,
                        null));
                }
            }
        }

        // One-time incomes (REQ-006 §3.5) feed the INGRESO_EVENTUAL_PENDIENTE settlement SUGGESTION: each AUTORIZADO
        // one-off carries a KNOWN amount, so it travels through the existing SuggestedItems channel as an
        // editable/excludable manual line — NOT an engine-calculated line (seed -9905, IsSystemCalculated=false),
        // like the recurring INGRESO_CICLICO_PENDIENTE. An AUTORIZADO income by definition has no active application
        // (RN-06), so the filter is just StatusCode == AUTORIZADO. Restricted to the principal plaza (per-employee
        // vs per-plaza → no double suggestion on multi-plaza retirements, same criterion as compensatory time /
        // cyclic incomes). No AUTORIZADO income → no suggestion (retrocompatible: the existing settlement is unchanged).
        if (isPrincipalPlaza)
        {
            var pendingOneTimeIncomes = await dbContext.Set<PersonnelFileOneTimeIncome>()
                .AsNoTracking()
                .Where(item => item.PersonnelFileId == personnelFileId
                    && item.StatusCode == OneTimeIncomeStatuses.Autorizado)
                .Select(item => new
                {
                    item.Reference,
                    item.ConceptNameSnapshot,
                    item.Amount,
                })
                .ToListAsync(cancellationToken);
            foreach (var income in pendingOneTimeIncomes)
            {
                suggested.Add(new SettlementSuggestedItemDto(
                    SettlementConceptCodes.IngresoEventualPendiente,
                    income.Reference ?? income.ConceptNameSnapshot,
                    income.Amount,
                    null));
            }
        }

        // One-time deductions (REQ-009 §3.5) feed the DESCUENTO_EVENTUAL_PENDIENTE settlement SUGGESTION: an
        // AUTORIZADO one-off charge the employee still owes travels through the existing SuggestedItems channel as
        // an editable/excludable MANUAL line (seed -9945, IsSystemCalculated=false) — the exact mirror of the
        // one-time INCOME block above, but on the DEDUCTION side: it REDUCES the net. An AUTORIZADO deduction by
        // definition has no active application, so the filter is just StatusCode == AUTORIZADO. Restricted to the
        // principal plaza (no double suggestion on a multi-plaza retirement). None ⇒ no suggestion (retrocompatible).
        if (isPrincipalPlaza)
        {
            var pendingOneTimeDeductions = await dbContext.Set<PersonnelFileOneTimeDeduction>()
                .AsNoTracking()
                .Where(item => item.PersonnelFileId == personnelFileId
                    && item.StatusCode == OneTimeDeductionStatuses.Autorizado)
                .Select(item => new
                {
                    item.Reference,
                    item.ConceptNameSnapshot,
                    item.Amount,
                })
                .ToListAsync(cancellationToken);
            foreach (var deduction in pendingOneTimeDeductions)
            {
                suggested.Add(new SettlementSuggestedItemDto(
                    SettlementConceptCodes.DescuentoEventualPendiente,
                    deduction.Reference ?? deduction.ConceptNameSnapshot,
                    deduction.Amount,
                    null));
            }
        }

        // Recurring deductions with DESCONTAR_SALDO feed the DESCUENTO_CICLICO_PENDIENTE settlement SUGGESTION
        // (REQ-008 §3.5): the outstanding balance is KNOWN, so it travels through the existing SuggestedItems
        // channel as an editable/excludable MANUAL line (seed -9928, IsSystemCalculated=false) — the engine does
        // not compute it. It is the mirror of the cyclic INCOME suggestion above, but on the DEDUCTION side: it
        // REDUCES the net. The balance comes from the pure rules — with compound interest it is the outstanding
        // CAPITAL, not the sum of the remaining quotas (paying a credit off early does not owe the future
        // interest). Credits with CANCELAR are written off at issue (the hook below) and suggest nothing.
        // Restricted to the principal plaza (a credit is per-employee, like the cyclic incomes → no double
        // suggestion on a multi-plaza retirement). No positive balance ⇒ no suggestion (retrocompatible).
        if (isPrincipalPlaza)
        {
            var pendingDeductions = await dbContext.Set<PersonnelFileRecurringDeduction>()
                .AsNoTracking()
                .Include(item => item.PlanSegments)
                .Include(item => item.Installments)
                .Where(item => item.PersonnelFileId == personnelFileId
                    && item.StatusCode == RecurringDeductionStatuses.Vigente
                    && item.SettlementActionCode == RecurringDeductionSettlementActions.DescontarSaldo)
                .ToListAsync(cancellationToken);

            foreach (var deduction in pendingDeductions)
            {
                var balance = deduction.OutstandingBalance();
                if (balance <= 0m)
                {
                    continue;
                }

                suggested.Add(new SettlementSuggestedItemDto(
                    SettlementConceptCodes.DescuentoCiclicoPendiente,
                    $"Saldo descuento cíclico — {deduction.RecurringDeductionTypeCode} {deduction.Reference}",
                    balance,
                    deduction.FinancialInstitution));
            }
        }

        // Pending overtime feeds the AUTOMATIC HORAS_EXTRAS_PENDIENTES_PAGO pay-off line (REQ-007 RF-014/§0.15) —
        // an ENGINE-CALCULATED line (Σ hours × factor × hourly rate), NOT a suggested manual amount like the incomes
        // above. Scoped to THE PLAZA being settled (assigned_position_public_id == assignedPositionPublicId): each
        // overtime record carries its own plaza, so per-plaza scoping avoids a double pay-off across plazas — unlike
        // the per-employee compensatory-time fund, which is resolved only for the principal plaza. Only AUTORIZADA
        // records that are active, NOT compensated by a compensatory-time credit (RF-013) and whose work date has
        // elapsed (≤ the as-of date; future organized shifts are not payable) contribute. No records ⇒ null ⇒ the
        // engine emits no line (retrocompatible).
        OvertimeContext? pendingOvertime = null;
        var asOfDate = DateOnly.FromDateTime(asOfUtc);
        var overtimeRecords = await dbContext.Set<PersonnelFileOvertimeRecord>()
            .AsNoTracking()
            .Where(record => record.PersonnelFileId == personnelFileId
                && record.AssignedPositionPublicId == assignedPositionPublicId
                && record.StatusCode == OvertimeRecordStatuses.Autorizada
                && record.IsActive
                && record.CompensatedByCreditPublicId == null
                && record.WorkDate <= asOfDate)
            .Select(record => new OvertimeContextRecord(record.PublicId, record.DurationDecimalHours, record.FactorApplied))
            .ToListAsync(cancellationToken);
        if (overtimeRecords.Count > 0)
        {
            pendingOvertime = new OvertimeContext(overtimeRecords, preference?.CompensatoryTimeStandardDailyHours ?? 8m);
        }

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
            currency,
            pendingVacationDays,
            compensatoryTime,
            pendingOvertime);
    }

    /// <summary>
    /// True when <paramref name="assignedPositionPublicId"/> is the employee's principal plaza — the same
    /// criterion the vacation-fund resolver uses: the IsPrimary assignment among the active ones (oldest
    /// StartDate as tie-breaker), falling back to the oldest active assignment, then the oldest of all.
    /// </summary>
    private async Task<bool> IsPrincipalPlazaAsync(
        long personnelFileId,
        Guid assignedPositionPublicId,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId)
            .Select(item => new { item.PublicId, item.StartDate, item.IsActive, item.IsPrimary })
            .ToListAsync(cancellationToken);
        if (assignments.Count == 0)
        {
            return false;
        }

        var active = assignments.Where(item => item.IsActive).ToList();
        var chosen = active.Where(item => item.IsPrimary).OrderBy(item => item.StartDate).FirstOrDefault()
            ?? active.OrderBy(item => item.StartDate).FirstOrDefault()
            ?? assignments.OrderBy(item => item.StartDate).FirstOrDefault();

        return chosen is not null && chosen.PublicId == assignedPositionPublicId;
    }

    public Task<decimal?> GetPendingVacationDaysAsync(long personnelFileId, CancellationToken cancellationToken) =>
        VacationFundQueries.GetAvailableEnjoymentDaysAsync(dbContext, personnelFileId, cancellationToken);

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

    public async Task<SettlementBandejaResponse> QuerySettlementsAsync(
        QuerySettlementsQuery query,
        CancellationToken cancellationToken)
    {
        var filtered = FilteredSettlements(
            query.Kind, query.StatusCode, query.CategoryCode, query.ReasonCode, query.EmployeeId,
            query.RequestFromUtc, query.RequestToUtc, query.RetirementFromUtc, query.RetirementToUtc, query.Search);

        var totalCount = await filtered.CountAsync(cancellationToken);

        // Per-status counts over the full filter (scenarios roll up under the ESCENARIO key).
        var statusCounts = await filtered
            .GroupBy(row => row.Settlement.Kind == SettlementKind.Escenario
                ? "ESCENARIO"
                : (row.Settlement.StatusCode ?? string.Empty))
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);

        var items = await filtered
            .OrderByDescending(row => row.Settlement.RequestDate)
            .ThenByDescending(row => row.Settlement.CreatedUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new SettlementListItemResponse(
                row.Settlement.PublicId,
                row.File.PublicId,
                row.File.FullName,
                row.Settlement.Kind,
                row.Settlement.StatusCode,
                row.Settlement.AssignedPositionPublicId,
                row.Settlement.PositionNameSnapshot,
                row.Settlement.RequestDate,
                row.Settlement.RetirementDate,
                row.Settlement.RetirementCategoryCode,
                row.Settlement.RetirementCategoryNameSnapshot,
                row.Settlement.RetirementReasonCode,
                row.Settlement.RetirementReasonNameSnapshot,
                row.Settlement.RequesterNameSnapshot,
                row.Settlement.TotalIncomes,
                row.Settlement.TotalDeductions,
                row.Settlement.NetPay,
                row.Settlement.TotalEmployerCharges,
                row.Settlement.ProvisionTotal,
                row.Settlement.CurrencyCode,
                row.Settlement.IssuedAtUtc,
                row.Settlement.AnnulledAtUtc))
            .ToArrayAsync(cancellationToken);

        return new SettlementBandejaResponse(items, query.PageNumber, query.PageSize, totalCount, statusCounts);
    }

    public async Task<IReadOnlyCollection<SettlementExportRow>> GetSettlementExportRowsAsync(
        ExportSettlementsQuery query,
        CancellationToken cancellationToken)
    {
        var filtered = FilteredSettlements(
            query.Kind, query.StatusCode, query.CategoryCode, query.ReasonCode, query.EmployeeId,
            query.RequestFromUtc, query.RequestToUtc, query.RetirementFromUtc, query.RetirementToUtc, query.Search)
            .OrderByDescending(row => row.Settlement.RequestDate)
            .ThenByDescending(row => row.Settlement.CreatedUtc);

        var limited = query.MaxRows is { } maxRows ? filtered.Take(maxRows + 1) : filtered;

        return await limited
            .Select(row => new SettlementExportRow(
                row.File.FullName,
                row.Settlement.PositionNameSnapshot ?? row.Settlement.AssignedPositionPublicId.ToString(),
                row.Settlement.Kind == SettlementKind.Escenario ? "ESCENARIO (SIMULACIÓN)" : "LIQUIDACION",
                row.Settlement.Kind == SettlementKind.Escenario ? "SIMULACIÓN — SIN EFECTOS" : (row.Settlement.StatusCode ?? string.Empty),
                row.Settlement.RequesterNameSnapshot,
                row.Settlement.RequestDate,
                row.Settlement.RetirementDate,
                row.Settlement.RetirementCategoryNameSnapshot ?? row.Settlement.RetirementCategoryCode,
                row.Settlement.RetirementReasonNameSnapshot ?? row.Settlement.RetirementReasonCode,
                row.Settlement.TotalIncomes,
                row.Settlement.TotalDeductions,
                row.Settlement.NetPay,
                row.Settlement.TotalEmployerCharges,
                row.Settlement.ProvisionTotal,
                row.Settlement.CurrencyCode,
                row.Settlement.Notes))
            .ToArrayAsync(cancellationToken);
    }

    private IQueryable<SettlementFileRow> FilteredSettlements(
        string? kind,
        string? statusCode,
        string? categoryCode,
        string? reasonCode,
        Guid? employeeId,
        DateTime? requestFromUtc,
        DateTime? requestToUtc,
        DateTime? retirementFromUtc,
        DateTime? retirementToUtc,
        string? search)
    {
        // Member-init (not a positional record ctor): EF composes further Where/GroupBy over this
        // intermediate projection reliably only with the member-init/transparent-identifier shape.
        var query =
            from settlement in dbContext.PersonnelFileSettlements.AsNoTracking()
            join file in dbContext.PersonnelFiles.AsNoTracking() on settlement.PersonnelFileId equals file.Id
            where settlement.IsActive
            select new SettlementFileRow { Settlement = settlement, File = file };

        if (!string.IsNullOrWhiteSpace(kind) && Enum.TryParse<SettlementKind>(kind.Trim(), ignoreCase: true, out var parsedKind))
        {
            query = query.Where(row => row.Settlement.Kind == parsedKind);
        }

        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            var normalizedStatus = statusCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.Settlement.StatusCode == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(categoryCode))
        {
            var normalizedCategory = categoryCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.Settlement.RetirementCategoryCode == normalizedCategory);
        }

        if (!string.IsNullOrWhiteSpace(reasonCode))
        {
            var normalizedReason = reasonCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.Settlement.RetirementReasonCode == normalizedReason);
        }

        if (employeeId is { } employeePublicId)
        {
            query = query.Where(row => row.File.PublicId == employeePublicId);
        }

        if (requestFromUtc is { } requestFrom)
        {
            query = query.Where(row => row.Settlement.RequestDate >= requestFrom.Date);
        }

        if (requestToUtc is { } requestTo)
        {
            query = query.Where(row => row.Settlement.RequestDate <= requestTo.Date);
        }

        if (retirementFromUtc is { } retirementFrom)
        {
            query = query.Where(row => row.Settlement.RetirementDate >= retirementFrom.Date);
        }

        if (retirementToUtc is { } retirementTo)
        {
            query = query.Where(row => row.Settlement.RetirementDate <= retirementTo.Date);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(row =>
                row.File.FullName.ToUpper().Contains(term)
                || row.Settlement.RequesterNameSnapshot.ToUpper().Contains(term)
                || (row.Settlement.PositionNameSnapshot != null && row.Settlement.PositionNameSnapshot.ToUpper().Contains(term)));
        }

        return query;
    }

    private sealed class SettlementFileRow
    {
        public required PersonnelFileSettlement Settlement { get; init; }

        public required Domain.PersonnelFiles.PersonnelFile File { get; init; }
    }

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
