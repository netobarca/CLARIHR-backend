using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Shared plumbing of the settlement write paths: requester resolution (D-06 hardened: HR only),
/// engine-input assembly from the persisted record, and the line-sync that applies an engine result
/// back onto the entity (stable line PublicIds — adjustments survive, D-14).
/// </summary>
internal static class SettlementCalculationSupport
{
    public static SettlementParametersInput BuildParameters(PersonnelFileSettlement settlement) => new(
        settlement.MinimumMonthlyWage,
        settlement.IndemnityCapMultiplier,
        settlement.ResignationCapMultiplier,
        settlement.VacationDays,
        settlement.VacationPremiumPercent,
        settlement.AguinaldoDays > 0 ? settlement.AguinaldoDays : null,
        settlement.ResignationBenefitDays,
        settlement.ResignationMinimumServiceYears,
        settlement.AguinaldoExemptionMultiplier,
        settlement.MonthDivisorDays,
        settlement.YearDivisorDays);

    public static IReadOnlyList<SettlementLineState> BuildStates(PersonnelFileSettlement settlement) =>
        settlement.Lines
            .Select(line => new SettlementLineState(
                line.PublicId,
                line.ConceptCode,
                line.ConceptClass,
                line.IsIncluded,
                line.UnitsOverridden ? line.UnitsOrDays : null,
                line.OverrideAmount,
                !line.IsSystemCalculated,
                line.IsSystemCalculated ? 0m : line.CalculatedAmount,
                line.Description,
                line.CounterpartyName))
            .ToArray();

    /// <summary>
    /// Runs the engine over the settlement (recalculation over its lines, or a fresh generation when
    /// <paramref name="regenerate"/>) and applies the result: line computations in place (matched by
    /// PublicId), new lines created, derived bases and five-section totals written (RN-13).
    /// </summary>
    public static IReadOnlyCollection<SettlementWarningResponse> Recalculate(
        PersonnelFileSettlement settlement,
        SettlementCalculationContext context,
        RetirementSeparationType separationType,
        Guid tenantId,
        bool regenerate = false)
    {
        if (regenerate)
        {
            settlement.ClearLines();
        }

        var input = new SettlementCalculationInput(
            settlement.Kind,
            separationType,
            settlement.PlazaStartDate,
            settlement.RetirementDate,
            settlement.MonthlyBaseSalary > 0 ? settlement.MonthlyBaseSalary : context.MonthlyBaseSalary ?? 0m,
            BuildParameters(settlement),
            context.Concepts
                .Select(concept => new SettlementConceptConfig(
                    concept.Code, concept.Name, concept.ConceptClass, concept.AffectsIsss, concept.AffectsAfp,
                    concept.AffectsRenta, concept.ExemptionRule, concept.ExemptionMultiplier,
                    concept.IsSystemCalculated, concept.DefaultRatePercent, concept.SortOrder))
                .ToArray(),
            context.SuggestedItems
                .Select(item => new SuggestedPlazaItem(item.ConceptCode, item.Description, item.Amount, item.CounterpartyName))
                .ToArray(),
            new ContributionSchemeInput(context.Isss.EmployeeRatePercent, context.Isss.EmployerRatePercent, context.Isss.ContributionCap),
            new ContributionSchemeInput(context.Afp.EmployeeRatePercent, context.Afp.EmployerRatePercent, context.Afp.ContributionCap),
            context.RentaBrackets
                .Select(bracket => new TaxBracketInput(bracket.LowerBound, bracket.UpperBound, bracket.FixedFee, bracket.RatePercent, bracket.ExcessOver))
                .ToArray(),
            settlement.Lines.Count == 0 ? [] : BuildStates(settlement),
            context.PendingVacationDays,
            context.CompensatoryTime is null
                ? null
                : new CompensatoryTimeInput(
                    context.CompensatoryTime.PendingHours,
                    context.CompensatoryTime.StandardDailyHours,
                    context.CompensatoryTime.RateFactor));

        var result = SettlementCalculationRules.Calculate(input);

        var linesById = settlement.Lines.ToDictionary(line => line.PublicId, line => line);
        foreach (var lineResult in result.Lines)
        {
            PersonnelFileSettlementLine line;
            if (lineResult.LinePublicId is { } lineId && linesById.TryGetValue(lineId, out var existing))
            {
                line = existing;
            }
            else
            {
                line = PersonnelFileSettlementLine.Create(
                    lineResult.ConceptClass,
                    lineResult.ConceptCode,
                    lineResult.ConceptName,
                    lineResult.Description,
                    lineResult.IsSystemCalculated,
                    lineResult.SortOrder);
                line.SetTenantId(tenantId);
                settlement.AddLine(line);
            }

            line.ApplyComputation(
                lineResult.CalculationBase,
                lineResult.UnitsOrDays,
                lineResult.CalculatedAmount,
                lineResult.ExemptAmount,
                lineResult.TaxableExcessAmount,
                lineResult.CalculationDetail,
                lineResult.IsZeroByLaw,
                lineResult.ZeroReasonCode,
                lineResult.CounterpartyName);
        }

        settlement.ApplyCalculation(
            input.MonthlyBaseSalary,
            result.Derived.SeniorityYears,
            result.Derived.SeniorityDays,
            result.Derived.CappedMonthlySalaryIndemnity,
            result.Derived.CappedMonthlySalaryResignation,
            result.Totals.TotalIncomes,
            result.Totals.TotalDeductions,
            result.Totals.NetPay,
            result.Totals.TotalEmployerCharges,
            result.Totals.ProvisionTotal);

        return result.Warnings
            .Select(warning => new SettlementWarningResponse(
                warning.Code,
                string.IsNullOrEmpty(warning.ConceptCode) ? null : warning.ConceptCode))
            .Concat(settlement.CostCenterPublicId is null
                ? [new SettlementWarningResponse("SETTLEMENT_WARNING_NO_COST_CENTER", null)]
                : Array.Empty<SettlementWarningResponse>())
            .ToArray();
    }
}

/// <summary>Requester resolution (D-06 hardened: "solamente puede ser RRHH").</summary>
internal static class SettlementRequesterSupport
{
    /// <summary>
    /// Default: the registering manager's own personnel file. An explicit different file must belong to the
    /// HR functional area when the company preference declares one; without a configured HR area only the
    /// registering manager's file is accepted (fail-closed reading of the ratification).
    /// </summary>
    public static async Task<Result<SettlementRequesterLookup>> ResolveAsync(
        Guid tenantId,
        Guid? requestedRequesterFilePublicId,
        Guid currentUserId,
        IPersonnelFileRepository personnelFileRepository,
        ISettlementRepository settlementRepository,
        CancellationToken cancellationToken)
    {
        var ownFile = await personnelFileRepository.GetByLinkedUserIdAsync(tenantId, currentUserId, cancellationToken);

        var targetPublicId = requestedRequesterFilePublicId ?? ownFile?.PublicId;
        if (targetPublicId is null)
        {
            return Result<SettlementRequesterLookup>.Failure(SettlementErrors.RequesterNotHr);
        }

        var lookup = await settlementRepository.GetRequesterLookupAsync(tenantId, targetPublicId.Value, cancellationToken);
        if (lookup is null || !lookup.IsActive)
        {
            return Result<SettlementRequesterLookup>.Failure(SettlementErrors.RequesterNotHr);
        }

        if (ownFile is not null && lookup.PersonnelFilePublicId == ownFile.PublicId)
        {
            return Result<SettlementRequesterLookup>.Success(lookup);
        }

        var hrConfigured = !string.IsNullOrWhiteSpace(lookup.HrFunctionalAreaCode);
        if (!hrConfigured ||
            !string.Equals(lookup.OrgUnitFunctionalAreaCode, lookup.HrFunctionalAreaCode, StringComparison.OrdinalIgnoreCase))
        {
            return Result<SettlementRequesterLookup>.Failure(SettlementErrors.RequesterNotHr);
        }

        return Result<SettlementRequesterLookup>.Success(lookup);
    }
}

internal sealed class AddSettlementScenarioCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ISettlementRepository settlementRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddSettlementScenarioCommand, PersonnelFileSettlementResponse>
{
    public async Task<Result<PersonnelFileSettlementResponse>> Handle(
        AddSettlementScenarioCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageSettlementsAsync<PersonnelFileSettlementResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee || !personnelFile.IsActive)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var currentUserId);

        // D-20: the subject employee never manages their own settlement (403 dedicated).
        if (personnelFile.LinkedUserPublicId is { } linkedUser && linkedUser == currentUserId)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.SelfActionForbidden);
        }

        // Hypothetical motive: active + hierarchy-coherent catalog codes (reuses the retirement validation).
        var catalogError = await PersonnelReferenceCatalogValidation.ValidateRetirementCodesAsync(
            personnelFileRepository, personnelFile.TenantId, command.Item.RetirementCategoryCode, command.Item.RetirementReasonCode, cancellationToken);
        if (catalogError != Error.None)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(catalogError);
        }

        var context = await settlementRepository.GetCalculationContextAsync(
            personnelFile.TenantId, personnelFile.Id, command.Item.AssignedPositionPublicId, dateTimeProvider.UtcNow, cancellationToken);
        if (context is null || !context.Plaza.IsActive)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.PositionInvalid);
        }

        // E-04: a retired employee gets a real settlement, not a scenario.
        if (context.ProfileRetirementDate is not null)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.ScenarioEmployeeRetired);
        }

        if (command.Item.RequestDate.Date > dateTimeProvider.UtcNow.Date
            || command.Item.EstimatedRetirementDate.Date < context.Plaza.StartDate.Date)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.DateIncoherent);
        }

        if (context.MonthlyBaseSalary is not > 0)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.BaseSalaryMissing);
        }

        // RN-001.7: minimum wage from the "ficha" (ratified §17.16) or the explicit override.
        var minimumWage = context.ProfileMinimumMonthlyWage ?? command.Item.MinimumMonthlyWage;
        if (minimumWage is not > 0)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.MinimumWageMissing);
        }

        var requesterResult = await SettlementRequesterSupport.ResolveAsync(
            personnelFile.TenantId, command.Item.RequesterFilePublicId, currentUserId, personnelFileRepository, settlementRepository, cancellationToken);
        if (requesterResult.IsFailure)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(requesterResult.Error);
        }

        var separationType = await settlementRepository.GetSeparationTypeAsync(
            personnelFile.TenantId, command.Item.RetirementCategoryCode, cancellationToken) ?? RetirementSeparationType.Otra;
        var (categoryName, reasonName) = await personnelFileRepository.GetRetirementCatalogNamesAsync(
            personnelFile.TenantId, command.Item.RetirementCategoryCode, command.Item.RetirementReasonCode, cancellationToken);

        var settlement = PersonnelFileSettlement.CreateScenario(
            context.Plaza.AssignedPositionPublicId,
            context.Plaza.PositionTitle,
            context.Plaza.StartDate,
            context.Plaza.CostCenterPublicId,
            context.Plaza.CostCenterName,
            command.Item.EstimatedRetirementDate,
            command.Item.RetirementCategoryCode,
            categoryName,
            command.Item.RetirementReasonCode,
            reasonName,
            requesterResult.Value.PersonnelFilePublicId,
            requesterResult.Value.FullName,
            command.Item.RequestDate,
            command.Item.Notes,
            currentUserId,
            context.CurrencyCode);
        settlement.BindToPersonnelFile(personnelFile.Id);
        settlement.SetTenantId(personnelFile.TenantId);
        settlement.UpdateParameters(
            minimumWage.Value,
            indemnityCapMultiplier: 4m,
            resignationCapMultiplier: 2m,
            vacationDays: 15m,
            vacationPremiumPercent: 30m,
            aguinaldoDays: 0m,
            resignationBenefitDays: 15m,
            resignationMinimumServiceYears: 2,
            aguinaldoExemptionMultiplier: 2m,
            monthDivisorDays: 30,
            yearDivisorDays: 365);

        var warnings = SettlementCalculationSupport.Recalculate(settlement, context, separationType, personnelFile.TenantId);
        await settlementRepository.AddAsync(settlement, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            var response = SettlementResponseMapper.Map(settlement, warnings);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Added settlement scenario for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileSettlementResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

/// <summary>Shared mutation skeleton: load file + settlement, guard token/editability, mutate, recalc, audit, save.</summary>
internal abstract class SettlementMutationHandlerBase : PersonnelFileEmployeeCommandHandlerBase
{
    protected static async Task<(Result<PersonnelFileSettlementResponse>? Failure, Domain.PersonnelFiles.PersonnelFile? File, PersonnelFileSettlement? Settlement)>
        LoadEditableAsync(
            Guid personnelFileId,
            Guid settlementId,
            Guid concurrencyToken,
            ITenantContext tenantContext,
            IPersonnelFileAuthorizationService authorizationService,
            IPersonnelFileRepository personnelFileRepository,
            ISettlementRepository settlementRepository,
            ICurrentUserService currentUserService,
            CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageSettlementsAsync<PersonnelFileSettlementResponse>(
            personnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return (failure, null, null);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var currentUserId);
        if (personnelFile!.LinkedUserPublicId is { } linkedUser && linkedUser == currentUserId)
        {
            return (Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.SelfActionForbidden), null, null);
        }

        var settlement = await settlementRepository.GetTrackedAsync(personnelFile.Id, settlementId, cancellationToken);
        if (settlement is null || !settlement.IsActive)
        {
            return (Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.NotFound), null, null);
        }

        if (settlement.ConcurrencyToken != concurrencyToken)
        {
            return (Result<PersonnelFileSettlementResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null, null);
        }

        if (!settlement.IsEditable)
        {
            return (Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.StateRuleViolation), null, null);
        }

        return (null, personnelFile, settlement);
    }

    protected static async Task<Result<PersonnelFileSettlementResponse>> RecalculatePersistAsync(
        Domain.PersonnelFiles.PersonnelFile personnelFile,
        PersonnelFileSettlement settlement,
        string auditMessage,
        ISettlementRepository settlementRepository,
        IDateTimeProvider dateTimeProvider,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var context = await settlementRepository.GetCalculationContextAsync(
            personnelFile.TenantId, personnelFile.Id, settlement.AssignedPositionPublicId, dateTimeProvider.UtcNow, cancellationToken);
        if (context is null)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.PositionInvalid);
        }

        var separationType = await settlementRepository.GetSeparationTypeAsync(
            personnelFile.TenantId, settlement.RetirementCategoryCode, cancellationToken) ?? RetirementSeparationType.Otra;

        var warnings = SettlementCalculationSupport.Recalculate(settlement, context, separationType, personnelFile.TenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            var response = SettlementResponseMapper.Map(settlement, warnings);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, auditMessage, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileSettlementResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateSettlementCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ISettlementRepository settlementRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : SettlementMutationHandlerBase,
      ICommandHandler<UpdateSettlementCommand, PersonnelFileSettlementResponse>
{
    public async Task<Result<PersonnelFileSettlementResponse>> Handle(
        UpdateSettlementCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, settlement) = await LoadEditableAsync(
            command.PersonnelFileId, command.SettlementId, command.ConcurrencyToken,
            tenantContext, authorizationService, personnelFileRepository, settlementRepository, currentUserService, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (command.RequestDate.Date > dateTimeProvider.UtcNow.Date)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.DateIncoherent);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var currentUserId);
        var requesterResult = await SettlementRequesterSupport.ResolveAsync(
            personnelFile!.TenantId,
            command.RequesterFilePublicId ?? settlement!.RequesterFilePublicId,
            currentUserId,
            personnelFileRepository,
            settlementRepository,
            cancellationToken);
        if (requesterResult.IsFailure)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(requesterResult.Error);
        }

        // Scenario assumptions (hypothetical motive/date) — only when supplied and only on scenarios.
        if (command.EstimatedRetirementDate is not null || command.RetirementCategoryCode is not null || command.RetirementReasonCode is not null)
        {
            if (settlement!.Kind != SettlementKind.Escenario)
            {
                return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.StateRuleViolation);
            }

            var categoryCode = command.RetirementCategoryCode ?? settlement.RetirementCategoryCode;
            var reasonCode = command.RetirementReasonCode ?? settlement.RetirementReasonCode;
            var estimatedDate = command.EstimatedRetirementDate ?? settlement.RetirementDate;

            var catalogError = await PersonnelReferenceCatalogValidation.ValidateRetirementCodesAsync(
                personnelFileRepository, personnelFile.TenantId, categoryCode, reasonCode, cancellationToken);
            if (catalogError != Error.None)
            {
                return Result<PersonnelFileSettlementResponse>.Failure(catalogError);
            }

            if (estimatedDate.Date < settlement.PlazaStartDate.Date)
            {
                return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.DateIncoherent);
            }

            var (categoryName, reasonName) = await personnelFileRepository.GetRetirementCatalogNamesAsync(
                personnelFile.TenantId, categoryCode, reasonCode, cancellationToken);
            settlement.UpdateScenarioAssumptions(estimatedDate, categoryCode, categoryName, reasonCode, reasonName);
        }

        settlement!.UpdateHeader(
            requesterResult.Value.PersonnelFilePublicId,
            requesterResult.Value.FullName,
            command.RequestDate,
            command.Notes);
        settlement.UpdateParameters(
            command.Parameters.MinimumMonthlyWage,
            command.Parameters.IndemnityCapMultiplier,
            command.Parameters.ResignationCapMultiplier,
            command.Parameters.VacationDays,
            command.Parameters.VacationPremiumPercent,
            command.Parameters.AguinaldoDays,
            command.Parameters.ResignationBenefitDays,
            command.Parameters.ResignationMinimumServiceYears,
            command.Parameters.AguinaldoExemptionMultiplier,
            command.Parameters.MonthDivisorDays,
            command.Parameters.YearDivisorDays);

        TouchPersonnelFile(personnelFile);
        return await RecalculatePersistAsync(
            personnelFile, settlement, $"Updated settlement for {personnelFile.FullName}.",
            settlementRepository, dateTimeProvider, auditService, unitOfWork, cancellationToken);
    }
}

internal sealed class UpdateSettlementLineCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ISettlementRepository settlementRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : SettlementMutationHandlerBase,
      ICommandHandler<UpdateSettlementLineCommand, PersonnelFileSettlementResponse>
{
    public async Task<Result<PersonnelFileSettlementResponse>> Handle(
        UpdateSettlementLineCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, settlement) = await LoadEditableAsync(
            command.PersonnelFileId, command.SettlementId, command.ConcurrencyToken,
            tenantContext, authorizationService, personnelFileRepository, settlementRepository, currentUserService, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var line = settlement!.Lines.SingleOrDefault(item => item.PublicId == command.LineId);
        if (line is null)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.LineNotFound);
        }

        if (command.OverrideAmount is not null && string.IsNullOrWhiteSpace(command.OverrideReason))
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.OverrideNoteRequired);
        }

        if (command.IsIncluded is { } isIncluded)
        {
            line.SetIncluded(isIncluded);
        }

        if (command.ClearUnitsOverride)
        {
            line.ClearUnitsOverride();
        }
        else if (command.UnitsOrDays is { } units)
        {
            line.SetUnitsOrDays(units);
        }

        if (command.ClearOverride)
        {
            line.ClearOverride();
        }
        else if (command.OverrideAmount is { } overrideAmount)
        {
            line.SetOverride(overrideAmount, command.OverrideReason!);
        }

        if (!line.IsSystemCalculated && (command.Description is not null || command.ManualAmount is not null))
        {
            line.UpdateManual(
                command.Description ?? line.Description ?? line.ConceptNameSnapshot,
                command.ManualAmount ?? line.CalculatedAmount);
        }

        TouchPersonnelFile(personnelFile!);
        return await RecalculatePersistAsync(
            personnelFile!, settlement, $"Adjusted settlement line {line.ConceptCode} for {personnelFile!.FullName}.",
            settlementRepository, dateTimeProvider, auditService, unitOfWork, cancellationToken);
    }
}

internal sealed class AddSettlementManualLineCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ISettlementRepository settlementRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : SettlementMutationHandlerBase,
      ICommandHandler<AddSettlementManualLineCommand, PersonnelFileSettlementResponse>
{
    public async Task<Result<PersonnelFileSettlementResponse>> Handle(
        AddSettlementManualLineCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, settlement) = await LoadEditableAsync(
            command.PersonnelFileId, command.SettlementId, command.ConcurrencyToken,
            tenantContext, authorizationService, personnelFileRepository, settlementRepository, currentUserService, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var context = await settlementRepository.GetCalculationContextAsync(
            personnelFile!.TenantId, personnelFile.Id, settlement!.AssignedPositionPublicId, dateTimeProvider.UtcNow, cancellationToken);
        if (context is null)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.PositionInvalid);
        }

        // The concept must be an active MANUAL (non-engine) concept of the catalog (RN-002.4).
        var concept = context.Concepts.SingleOrDefault(item =>
            string.Equals(item.Code, command.ConceptCode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (concept is null || concept.IsSystemCalculated || !concept.IsActive)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.ConceptInvalid);
        }

        var line = PersonnelFileSettlementLine.Create(
            concept.ConceptClass, concept.Code, concept.Name, command.Description, isSystemCalculated: false, concept.SortOrder);
        line.SetTenantId(personnelFile.TenantId);
        line.UpdateManual(command.Description, command.Amount);
        settlement.AddLine(line);

        TouchPersonnelFile(personnelFile);
        return await RecalculatePersistAsync(
            personnelFile, settlement, $"Added manual settlement line {concept.Code} for {personnelFile.FullName}.",
            settlementRepository, dateTimeProvider, auditService, unitOfWork, cancellationToken);
    }
}

internal sealed class RemoveSettlementLineCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ISettlementRepository settlementRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : SettlementMutationHandlerBase,
      ICommandHandler<RemoveSettlementLineCommand, PersonnelFileSettlementResponse>
{
    public async Task<Result<PersonnelFileSettlementResponse>> Handle(
        RemoveSettlementLineCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, settlement) = await LoadEditableAsync(
            command.PersonnelFileId, command.SettlementId, command.ConcurrencyToken,
            tenantContext, authorizationService, personnelFileRepository, settlementRepository, currentUserService, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var line = settlement!.Lines.SingleOrDefault(item => item.PublicId == command.LineId);
        if (line is null)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.LineNotFound);
        }

        settlement.RemoveLine(line);
        TouchPersonnelFile(personnelFile!);
        return await RecalculatePersistAsync(
            personnelFile!, settlement, $"Removed settlement line {line.ConceptCode} for {personnelFile!.FullName}.",
            settlementRepository, dateTimeProvider, auditService, unitOfWork, cancellationToken);
    }
}

internal sealed class RegenerateSettlementLinesCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ISettlementRepository settlementRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : SettlementMutationHandlerBase,
      ICommandHandler<RegenerateSettlementLinesCommand, PersonnelFileSettlementResponse>
{
    public async Task<Result<PersonnelFileSettlementResponse>> Handle(
        RegenerateSettlementLinesCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, settlement) = await LoadEditableAsync(
            command.PersonnelFileId, command.SettlementId, command.ConcurrencyToken,
            tenantContext, authorizationService, personnelFileRepository, settlementRepository, currentUserService, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var context = await settlementRepository.GetCalculationContextAsync(
            personnelFile!.TenantId, personnelFile.Id, settlement!.AssignedPositionPublicId, dateTimeProvider.UtcNow, cancellationToken);
        if (context is null)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.PositionInvalid);
        }

        var separationType = await settlementRepository.GetSeparationTypeAsync(
            personnelFile.TenantId, settlement.RetirementCategoryCode, cancellationToken) ?? RetirementSeparationType.Otra;

        var warnings = SettlementCalculationSupport.Recalculate(
            settlement, context, separationType, personnelFile.TenantId, regenerate: true);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            var response = SettlementResponseMapper.Map(settlement, warnings);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Regenerated settlement lines for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileSettlementResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeleteSettlementScenarioCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ISettlementRepository settlementRepository,
    ICurrentUserService currentUserService,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeleteSettlementScenarioCommand, bool>
{
    public async Task<Result<bool>> Handle(DeleteSettlementScenarioCommand command, CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageSettlementsAsync<bool>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        _ = Guid.TryParse(currentUserService.UserId, out var currentUserId);
        if (personnelFile!.LinkedUserPublicId is { } linkedUser && linkedUser == currentUserId)
        {
            return Result<bool>.Failure(SettlementErrors.SelfActionForbidden);
        }

        var settlement = await settlementRepository.GetTrackedAsync(personnelFile.Id, command.SettlementId, cancellationToken);
        if (settlement is null || !settlement.IsActive)
        {
            return Result<bool>.Failure(SettlementErrors.NotFound);
        }

        if (settlement.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (settlement.Kind != SettlementKind.Escenario)
        {
            return Result<bool>.Failure(SettlementErrors.StateRuleViolation);
        }

        settlement.SetActive(false);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Deleted settlement scenario for {personnelFile.FullName}.", new { settlement.PublicId }, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddSettlementCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ISettlementRepository settlementRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddSettlementCommand, PersonnelFileSettlementResponse>
{
    public async Task<Result<PersonnelFileSettlementResponse>> Handle(
        AddSettlementCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageSettlementsAsync<PersonnelFileSettlementResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var currentUserId);
        if (personnelFile.LinkedUserPublicId is { } linkedUser && linkedUser == currentUserId)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.SelfActionForbidden);
        }

        // D-03: the anchor is the employee's most recent retirement, and it must be EXECUTED.
        var retirement = await settlementRepository.GetLatestRetirementAsync(personnelFile.Id, cancellationToken);
        if (retirement is null || retirement.StatusCode != RetirementRequestStatuses.Ejecutada)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(
                retirement?.StatusCode == RetirementRequestStatuses.Revertida
                    ? SettlementErrors.RetirementReverted
                    : SettlementErrors.RetirementNotExecuted);
        }

        // D-10: the plaza must be one of the assignments that retirement closed.
        if (!retirement.ClosedAssignmentPublicIds.Contains(command.Item.AssignedPositionPublicId))
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.PositionNotInRetirement);
        }

        // D-16: one live settlement per (retirement × plaza) — the filtered unique index is the DB backstop.
        if (await settlementRepository.HasLiveSettlementAsync(retirement.Id, command.Item.AssignedPositionPublicId, cancellationToken))
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.AlreadyExistsForPosition);
        }

        if (command.Item.RequestDate.Date > dateTimeProvider.UtcNow.Date)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.DateIncoherent);
        }

        var context = await settlementRepository.GetCalculationContextAsync(
            personnelFile.TenantId, personnelFile.Id, command.Item.AssignedPositionPublicId, dateTimeProvider.UtcNow, cancellationToken);
        if (context is null)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.PositionInvalid);
        }

        if (context.MonthlyBaseSalary is not > 0)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.BaseSalaryMissing);
        }

        // RN-001.7: a retired profile is locked, so the explicit override is the escape hatch here.
        var minimumWage = context.ProfileMinimumMonthlyWage ?? command.Item.MinimumMonthlyWage;
        if (minimumWage is not > 0)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.MinimumWageMissing);
        }

        var requesterResult = await SettlementRequesterSupport.ResolveAsync(
            personnelFile.TenantId, command.Item.RequesterFilePublicId, currentUserId, personnelFileRepository, settlementRepository, cancellationToken);
        if (requesterResult.IsFailure)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(requesterResult.Error);
        }

        var separationType = await settlementRepository.GetSeparationTypeAsync(
            personnelFile.TenantId, retirement.RetirementCategoryCode, cancellationToken) ?? RetirementSeparationType.Otra;

        var settlement = PersonnelFileSettlement.CreateSettlement(
            retirement.PublicId,
            context.Plaza.AssignedPositionPublicId,
            context.Plaza.PositionTitle,
            context.Plaza.StartDate,
            context.Plaza.CostCenterPublicId,
            context.Plaza.CostCenterName,
            retirement.RetirementDate,
            retirement.RetirementCategoryCode,
            retirement.RetirementCategoryNameSnapshot,
            retirement.RetirementReasonCode,
            retirement.RetirementReasonNameSnapshot,
            requesterResult.Value.PersonnelFilePublicId,
            requesterResult.Value.FullName,
            command.Item.RequestDate,
            command.Item.Notes,
            currentUserId,
            context.CurrencyCode);
        settlement.BindToPersonnelFile(personnelFile.Id);
        settlement.BindToRetirementRequest(retirement.Id);
        settlement.SetTenantId(personnelFile.TenantId);
        settlement.UpdateParameters(
            minimumWage.Value,
            indemnityCapMultiplier: 4m,
            resignationCapMultiplier: 2m,
            vacationDays: 15m,
            vacationPremiumPercent: 30m,
            aguinaldoDays: 0m,
            resignationBenefitDays: 15m,
            resignationMinimumServiceYears: 2,
            aguinaldoExemptionMultiplier: 2m,
            monthDivisorDays: 30,
            yearDivisorDays: 365);

        var warnings = SettlementCalculationSupport.Recalculate(settlement, context, separationType, personnelFile.TenantId);
        await settlementRepository.AddAsync(settlement, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            var response = SettlementResponseMapper.Map(settlement, warnings);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Added settlement for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileSettlementResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class IssueSettlementCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ISettlementRepository settlementRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : SettlementMutationHandlerBase,
      ICommandHandler<IssueSettlementCommand, PersonnelFileSettlementResponse>
{
    private const string SettlementActionTypeCode = "LIQUIDACION";
    private const string AppliedActionStatusCode = "APLICADA";

    public async Task<Result<PersonnelFileSettlementResponse>> Handle(
        IssueSettlementCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, settlement) = await LoadEditableAsync(
            command.PersonnelFileId, command.SettlementId, command.ConcurrencyToken,
            tenantContext, authorizationService, personnelFileRepository, settlementRepository, currentUserService, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (settlement!.Kind != SettlementKind.Liquidacion)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.StateRuleViolation);
        }

        // Typed pre-checks of the domain guards (RN-14) so the API returns coded 422s, not 500s.
        if (!settlement.Lines.Any(line => line is { ConceptClass: SettlementConceptClass.Ingreso, IsIncluded: true }))
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.IssueRequiresIncome);
        }

        if (settlement.NetPay < 0 && !command.ConfirmNegativeNet)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.NetNegativeConfirmationRequired);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var issuedByUserId);
        settlement.MarkIssued(issuedByUserId, dateTimeProvider.UtcNow, command.ConfirmNegativeNet);

        // D-15: append-only LIQUIDACION action in the employee's journal (documental act — FA-1: no payroll write).
        var action = PersonnelFilePersonnelAction.Create(
            SettlementActionTypeCode,
            AppliedActionStatusCode,
            actionDateUtc: dateTimeProvider.UtcNow,
            effectiveFromUtc: settlement.RetirementDate,
            effectiveToUtc: null,
            description: $"Liquidación emitida — plaza {settlement.PositionNameSnapshot ?? settlement.AssignedPositionPublicId.ToString()}, neto {settlement.NetPay:0.00} {settlement.CurrencyCode}.",
            reference: settlement.PublicId.ToString(),
            amount: settlement.NetPay,
            currencyCode: settlement.CurrencyCode,
            isSystemGenerated: true);
        action.BindToPersonnelFile(personnelFile!.Id);
        action.SetTenantId(personnelFile.TenantId);
        _ = await employeeRepository.AddPersonnelActionAsync(action, cancellationToken);

        // REQ-005 §3.5: a settled employee's cyclic incomes end. Finalize the VIGENTE recurring incomes in the same
        // transaction (idempotent; stamps ClosedBySettlementPublicId so annulment can reopen exactly these).
        var cyclicToFinalize = await employeeRepository.GetVigenteRecurringIncomesForSettlementAsync(
            personnelFile.Id, cancellationToken);
        foreach (var income in cyclicToFinalize)
        {
            income.FinalizeBySettlement(settlement.PublicId, dateTimeProvider.UtcNow);
        }

        // REQ-006 §3.5: mark the employee's AUTORIZADO one-time incomes applied by THIS settlement, but ONLY those
        // whose INGRESO_EVENTUAL_PENDIENTE suggestion line was kept INCLUDED (unlike the cyclic hook, an EXCLUDED
        // line ⇒ the income is NOT paid via the settlement and stays AUTORIZADO). The line ↔ income link is the
        // suggestion description (reference ?? conceptNameSnapshot). Idempotent: MarkAppliedBySettlement stamps
        // AppliedBySettlementPublicId so annulment reopens exactly these.
        var includedEventualKeys = settlement.Lines
            .Where(line => line.IsIncluded && line.ConceptCode == SettlementConceptCodes.IngresoEventualPendiente)
            .Select(line => line.Description)
            .Where(description => description is not null)
            .Select(description => description!)
            .ToHashSet(StringComparer.Ordinal);
        if (includedEventualKeys.Count > 0)
        {
            var oneTimeToApply = await employeeRepository.GetAutorizadoOneTimeIncomesForSettlementAsync(
                personnelFile.Id, cancellationToken);
            foreach (var income in oneTimeToApply)
            {
                if (includedEventualKeys.Contains(income.Reference ?? income.ConceptNameSnapshot))
                {
                    income.MarkAppliedBySettlement(settlement.PublicId, dateTimeProvider.UtcNow);
                }
            }
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            var response = SettlementResponseMapper.Map(settlement);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Issued settlement for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileSettlementResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AnnulSettlementCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ISettlementRepository settlementRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulSettlementCommand, PersonnelFileSettlementResponse>
{
    public async Task<Result<PersonnelFileSettlementResponse>> Handle(
        AnnulSettlementCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageSettlementsAsync<PersonnelFileSettlementResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        _ = Guid.TryParse(currentUserService.UserId, out var currentUserId);
        if (personnelFile!.LinkedUserPublicId is { } linkedUser && linkedUser == currentUserId)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.SelfActionForbidden);
        }

        // Annulment loads WITHOUT the editability guard: an EMITIDA settlement is precisely what it targets.
        var settlement = await settlementRepository.GetTrackedAsync(personnelFile.Id, command.SettlementId, cancellationToken);
        if (settlement is null || !settlement.IsActive)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.NotFound);
        }

        if (settlement.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileSettlementResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (settlement.Kind != SettlementKind.Liquidacion
            || settlement.StatusCode is not (SettlementStatuses.Borrador or SettlementStatuses.Emitida))
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.StateRuleViolation);
        }

        if (settlement.StatusCode == SettlementStatuses.Emitida && string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<PersonnelFileSettlementResponse>.Failure(SettlementErrors.AnnulReasonRequired);
        }

        settlement.Annul(currentUserId, dateTimeProvider.UtcNow, command.Reason);

        // REQ-005 §3.5: reopen exactly the recurring incomes this settlement finalized (symmetric with the issue hook).
        var cyclicToReopen = await employeeRepository.GetRecurringIncomesClosedBySettlementAsync(
            personnelFile.Id, settlement.PublicId, cancellationToken);
        foreach (var income in cyclicToReopen)
        {
            income.ReopenFromSettlement(settlement.PublicId, dateTimeProvider.UtcNow);
        }

        // REQ-006 §3.5: reopen exactly the one-time incomes this settlement applied (symmetric with the issue hook).
        var oneTimeToReopen = await employeeRepository.GetOneTimeIncomesAppliedBySettlementAsync(
            personnelFile.Id, settlement.PublicId, cancellationToken);
        foreach (var income in oneTimeToReopen)
        {
            income.ReopenFromSettlement(settlement.PublicId, dateTimeProvider.UtcNow);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            var response = SettlementResponseMapper.Map(settlement);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Annulled settlement for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileSettlementResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetSettlementQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ISettlementRepository settlementRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetSettlementQuery, PersonnelFileSettlementResponse?>
{
    public async Task<Result<PersonnelFileSettlementResponse?>> Handle(
        GetSettlementQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForViewSettlementsAsync<PersonnelFileSettlementResponse?>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var settlement = await settlementRepository.GetTrackedAsync(personnelFile!.Id, query.SettlementId, cancellationToken);
        if (settlement is null || !settlement.IsActive)
        {
            return Result<PersonnelFileSettlementResponse?>.Failure(SettlementErrors.NotFound);
        }

        return Result<PersonnelFileSettlementResponse?>.Success(SettlementResponseMapper.Map(settlement));
    }
}

internal sealed class GetSettlementDocumentDataQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ISettlementRepository settlementRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetSettlementDocumentDataQuery, SettlementDocumentDataResponse>
{
    public async Task<Result<SettlementDocumentDataResponse>> Handle(
        GetSettlementDocumentDataQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForViewSettlementsAsync<SettlementDocumentDataResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var settlement = await settlementRepository.GetTrackedAsync(personnelFile!.Id, query.SettlementId, cancellationToken);
        if (settlement is null || !settlement.IsActive)
        {
            return Result<SettlementDocumentDataResponse>.Failure(SettlementErrors.NotFound);
        }

        return Result<SettlementDocumentDataResponse>.Success(
            new SettlementDocumentDataResponse(SettlementResponseMapper.Map(settlement), personnelFile.FullName));
    }
}

internal sealed class GetSettlementsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ISettlementRepository settlementRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetSettlementsQuery, IReadOnlyCollection<PersonnelFileSettlementResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileSettlementResponse>>> Handle(
        GetSettlementsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForViewSettlementsAsync<IReadOnlyCollection<PersonnelFileSettlementResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var settlements = await settlementRepository.GetByFileAsync(personnelFile!.Id, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileSettlementResponse>>.Success(
            settlements.Select(settlement => SettlementResponseMapper.Map(settlement)).ToArray());
    }
}
