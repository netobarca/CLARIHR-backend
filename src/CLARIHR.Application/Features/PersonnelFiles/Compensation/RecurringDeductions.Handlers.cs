using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Maps a recurring-deduction aggregate to its API response (user ids null-safe — a non-Guid principal → null).</summary>
public static class RecurringDeductionMapping
{
    public static RecurringDeductionResponse ToResponse(PersonnelFileRecurringDeduction entity) =>
        new(
            entity.PublicId,
            entity.EffectiveDate,
            entity.Reference,
            entity.RecurringDeductionTypeCode,
            entity.ConceptTypeCode,
            entity.ConceptNameSnapshot,
            entity.FinancialInstitution,
            entity.Observations,
            entity.AssignedPositionPublicId,
            entity.InstallmentStartDate,
            entity.ExceptionMonths,
            entity.CurrencyCode,
            entity.PayrollTypeCode,
            entity.InstallmentFrequencyCode,
            entity.ApplicationFrequencyCode,
            entity.IsIndefinite,
            entity.UsesCompoundInterest,
            entity.PrincipalAmount,
            entity.InterestRatePercent,
            entity.PlannedInstallments,
            entity.PlanSegments
                .Where(segment => segment.IsActive)
                .OrderBy(segment => segment.FromInstallment)
                .Select(segment => new RecurringDeductionSegmentResponse(
                    segment.PublicId,
                    segment.FromInstallment,
                    segment.ToInstallment,
                    segment.InstallmentValue))
                .ToArray(),
            entity.PlannedInstallmentCount,
            TotalAmountOf(entity),
            entity.SettlementActionCode,
            entity.StatusCode,
            NullIfEmpty(entity.RegisteredByUserId),
            entity.DecidedByUserId,
            entity.DecidedUtc,
            entity.DecisionNote,
            entity.SuspendedUtc,
            entity.SuspensionNote,
            entity.ClosedUtc,
            entity.ClosureReason,
            entity.ClosedByUserId,
            entity.IsActive,
            entity.ConcurrencyToken,
            entity.IndebtednessOverrides
                .OrderByDescending(footprint => footprint.AcknowledgedUtc)
                .Select(footprint => new IndebtednessOverrideResponse(
                    footprint.PublicId,
                    footprint.Stage,
                    footprint.AcknowledgedByUserId,
                    footprint.AcknowledgedUtc,
                    footprint.BaseIncome,
                    footprint.MonthlyLoad,
                    footprint.NewInstallment,
                    footprint.ProjectedPercent,
                    footprint.LimitPercent,
                    footprint.LimitSource))
                .ToArray());

    /// <summary>
    /// What the employee ends up paying. Without interest the aggregate sums its own segments; WITH interest the
    /// total is principal + interest, which only the amortization calculator knows — the domain deliberately does
    /// not run it (it is a pure-rules concern), so the derivation happens here. Null for an indefinite plan.
    /// </summary>
    private static decimal? TotalAmountOf(PersonnelFileRecurringDeduction entity)
    {
        if (entity.IsIndefinite)
        {
            return null;
        }

        if (!entity.UsesCompoundInterest)
        {
            return entity.TotalPlanAmount();
        }

        var schedule = RecurringDeductionRules.BuildAmortizationSchedule(
            entity.PrincipalAmount!.Value,
            entity.InterestRatePercent!.Value,
            entity.PlannedInstallments!.Value,
            entity.InstallmentFrequencyCode);

        return RecurringDeductionRules.Round2(schedule.Sum(row => row.Amount));
    }

    private static Guid? NullIfEmpty(Guid value) => value == Guid.Empty ? null : value;
}

/// <summary>
/// Cross-aggregate validation + snapshot resolution shared by the recurring-deduction write handlers
/// (database-backed, so it lives outside the pure <c>RecurringDeductionRules</c>): the deduction type, payroll type
/// and both frequencies are active catalog codes; the concept is an active NON-STATUTORY deduction concept
/// (Nature = Egreso, RN-04) whose name is snapshotted and whose class decides whether the financial institution is
/// mandatory (P-07); the plaza resolves (D-13 — no cost center, P-08); the plan is normalized (segments or
/// amortization); the settlement action is coherent with the plan (DESCONTAR_SALDO × indefinite → 422); the
/// frequency pair is divisible (№14).
/// <para>The effective date is NOT bounded here: it may be in the FUTURE (D-04) — the credit is registered and
/// authorized, and the installment application is what enforces "the date has been reached".</para>
/// </summary>
internal sealed record RecurringDeductionResolved(
    string ConceptName,
    Guid AssignedPositionPublicId,
    RecurringDeductionPlan Plan,
    string CurrencyCode,
    // Non-null only when the caller CONFIRMED an indebtedness excess (REQ-010): the handler must then stamp the
    // audited footprint on the aggregate. Null in the overwhelmingly common case — nothing was exceeded, or the
    // company has no indebtedness parameters at all.
    IndebtednessAssessment? IndebtednessOverride = null);

internal static class RecurringDeductionWriteSupport
{
    public static async Task<Result<RecurringDeductionResolved>> ResolveAndValidateAsync(
        RecurringDeductionInput input,
        PersonnelFile personnelFile,
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        // 1) Deduction type (P-10 catalog).
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.RecurringDeductionType, input.RecurringDeductionTypeCode, cancellationToken))
        {
            return Result<RecurringDeductionResolved>.Failure(RecurringDeductionErrors.TypeInvalid);
        }

        // 2) Compensation concept — active, NON-STATUTORY deduction concept (RN-04); snapshot its name.
        var concept = await personnelFileRepository.GetActiveDeductionConceptAsync(
            personnelFile.TenantId, input.ConceptTypeCode, cancellationToken);
        if (concept is null || string.IsNullOrWhiteSpace(concept.Name))
        {
            return Result<RecurringDeductionResolved>.Failure(RecurringDeductionErrors.ConceptInvalid);
        }

        // 3) The financial institution is mandatory for EXTERNAL deductions (P-07): a bank loan, a procuraduría or
        // a cooperativa is owed to a third party, and the payroll input must name it.
        if (concept.DeductionClass == DeductionClass.Externo && string.IsNullOrWhiteSpace(input.FinancialInstitution))
        {
            return Result<RecurringDeductionResolved>.Failure(RecurringDeductionErrors.FinancialInstitutionRequired);
        }

        // 4) Payroll type (REQ-004 catalog).
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.PayrollType, input.PayrollTypeCode, cancellationToken))
        {
            return Result<RecurringDeductionResolved>.Failure(RecurringDeductionErrors.PayrollTypeInvalid);
        }

        // 5) Both frequencies (PAY_PERIOD_CATALOG) — the installments are DUE at one cadence and APPLIED at another.
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.PayPeriod, input.InstallmentFrequencyCode, cancellationToken))
        {
            return Result<RecurringDeductionResolved>.Failure(RecurringDeductionErrors.FrequencyInvalid);
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.PayPeriod, input.ApplicationFrequencyCode, cancellationToken))
        {
            return Result<RecurringDeductionResolved>.Failure(RecurringDeductionErrors.FrequencyInvalid);
        }

        // 6) The application cadence must divide the installment cadence (№14 / P-06).
        var frequencyRule = RecurringDeductionRules.ValidateFrequencyPair(input.InstallmentFrequencyCode, input.ApplicationFrequencyCode);
        if (!frequencyRule.IsValid)
        {
            return Result<RecurringDeductionResolved>.Failure(
                new Error(frequencyRule.ErrorCode!, "The application frequency is not coherent with the installment frequency.", ErrorType.UnprocessableEntity));
        }

        // 7) Plaza (D-13): default the principal plaza. No cost center is involved (P-08).
        var plaza = await employeeRepository.ResolveRecurringDeductionPlazaAsync(
            personnelFile.Id, input.AssignedPositionPublicId, cancellationToken);
        if (!plaza.Found)
        {
            return Result<RecurringDeductionResolved>.Failure(RecurringDeductionErrors.AssignedPositionInvalid);
        }

        // 8) Plan coherence — segments (plain) or amortization (compound interest); the code is bilingual (PR-2).
        var segments = input.Segments?
            .Select(segment => new RecurringDeductionSegment(segment.FromInstallment, segment.ToInstallment, segment.InstallmentValue))
            .ToList() ?? [];

        var normalization = RecurringDeductionRules.NormalizePlan(
            segments,
            input.IsIndefinite,
            input.UsesCompoundInterest,
            input.PrincipalAmount,
            input.InterestRatePercent,
            input.PlannedInstallments,
            input.InstallmentFrequencyCode);
        if (!normalization.IsValid || normalization.Plan is null)
        {
            return Result<RecurringDeductionResolved>.Failure(
                new Error(normalization.ErrorCode!, "The recurring-deduction plan is not coherent.", ErrorType.UnprocessableEntity));
        }

        // 9) Settlement action coherence (D-12): DESCONTAR_SALDO is meaningless for an indefinite plan.
        var settlementRule = RecurringDeductionRules.ValidateSettlementAction(input.SettlementActionCode, input.IsIndefinite);
        if (!settlementRule.IsValid)
        {
            return Result<RecurringDeductionResolved>.Failure(
                new Error(settlementRule.ErrorCode!, "The settlement action is not coherent with the plan.", ErrorType.UnprocessableEntity));
        }

        // 10) REQ-010 — the debt-capacity ("endeudamiento") check. It lands HERE, where the seam was left: the plan
        // is normalized (so the installment is known) and nothing has been persisted yet. With no parameters
        // configured this resolves to "not exceeded" and the whole module is invisible.
        var indebtedness = await EvaluateIndebtednessAsync(
            input, personnelFile, normalization.Plan, employeeRepository, cancellationToken);
        if (indebtedness.IsExceeded && !input.AcknowledgeIndebtednessExceeded)
        {
            // A RETRYABLE 422: re-sending with acknowledgeIndebtednessExceeded = true registers the credit. The
            // breakdown rides in the error's extensions because the localizer OVERWRITES `detail`.
            return Result<RecurringDeductionResolved>.Failure(
                IndebtednessErrors.LimitExceeded with { Extensions = IndebtednessRules.ToProblemExtensions(indebtedness) });
        }

        return Result<RecurringDeductionResolved>.Success(new RecurringDeductionResolved(
            concept.Name,
            plaza.AssignedPositionPublicId,
            normalization.Plan,
            input.CurrencyCode.Trim().ToUpperInvariant(),
            indebtedness.IsExceeded ? indebtedness : null));
    }

    /// <summary>
    /// Projects the employee's indebtedness WITH this credit added (REQ-010 RF-021). The candidate's installment is
    /// the FIRST one of its plan — that is the amount that starts consuming capacity today.
    /// </summary>
    public static async Task<IndebtednessAssessment> EvaluateIndebtednessAsync(
        RecurringDeductionInput input,
        PersonnelFile personnelFile,
        RecurringDeductionPlan plan,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        var snapshot = await employeeRepository.GetIndebtednessSnapshotAsync(
            personnelFile.TenantId, personnelFile.Id, cancellationToken);

        return IndebtednessRules.Assess(
            IndebtednessRules.ComputeBaseIncome(snapshot.BaseItems),
            snapshot.LoadItems,
            RecurringDeductionRules.InstallmentAmountFor(1, plan),
            input.InstallmentFrequencyCode,
            snapshot.GlobalLimitPercent,
            snapshot.LimitsByType,
            input.RecurringDeductionTypeCode);
    }

    /// <summary>
    /// The same projection as at registration, but driven by the PERSISTED credit (REQ-010 P-14). The credit being
    /// authorized is EN_REVISION, so it is not part of the load yet — adding its installment double-counts nothing.
    /// </summary>
    public static async Task<IndebtednessAssessment> EvaluateIndebtednessAtAuthorizationAsync(
        PersonnelFileRecurringDeduction entity,
        PersonnelFile personnelFile,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        var snapshot = await employeeRepository.GetIndebtednessSnapshotAsync(
            personnelFile.TenantId, personnelFile.Id, cancellationToken);

        var plan = new RecurringDeductionPlan(
            entity.PlanSegments
                .Where(segment => segment.IsActive)
                .OrderBy(segment => segment.FromInstallment)
                .Select(segment => new RecurringDeductionSegment(
                    segment.FromInstallment, segment.ToInstallment, segment.InstallmentValue))
                .ToList(),
            entity.PlannedInstallmentCount,
            entity.TotalPlanAmount(),
            entity.IsIndefinite,
            entity.UsesCompoundInterest,
            entity.PrincipalAmount,
            entity.InterestRatePercent,
            entity.InstallmentFrequencyCode);

        return IndebtednessRules.Assess(
            IndebtednessRules.ComputeBaseIncome(snapshot.BaseItems),
            snapshot.LoadItems,
            RecurringDeductionRules.InstallmentAmountFor(1, plan),
            entity.InstallmentFrequencyCode,
            snapshot.GlobalLimitPercent,
            snapshot.LimitsByType,
            entity.RecurringDeductionTypeCode);
    }

    /// <summary>Stamps the audited footprint of a confirmed indebtedness override (REQ-010 P-14).</summary>
    public static void StampOverride(
        PersonnelFileRecurringDeduction entity,
        string stage,
        Guid acknowledgedByUserId,
        DateTime acknowledgedUtc,
        IndebtednessAssessment assessment) =>
        entity.StampIndebtednessOverride(
            stage,
            acknowledgedByUserId,
            acknowledgedUtc,
            assessment.BaseIncome,
            assessment.CurrentLoad,
            assessment.NewInstallment,
            assessment.ProjectedPercent,
            assessment.LimitPercent!.Value,
            assessment.LimitSource!);

    /// <summary>Applies the resolved plan to a (new or edited) aggregate: the segments are replace-all (№12).</summary>
    public static void ApplyPlanSegments(PersonnelFileRecurringDeduction entity, RecurringDeductionPlan plan) =>
        entity.ReplacePlanSegments(plan.Segments.Select(segment =>
            (segment.FromInstallment, segment.ToInstallment, segment.InstallmentValue)));
}

/// <summary>
/// Separation-of-duties checks for the authorizer actions (D-05, DOUBLE anti-self): neither the SUBJECT employee
/// (the file's linked login) nor the REGISTRAR (who created the credit) may decide or revoke it.
/// </summary>
internal static class RecurringDeductionAuthorizerGuards
{
    public static Error? Check(PersonnelFile personnelFile, PersonnelFileRecurringDeduction deduction, Guid actingUserId)
    {
        if (actingUserId != Guid.Empty
            && ((personnelFile.LinkedUserPublicId is { } subjectUserId && subjectUserId == actingUserId)
                || deduction.RegisteredByUserId == actingUserId))
        {
            return RecurringDeductionErrors.SelfApprovalForbidden;
        }

        return null;
    }
}

// ── CRUD (Manage) ───────────────────────────────────────────────────────────────────────────────────

internal sealed class AddPersonnelFileRecurringDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileRecurringDeductionCommand, RecurringDeductionResponse>
{
    public async Task<Result<RecurringDeductionResponse>> Handle(
        AddPersonnelFileRecurringDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringDeductionsAsync<RecurringDeductionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsRecurringIncomeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var resolution = await RecurringDeductionWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, employeeRepository, cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<RecurringDeductionResponse>.Failure(resolution.Error);
        }

        var resolved = resolution.Value;
        _ = Guid.TryParse(currentUserService.UserId, out var registeredByUserId);

        var entity = PersonnelFileRecurringDeduction.Create(
            command.Item.EffectiveDate,
            command.Item.Reference,
            command.Item.RecurringDeductionTypeCode,
            command.Item.ConceptTypeCode,
            resolved.ConceptName,
            command.Item.FinancialInstitution,
            command.Item.Observations,
            resolved.AssignedPositionPublicId,
            command.Item.InstallmentStartDate,
            command.Item.ExceptionMonths,
            resolved.CurrencyCode,
            command.Item.PayrollTypeCode,
            command.Item.InstallmentFrequencyCode,
            command.Item.ApplicationFrequencyCode,
            command.Item.IsIndefinite,
            command.Item.SettlementActionCode,
            command.Item.UsesCompoundInterest,
            command.Item.PrincipalAmount,
            command.Item.InterestRatePercent,
            command.Item.PlannedInstallments,
            registeredByUserId);
        RecurringDeductionWriteSupport.ApplyPlanSegments(entity, resolved.Plan);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);
        foreach (var segment in entity.PlanSegments)
        {
            segment.SetTenantId(personnelFile.TenantId);
        }

        // REQ-010: the registrar was warned and confirmed. Stamp WHO decided it and WITH WHICH figures — the
        // parameters and the employee's other credits will move, the accountability trail must not.
        if (resolved.IndebtednessOverride is { } overrideAtCreation)
        {
            RecurringDeductionWriteSupport.StampOverride(
                entity, IndebtednessOverrideStages.Creacion, registeredByUserId, dateTimeProvider.UtcNow, overrideAtCreation);
        }

        var all = await employeeRepository.AddRecurringDeductionAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Recurring-deduction response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Registered recurring deduction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<RecurringDeductionResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileRecurringDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileRecurringDeductionCommand, RecurringDeductionResponse>
{
    public async Task<Result<RecurringDeductionResponse>> Handle(
        UpdatePersonnelFileRecurringDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringDeductionsAsync<RecurringDeductionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsRecurringIncomeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var entity = await employeeRepository.GetRecurringDeductionEntityAsync(
            personnelFile.PublicId, command.RecurringDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Only an EN_REVISION credit can be edited; pre-check to avoid a domain exception → 500.
        if (entity.StatusCode != RecurringDeductionStatuses.EnRevision)
        {
            return Result<RecurringDeductionResponse>.Failure(RecurringDeductionErrors.StateRuleViolation);
        }

        var resolution = await RecurringDeductionWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, employeeRepository, cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<RecurringDeductionResponse>.Failure(resolution.Error);
        }

        var resolved = resolution.Value;
        entity.Update(
            command.Item.EffectiveDate,
            command.Item.Reference,
            command.Item.RecurringDeductionTypeCode,
            command.Item.ConceptTypeCode,
            resolved.ConceptName,
            command.Item.FinancialInstitution,
            command.Item.Observations,
            resolved.AssignedPositionPublicId,
            command.Item.InstallmentStartDate,
            command.Item.ExceptionMonths,
            resolved.CurrencyCode,
            command.Item.PayrollTypeCode,
            command.Item.InstallmentFrequencyCode,
            command.Item.ApplicationFrequencyCode,
            command.Item.IsIndefinite,
            command.Item.SettlementActionCode,
            command.Item.UsesCompoundInterest,
            command.Item.PrincipalAmount,
            command.Item.InterestRatePercent,
            command.Item.PlannedInstallments);

        // Replace-all: the previous segments are dropped and the new plan is written (only EN_REVISION, №12).
        RecurringDeductionWriteSupport.ApplyPlanSegments(entity, resolved.Plan);
        foreach (var segment in entity.PlanSegments)
        {
            segment.SetTenantId(personnelFile.TenantId);
        }

        if (resolved.IndebtednessOverride is { } overrideAtEdit)
        {
            _ = Guid.TryParse(currentUserService.UserId, out var editedByUserId);
            RecurringDeductionWriteSupport.StampOverride(
                entity, IndebtednessOverrideStages.Creacion, editedByUserId, dateTimeProvider.UtcNow, overrideAtEdit);
        }

        var response = RecurringDeductionMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated recurring deduction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<RecurringDeductionResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileRecurringDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileRecurringDeductionCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileRecurringDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringDeductionsAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetRecurringDeductionEntityAsync(
            personnelFile!.PublicId, command.RecurringDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Only a never-authorized draft can be discarded; an authorized credit is revoked or closed.
        if (entity.StatusCode != RecurringDeductionStatuses.EnRevision)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(RecurringDeductionErrors.StateRuleViolation);
        }

        entity.Deactivate();
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deleted recurring deduction draft for {personnelFile.FullName}.", new { command.RecurringDeductionPublicId }, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileParentConcurrencyResult>.Success(
            new PersonnelFileParentConcurrencyResult(personnelFile.ConcurrencyToken));
    }
}

internal sealed class SetPersonnelFileRecurringDeductionSuspensionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<SetPersonnelFileRecurringDeductionSuspensionCommand, RecurringDeductionResponse>
{
    public async Task<Result<RecurringDeductionResponse>> Handle(
        SetPersonnelFileRecurringDeductionSuspensionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringDeductionsAsync<RecurringDeductionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetRecurringDeductionEntityAsync(
            personnelFile!.PublicId, command.RecurringDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var expected = command.Suspend ? RecurringDeductionStatuses.Vigente : RecurringDeductionStatuses.Suspendido;
        if (entity.StatusCode != expected)
        {
            return Result<RecurringDeductionResponse>.Failure(RecurringDeductionErrors.StateRuleViolation);
        }

        var now = dateTimeProvider.UtcNow;
        if (command.Suspend)
        {
            entity.Suspend(command.Note, now);
        }
        else
        {
            entity.Resume(now);
        }

        var response = RecurringDeductionMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            var verb = command.Suspend ? "Suspended" : "Resumed";
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"{verb} recurring deduction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<RecurringDeductionResponse>.Success(response);
    }
}

internal sealed class ClosePersonnelFileRecurringDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ClosePersonnelFileRecurringDeductionCommand, RecurringDeductionResponse>
{
    public async Task<Result<RecurringDeductionResponse>> Handle(
        ClosePersonnelFileRecurringDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringDeductionsAsync<RecurringDeductionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<RecurringDeductionResponse>.Failure(RecurringDeductionErrors.ClosureReasonRequired);
        }

        var entity = await employeeRepository.GetRecurringDeductionEntityAsync(
            personnelFile!.PublicId, command.RecurringDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Only an INDEFINITE VIGENTE credit is closed by hand: a finite one ends when its plan completes.
        if (entity.StatusCode != RecurringDeductionStatuses.Vigente || !entity.IsIndefinite)
        {
            return Result<RecurringDeductionResponse>.Failure(RecurringDeductionErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        entity.CloseManually(command.Reason, byUserId, dateTimeProvider.UtcNow);

        var response = RecurringDeductionMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Closed recurring deduction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<RecurringDeductionResponse>.Success(response);
    }
}

internal sealed class AnnulPersonnelFileRecurringDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulPersonnelFileRecurringDeductionCommand, RecurringDeductionResponse>
{
    public async Task<Result<RecurringDeductionResponse>> Handle(
        AnnulPersonnelFileRecurringDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringDeductionsAsync<RecurringDeductionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<RecurringDeductionResponse>.Failure(RecurringDeductionErrors.AnnulmentReasonRequired);
        }

        var entity = await employeeRepository.GetRecurringDeductionEntityAsync(
            personnelFile!.PublicId, command.RecurringDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // The HR (Manage) branch only annuls a draft: revoking an AUTHORIZED credit is the authorizer's job
        // (the dedicated AuthorizeRecurringDeductions grant + double anti-self).
        if (entity.StatusCode != RecurringDeductionStatuses.EnRevision)
        {
            return Result<RecurringDeductionResponse>.Failure(RecurringDeductionErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        entity.Annul(command.Reason, byUserId, dateTimeProvider.UtcNow);

        var response = RecurringDeductionMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Annulled recurring deduction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<RecurringDeductionResponse>.Success(response);
    }
}

// ── Resolution (Authorize — double anti-self) ────────────────────────────────────────────────────────

internal sealed class ResolvePersonnelFileRecurringDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ResolvePersonnelFileRecurringDeductionCommand, RecurringDeductionResponse>
{
    public async Task<Result<RecurringDeductionResponse>> Handle(
        ResolvePersonnelFileRecurringDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeRecurringDeductionsAsync<RecurringDeductionResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var targetStatus = command.TargetStatusCode.Trim().ToUpperInvariant();
        if (targetStatus is not (RecurringDeductionStatuses.Vigente or RecurringDeductionStatuses.Rechazado))
        {
            return Result<RecurringDeductionResponse>.Failure(RecurringDeductionErrors.StatusInvalid);
        }

        if (targetStatus == RecurringDeductionStatuses.Rechazado && string.IsNullOrWhiteSpace(command.Note))
        {
            return Result<RecurringDeductionResponse>.Failure(RecurringDeductionErrors.DecisionNoteRequired);
        }

        var entity = await employeeRepository.GetRecurringDeductionEntityAsync(
            personnelFile!.PublicId, command.RecurringDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Re-verify EN_REVISION inside the request (the decision is one-shot).
        if (entity.StatusCode != RecurringDeductionStatuses.EnRevision)
        {
            return Result<RecurringDeductionResponse>.Failure(RecurringDeductionErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);
        if (RecurringDeductionAuthorizerGuards.Check(personnelFile, entity, actingUserId) is { } selfApproval)
        {
            return Result<RecurringDeductionResponse>.Failure(selfApproval);
        }

        var now = dateTimeProvider.UtcNow;
        if (targetStatus == RecurringDeductionStatuses.Vigente)
        {
            // REQ-010 (P-14) — the SECOND check. It is not redundant with the one at registration: the employee's
            // load moves in between (other credits get authorized, some finish), so a credit that fit when it was
            // registered may no longer fit when it is decided. Rejecting needs no check — it adds no debt.
            var indebtedness = await RecurringDeductionWriteSupport.EvaluateIndebtednessAtAuthorizationAsync(
                entity, personnelFile, employeeRepository, cancellationToken);
            if (indebtedness.IsExceeded && !command.AcknowledgeIndebtednessExceeded)
            {
                return Result<RecurringDeductionResponse>.Failure(
                    IndebtednessErrors.LimitExceeded with { Extensions = IndebtednessRules.ToProblemExtensions(indebtedness) });
            }

            entity.Approve(actingUserId, now);

            if (indebtedness.IsExceeded)
            {
                RecurringDeductionWriteSupport.StampOverride(
                    entity, IndebtednessOverrideStages.Autorizacion, actingUserId, now, indebtedness);
            }
        }
        else
        {
            entity.Reject(actingUserId, now, command.Note!);
        }

        var response = RecurringDeductionMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Resolved recurring deduction ({targetStatus}) for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<RecurringDeductionResponse>.Success(response);
    }
}

internal sealed class RevokePersonnelFileRecurringDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<RevokePersonnelFileRecurringDeductionCommand, RecurringDeductionResponse>
{
    public async Task<Result<RecurringDeductionResponse>> Handle(
        RevokePersonnelFileRecurringDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeRecurringDeductionsAsync<RecurringDeductionResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<RecurringDeductionResponse>.Failure(RecurringDeductionErrors.AnnulmentReasonRequired);
        }

        var entity = await employeeRepository.GetRecurringDeductionEntityAsync(
            personnelFile!.PublicId, command.RecurringDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Revocation targets an AUTHORIZED credit (the draft branch is the HR annulment).
        if (entity.StatusCode != RecurringDeductionStatuses.Vigente)
        {
            return Result<RecurringDeductionResponse>.Failure(RecurringDeductionErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);
        if (RecurringDeductionAuthorizerGuards.Check(personnelFile, entity, actingUserId) is { } selfApproval)
        {
            return Result<RecurringDeductionResponse>.Failure(selfApproval);
        }

        entity.Annul(command.Reason, actingUserId, dateTimeProvider.UtcNow);

        var response = RecurringDeductionMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Revoked recurring deduction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<RecurringDeductionResponse>.Success(response);
    }
}

// ── Queries (View) ──────────────────────────────────────────────────────────────────────────────────

internal sealed class GetPersonnelFileRecurringDeductionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFileRecurringDeductionsQuery, IReadOnlyCollection<RecurringDeductionResponse>>
{
    public async Task<Result<IReadOnlyCollection<RecurringDeductionResponse>>> Handle(
        GetPersonnelFileRecurringDeductionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForRecurringDeductionReadAsync<IReadOnlyCollection<RecurringDeductionResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var items = await employeeRepository.GetRecurringDeductionsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<RecurringDeductionResponse>>.Success(items);
    }
}

internal sealed class GetPersonnelFileRecurringDeductionByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFileRecurringDeductionByIdQuery, RecurringDeductionResponse>
{
    public async Task<Result<RecurringDeductionResponse>> Handle(
        GetPersonnelFileRecurringDeductionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForRecurringDeductionReadAsync<RecurringDeductionResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var item = await employeeRepository.GetRecurringDeductionAsync(
            personnelFile!.PublicId, query.RecurringDeductionPublicId, cancellationToken);

        return item is null
            ? Result<RecurringDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<RecurringDeductionResponse>.Success(item);
    }
}
