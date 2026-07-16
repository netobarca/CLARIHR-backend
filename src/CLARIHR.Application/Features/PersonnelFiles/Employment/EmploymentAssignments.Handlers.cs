using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Resolves the cross-feature <see cref="EmploymentAssignmentRules.PositionSlotFacts"/> for an
/// employment-assignment command: it loads the referenced <c>PositionSlot</c> and counts the active
/// assignments whose vigencia overlaps the candidate's window (capacity-by-vigencia, RF-005).
/// </summary>
internal static class EmploymentAssignmentSlotResolver
{
    public static async Task<EmploymentAssignmentRules.PositionSlotFacts?> ResolveAsync(
        IPositionSlotRepository positionSlotRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        Guid tenantId,
        Guid? positionSlotPublicId,
        DateTime startDate,
        DateTime? endDate,
        bool isActive,
        Guid? excludeAssignmentPublicId,
        CancellationToken cancellationToken)
    {
        // Only an active assignment that references a slot occupies capacity (P-06).
        if (!isActive || positionSlotPublicId is not { } slotId)
        {
            return null;
        }

        var slot = await positionSlotRepository.GetByIdAsync(slotId, cancellationToken);
        if (slot is null)
        {
            // A referenced-but-missing slot is mapped to NOT_FOUND by EmploymentAssignmentRules.Evaluate.
            return null;
        }

        var overlapping = await employeeRepository.CountOverlappingActiveAssignmentsForSlotAsync(
            tenantId, slotId, startDate, endDate, excludeAssignmentPublicId, cancellationToken);

        return new EmploymentAssignmentRules.PositionSlotFacts(
            Exists: true,
            slot.Status,
            slot.EffectiveFromUtc,
            slot.EffectiveToUtc,
            slot.MaxEmployees,
            overlapping);
    }
}

/// <summary>
/// Shared multi-position validation for the Update/Patch handlers: it loads the employee's assignments,
/// resolves the target slot facts, runs <see cref="EmploymentAssignmentRules.Evaluate"/> (slot existence,
/// assignability, capacity-by-vigencia, same-slot dedup/overlap, single active primary) and, for edits,
/// enforces that an employee with active plazas always keeps an active primary (RF-002/RF-008). Returns the
/// other active primaries that must be demoted when the candidate becomes the active primary (P-03).
/// </summary>
internal static class EmploymentAssignmentCommandSupport
{
    public static async Task<Result<EmploymentAssignmentRules.Evaluation>> ValidateAsync(
        IPositionSlotRepository positionSlotRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        Guid personnelFilePublicId,
        Guid tenantId,
        EmploymentAssignmentRules.Candidate candidate,
        bool enforcePrimaryRetained,
        CancellationToken cancellationToken)
    {
        var all = (await employeeRepository.GetEmploymentAssignmentsAsync(personnelFilePublicId, cancellationToken))
            .Select(assignment => new EmploymentAssignmentRules.ExistingAssignment(
                assignment.Id,
                assignment.PositionSlotId,
                assignment.StartDate,
                assignment.EndDate,
                assignment.IsPrimary,
                assignment.IsActive))
            .ToArray();

        var slotFacts = await EmploymentAssignmentSlotResolver.ResolveAsync(
            positionSlotRepository,
            employeeRepository,
            tenantId,
            candidate.PositionSlotPublicId,
            candidate.StartDate,
            candidate.EndDate,
            candidate.IsActive,
            excludeAssignmentPublicId: candidate.PublicId,
            cancellationToken);

        var evaluation = EmploymentAssignmentRules.Evaluate(candidate, all, slotFacts);
        if (evaluation.IsFailure)
        {
            return evaluation;
        }

        if (enforcePrimaryRetained)
        {
            var others = all.Where(assignment => assignment.PublicId != candidate.PublicId).ToArray();
            var anyActiveRemains = candidate.IsActive || others.Any(assignment => assignment.IsActive);
            var anyActivePrimaryRemains = candidate is { IsActive: true, IsPrimary: true }
                || others.Any(assignment => assignment is { IsActive: true, IsPrimary: true });
            if (anyActiveRemains && !anyActivePrimaryRemains)
            {
                return Result<EmploymentAssignmentRules.Evaluation>.Failure(EmploymentAssignmentErrors.PrimaryRequired);
            }
        }

        return evaluation;
    }

    /// <summary>
    /// Validates the plaza's forma de pago: the payment method code (when set) must be an active
    /// <c>PaymentMethod</c> catalog code, and the bank account (when set) must be one of the employee's
    /// own configured bank accounts. Returns the validation error, or null when valid / not set.
    /// </summary>
    public static async Task<Error?> ValidatePaymentFieldsAsync(
        IPersonnelFileRepository personnelFileRepository,
        Guid tenantId,
        Guid personnelFilePublicId,
        string? paymentMethodCode,
        Guid? paymentBankAccountPublicId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(paymentMethodCode)
            && !await personnelFileRepository.CatalogCodeIsActiveAsync(
                tenantId, PersonnelCurriculumCatalogCategories.PaymentMethod, paymentMethodCode, cancellationToken))
        {
            return ErrorCatalog.Validation(new Dictionary<string, string[]> { ["paymentMethodCode"] = ["Payment method code is not valid."] });
        }

        if (paymentBankAccountPublicId is { } bankAccountId)
        {
            var bankAccountIds = (await personnelFileRepository.GetBankAccountIdsAsync(personnelFilePublicId, cancellationToken)).ToHashSet();
            if (!bankAccountIds.Contains(bankAccountId))
            {
                return ErrorCatalog.Validation(new Dictionary<string, string[]> { ["paymentBankAccountPublicId"] = ["Bank account does not exist in this personnel file."] });
            }
        }

        return null;
    }

    /// <summary>
    /// Validates the plaza's payroll (planilla) type: when <paramref name="payrollTypeCode"/> is supplied it must
    /// resolve to an ACTIVE item of the country-scoped <c>payroll-types</c> catalog (REQ-004, aclaración №3).
    /// Returns <see cref="EmploymentAssignmentErrors.PayrollTypeCodeInvalid"/> (422) when invalid, or null when
    /// valid / not set. Scope is <c>payrollTypeCode</c> only — other codes (e.g. <c>contractTypeCode</c>) keep
    /// their existing length-only validation.
    /// </summary>
    public static async Task<Error?> ValidatePayrollTypeCodeAsync(
        IPersonnelFileRepository personnelFileRepository,
        Guid tenantId,
        string? payrollTypeCode,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(payrollTypeCode)
            && !await personnelFileRepository.CatalogCodeIsActiveAsync(
                tenantId, PersonnelCurriculumCatalogCategories.PayrollType, payrollTypeCode, cancellationToken))
        {
            return EmploymentAssignmentErrors.PayrollTypeCodeInvalid;
        }

        return null;
    }

    /// <summary>
    /// Validates the plaza's workday: when <paramref name="workdayCode"/> is supplied it must resolve to an
    /// ACTIVE work schedule of the company's master (REQ-012 D-06 — the code IS the link, no FK/snapshot;
    /// free text died with the M3 cleanup). Returns
    /// <see cref="EmploymentAssignmentErrors.WorkdayCodeInvalid"/> (422) when invalid, or null when valid /
    /// not set.
    /// </summary>
    public static async Task<Error?> ValidateWorkdayCodeAsync(
        IWorkScheduleRepository workScheduleRepository,
        Guid tenantId,
        string? workdayCode,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(workdayCode)
            && !await workScheduleRepository.ActiveCodeExistsAsync(
                tenantId, workdayCode.Trim().ToUpperInvariant(), cancellationToken))
        {
            return EmploymentAssignmentErrors.WorkdayCodeInvalid;
        }

        return null;
    }
}

internal sealed class AddPersonnelFileEmploymentAssignmentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionSlotRepository positionSlotRepository,
    IWorkScheduleRepository workScheduleRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileEmploymentAssignmentCommand, PersonnelFileEmploymentAssignmentResponse>
{
    public async Task<Result<PersonnelFileEmploymentAssignmentResponse>> Handle(
        AddPersonnelFileEmploymentAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileEmploymentAssignmentResponse>(
            command.PersonnelFileId,
            Guid.Empty,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        // Assigned positions are manageable for an Employee record in ANY lifecycle state (Draft included): a
        // plaza must be addable BEFORE finalizing (the slot is the finalize prerequisite). Only the record type
        // is gated — a Candidate record cannot hold employment assignments.
        if (personnelFile!.RecordType != PersonnelFileRecordType.Employee)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var item = command.Item;
        if (item.PositionSlotId is null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(EmploymentAssignmentErrors.PositionSlotRequired);
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
            personnelFile.TenantId, PersonnelCurriculumCatalogCategories.AssignmentType, item.AssignmentTypeCode, cancellationToken))
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(EmploymentAssignmentErrors.TypeCodeInvalid);
        }

        var payrollTypeError = await EmploymentAssignmentCommandSupport.ValidatePayrollTypeCodeAsync(
            personnelFileRepository, personnelFile.TenantId, item.PayrollTypeCode, cancellationToken);
        if (payrollTypeError is not null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(payrollTypeError);
        }

        var workdayError = await EmploymentAssignmentCommandSupport.ValidateWorkdayCodeAsync(
            workScheduleRepository, personnelFile.TenantId, item.WorkdayCode, cancellationToken);
        if (workdayError is not null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(workdayError);
        }

        var bankAccountError = await EmploymentAssignmentCommandSupport.ValidatePaymentFieldsAsync(
            personnelFileRepository, personnelFile.TenantId, personnelFile.PublicId, item.PaymentMethodCode, item.PaymentBankAccountPublicId, cancellationToken);
        if (bankAccountError is not null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(bankAccountError);
        }

        var existing = (await employeeRepository.GetEmploymentAssignmentsAsync(personnelFile.PublicId, cancellationToken))
            .Select(assignment => new EmploymentAssignmentRules.ExistingAssignment(
                assignment.Id,
                assignment.PositionSlotId,
                assignment.StartDate,
                assignment.EndDate,
                assignment.IsPrimary,
                assignment.IsActive))
            .ToArray();

        // First active plaza (or when no other active primary exists) defaults to primary so the
        // employee always keeps exactly one active principal (RN-03).
        var hasActivePrimary = existing.Any(assignment => assignment is { IsActive: true, IsPrimary: true });
        var isPrimary = item.IsPrimary || (item.IsActive && !hasActivePrimary);

        var slotFacts = await EmploymentAssignmentSlotResolver.ResolveAsync(
            positionSlotRepository,
            employeeRepository,
            personnelFile.TenantId,
            item.PositionSlotId,
            item.StartDate,
            item.EndDate,
            item.IsActive,
            excludeAssignmentPublicId: null,
            cancellationToken);

        var candidate = new EmploymentAssignmentRules.Candidate(
            PublicId: null,
            item.PositionSlotId,
            item.StartDate,
            item.EndDate,
            isPrimary,
            item.IsActive);

        var evaluation = EmploymentAssignmentRules.Evaluate(candidate, existing, slotFacts);
        if (evaluation.IsFailure)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(evaluation.Error);
        }

        await employeeRepository.DemoteEmploymentAssignmentsAsync(
            personnelFile.TenantId, evaluation.Value.PrimariesToDemote, cancellationToken);

        // Derive org-unit, work-center and contract type from the position slot so the assignment can never
        // contradict its plaza (the frontend no longer sends these three — the slot is the single source of
        // truth; it wins whenever it resolves a value). Cost center stays client-supplied because the slot
        // resource does not expose its publicId.
        var slotDefaults = await positionSlotRepository.GetResponseByIdAsync(item.PositionSlotId.Value, cancellationToken);

        var entity = PersonnelFileEmploymentAssignment.Create(
            item.AssignmentTypeCode,
            slotDefaults?.ContractTypeCode ?? item.ContractTypeCode,
            item.WorkdayCode,
            item.PayrollTypeCode,
            item.PositionSlotId,
            slotDefaults?.OrgUnitId ?? item.OrgUnitId,
            slotDefaults?.WorkCenterId ?? item.WorkCenterId,
            item.CostCenterId,
            item.StartDate,
            item.EndDate,
            isPrimary,
            item.IsActive,
            item.Notes,
            item.PaymentMethodCode,
            item.PaymentBankAccountPublicId,
            (DayOfWeek?)item.RestDayOfWeek);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddEmploymentAssignmentAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(created => created.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Personnel file employment assignment response could not be resolved after creation.");

        // D-20: when a plaza is created, auto-suggest the statutory (de-ley) deductions (ISSS/AFP) on it,
        // pre-filled from the catalog defaults. Registered on the unit of work; saved in this transaction.
        await CompensationConceptSuggestionService.SuggestStatutoryForAssignmentAsync(
            personnelFileRepository, employeeRepository, personnelFile, entity.PublicId, entity.StartDate, cancellationToken);

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added employment assignment to {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileEmploymentAssignmentResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileEmploymentAssignmentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionSlotRepository positionSlotRepository,
    IWorkScheduleRepository workScheduleRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileEmploymentAssignmentCommand, PersonnelFileEmploymentAssignmentResponse>
{
    public async Task<Result<PersonnelFileEmploymentAssignmentResponse>> Handle(
        UpdatePersonnelFileEmploymentAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileEmploymentAssignmentResponse>(
            command.PersonnelFileId,
            Guid.Empty,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        // Assigned positions are manageable for an Employee record in ANY lifecycle state (Draft included): a
        // plaza must be addable BEFORE finalizing (the slot is the finalize prerequisite). Only the record type
        // is gated — a Candidate record cannot hold employment assignments.
        if (personnelFile!.RecordType != PersonnelFileRecordType.Employee)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetEmploymentAssignmentAsync(personnelFile.PublicId, command.EmploymentAssignmentPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
            personnelFile.TenantId, PersonnelCurriculumCatalogCategories.AssignmentType, command.Item.AssignmentTypeCode, cancellationToken))
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(EmploymentAssignmentErrors.TypeCodeInvalid);
        }

        var payrollTypeError = await EmploymentAssignmentCommandSupport.ValidatePayrollTypeCodeAsync(
            personnelFileRepository, personnelFile.TenantId, command.Item.PayrollTypeCode, cancellationToken);
        if (payrollTypeError is not null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(payrollTypeError);
        }

        var workdayError = await EmploymentAssignmentCommandSupport.ValidateWorkdayCodeAsync(
            workScheduleRepository, personnelFile.TenantId, command.Item.WorkdayCode, cancellationToken);
        if (workdayError is not null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(workdayError);
        }

        var bankAccountError = await EmploymentAssignmentCommandSupport.ValidatePaymentFieldsAsync(
            personnelFileRepository, personnelFile.TenantId, personnelFile.PublicId, command.Item.PaymentMethodCode, command.Item.PaymentBankAccountPublicId, cancellationToken);
        if (bankAccountError is not null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(bankAccountError);
        }

        // PUT preserves isActive (mutated only via PATCH), so the candidate keeps the stored active state.
        var candidate = new EmploymentAssignmentRules.Candidate(
            command.EmploymentAssignmentPublicId,
            command.Item.PositionSlotId,
            command.Item.StartDate,
            command.Item.EndDate,
            command.Item.IsPrimary,
            existing.IsActive);

        var evaluation = await EmploymentAssignmentCommandSupport.ValidateAsync(
            positionSlotRepository,
            employeeRepository,
            personnelFile.PublicId,
            personnelFile.TenantId,
            candidate,
            enforcePrimaryRetained: true,
            cancellationToken);
        if (evaluation.IsFailure)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(evaluation.Error);
        }

        await employeeRepository.DemoteEmploymentAssignmentsAsync(
            personnelFile.TenantId, evaluation.Value.PrimariesToDemote, cancellationToken);

        // Same slot-derivation as the create handler: org-unit, work-center and contract type follow the plaza
        // (the slot wins whenever it resolves a value); cost center stays client-supplied.
        var slotDefaults = command.Item.PositionSlotId is { } derivedSlotId
            ? await positionSlotRepository.GetResponseByIdAsync(derivedSlotId, cancellationToken)
            : null;

        // PUT replaces business fields only; isActive is preserved (it is mutated exclusively via PATCH).
        var response = await employeeRepository.UpdateEmploymentAssignmentAsync(
            command.EmploymentAssignmentPublicId,
            personnelFile.TenantId,
            command.Item.AssignmentTypeCode,
            slotDefaults?.ContractTypeCode ?? command.Item.ContractTypeCode,
            command.Item.WorkdayCode,
            command.Item.PayrollTypeCode,
            command.Item.PositionSlotId,
            slotDefaults?.OrgUnitId ?? command.Item.OrgUnitId,
            slotDefaults?.WorkCenterId ?? command.Item.WorkCenterId,
            command.Item.CostCenterId,
            command.Item.StartDate,
            command.Item.EndDate,
            command.Item.IsPrimary,
            command.Item.Notes,
            command.Item.PaymentMethodCode,
            command.Item.PaymentBankAccountPublicId,
            command.Item.RestDayOfWeek,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated employment assignment for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileEmploymentAssignmentResponse>.Success(response);
    }
}

internal sealed class PatchPersonnelFileEmploymentAssignmentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionSlotRepository positionSlotRepository,
    IWorkScheduleRepository workScheduleRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileEmploymentAssignmentCommand, PersonnelFileEmploymentAssignmentResponse>
{
    public async Task<Result<PersonnelFileEmploymentAssignmentResponse>> Handle(
        PatchPersonnelFileEmploymentAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileEmploymentAssignmentResponse>(
            command.PersonnelFileId,
            Guid.Empty,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        // Assigned positions are manageable for an Employee record in ANY lifecycle state (Draft included): a
        // plaza must be addable BEFORE finalizing (the slot is the finalize prerequisite). Only the record type
        // is gated — a Candidate record cannot hold employment assignments.
        if (personnelFile!.RecordType != PersonnelFileRecordType.Employee)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetEmploymentAssignmentAsync(personnelFile.PublicId, command.EmploymentAssignmentPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFileEmploymentAssignmentPatchState.From(existing);
        var applyResult = PersonnelFileEmploymentAssignmentPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileEmploymentAssignmentPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Success(existing);
        }

        if (state.PositionSlotId is null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(EmploymentAssignmentErrors.PositionSlotRequired);
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
            personnelFile.TenantId, PersonnelCurriculumCatalogCategories.AssignmentType, state.AssignmentTypeCode, cancellationToken))
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(EmploymentAssignmentErrors.TypeCodeInvalid);
        }

        var payrollTypeError = await EmploymentAssignmentCommandSupport.ValidatePayrollTypeCodeAsync(
            personnelFileRepository, personnelFile.TenantId, state.PayrollTypeCode, cancellationToken);
        if (payrollTypeError is not null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(payrollTypeError);
        }

        var workdayError = await EmploymentAssignmentCommandSupport.ValidateWorkdayCodeAsync(
            workScheduleRepository, personnelFile.TenantId, state.WorkdayCode, cancellationToken);
        if (workdayError is not null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(workdayError);
        }

        var bankAccountError = await EmploymentAssignmentCommandSupport.ValidatePaymentFieldsAsync(
            personnelFileRepository, personnelFile.TenantId, personnelFile.PublicId, state.PaymentMethodCode, state.PaymentBankAccountPublicId, cancellationToken);
        if (bankAccountError is not null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(bankAccountError);
        }

        var candidate = new EmploymentAssignmentRules.Candidate(
            command.EmploymentAssignmentPublicId,
            state.PositionSlotId,
            state.StartDate,
            state.EndDate,
            state.IsPrimary,
            state.IsActive);

        var evaluation = await EmploymentAssignmentCommandSupport.ValidateAsync(
            positionSlotRepository,
            employeeRepository,
            personnelFile.PublicId,
            personnelFile.TenantId,
            candidate,
            enforcePrimaryRetained: true,
            cancellationToken);
        if (evaluation.IsFailure)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(evaluation.Error);
        }

        await employeeRepository.DemoteEmploymentAssignmentsAsync(
            personnelFile.TenantId, evaluation.Value.PrimariesToDemote, cancellationToken);

        var input = state.ToInput();
        var response = await employeeRepository.PatchEmploymentAssignmentAsync(
            command.EmploymentAssignmentPublicId,
            personnelFile.TenantId,
            input.AssignmentTypeCode,
            input.ContractTypeCode,
            input.WorkdayCode,
            input.PayrollTypeCode,
            input.PositionSlotId,
            input.OrgUnitId,
            input.WorkCenterId,
            input.CostCenterId,
            input.StartDate,
            input.EndDate,
            input.IsPrimary,
            input.Notes,
            input.PaymentMethodCode,
            input.PaymentBankAccountPublicId,
            input.RestDayOfWeek,
            input.IsActive,
            state.IsActiveMutated,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileEmploymentAssignmentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched employment assignment for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileEmploymentAssignmentResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileEmploymentAssignmentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileEmploymentAssignmentCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileEmploymentAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId,
            Guid.Empty,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        // Removing an assigned position is allowed for an Employee record in any lifecycle state (Draft included);
        // see the create handler for the rationale. Only a Candidate record is blocked.
        if (personnelFile!.RecordType != PersonnelFileRecordType.Employee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetEmploymentAssignmentAsync(personnelFile.PublicId, command.EmploymentAssignmentPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // RF-002/RF-008: deleting the only active primary while other active plazas remain would leave the
        // employee with active assignments but no primary — block it (designate another primary first, P-04).
        if (existing is { IsActive: true, IsPrimary: true })
        {
            var others = (await employeeRepository.GetEmploymentAssignmentsAsync(personnelFile.PublicId, cancellationToken))
                .Where(assignment => assignment.Id != existing.Id)
                .ToArray();
            if (others.Any(assignment => assignment.IsActive)
                && !others.Any(assignment => assignment is { IsActive: true, IsPrimary: true }))
            {
                return Result<PersonnelFileParentConcurrencyResult>.Failure(EmploymentAssignmentErrors.PrimaryRequired);
            }
        }

        var removed = await employeeRepository.DeleteEmploymentAssignmentAsync(command.EmploymentAssignmentPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deleted employment assignment for {personnelFile.FullName}.", null, cancellationToken);
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

internal sealed class GetPersonnelFileEmploymentAssignmentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEmploymentAssignmentsQuery, IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>> Handle(
        GetPersonnelFileEmploymentAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        // Reading assigned positions is allowed in ANY state (Draft included): listing plazas is a query and a
        // Draft file simply returns its (possibly empty) list instead of a 422 — the section must be readable
        // before finalizing. Auth/not-found/tenant are still enforced by LoadForReadAsync.
        var (failure, personnelFile) = await LoadForReadAsync<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetEmploymentAssignmentsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileEmploymentAssignmentByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEmploymentAssignmentByIdQuery, PersonnelFileEmploymentAssignmentResponse>
{
    public async Task<Result<PersonnelFileEmploymentAssignmentResponse>> Handle(
        GetPersonnelFileEmploymentAssignmentByIdQuery query,
        CancellationToken cancellationToken)
    {
        // Reading a single assigned position is allowed in any state (Draft included); see the list handler.
        var (failure, personnelFile) = await LoadForReadAsync<PersonnelFileEmploymentAssignmentResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetEmploymentAssignmentAsync(personnelFile!.PublicId, query.EmploymentAssignmentPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileEmploymentAssignmentResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileEmploymentAssignmentResponse>.Success(response);
    }
}

internal static class PersonnelFileEmploymentAssignmentPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFileEmploymentAssignmentPatchOperation> operations, PersonnelFileEmploymentAssignmentPatchState state)
    {
        foreach (var operation in operations)
        {
            var op = operation.Op.Trim();
            if (!PersonnelFileTalentPatch.SupportedOperations.Contains(op))
            {
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, $"Unsupported JSON Patch operation '{operation.Op}'.");
            }

            var segments = PersonnelFileTalentPatch.ParsePath(operation.Path);
            if (segments.Length != 1)
            {
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only root employment assignment properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (PersonnelFilePatchValueException exception)
            {
                return PersonnelFileTalentPatch.ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(PersonnelFileEmploymentAssignmentPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.AssignmentTypeCode))
        {
            errors["assignmentTypeCode"] = ["AssignmentTypeCode is required."];
        }

        // PATCH bypasses the FluentValidation input validator, so the day-of-week range (D-26) is
        // re-enforced here with the same message the PUT/POST validator emits.
        if (state.RestDayOfWeek is < 0 or > 6)
        {
            errors["restDayOfWeek"] = ["RestDayOfWeek must be between 0 (Sunday) and 6 (Saturday)."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileEmploymentAssignmentPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "assignmentTypeCode"))
        {
            return Mutate(state, () => state.AssignmentTypeCode = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "contractTypeCode"))
        {
            return Mutate(state, () => state.ContractTypeCode = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "workdayCode"))
        {
            return Mutate(state, () => state.WorkdayCode = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "payrollTypeCode"))
        {
            return Mutate(state, () => state.PayrollTypeCode = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "paymentMethodCode"))
        {
            return Mutate(state, () => state.PaymentMethodCode = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "paymentBankAccountPublicId"))
        {
            return Mutate(state, () => state.PaymentBankAccountPublicId = isRemove ? null : PersonnelFileTalentPatch.ReadNullableGuid(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "restDayOfWeek"))
        {
            return Mutate(state, () => state.RestDayOfWeek = isRemove ? null : PersonnelFileTalentPatch.ReadNullableInt(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "positionSlotId"))
        {
            return Mutate(state, () => state.PositionSlotId = isRemove ? null : PersonnelFileTalentPatch.ReadNullableGuid(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "orgUnitId"))
        {
            return Mutate(state, () => state.OrgUnitId = isRemove ? null : PersonnelFileTalentPatch.ReadNullableGuid(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "workCenterId"))
        {
            return Mutate(state, () => state.WorkCenterId = isRemove ? null : PersonnelFileTalentPatch.ReadNullableGuid(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "costCenterId"))
        {
            return Mutate(state, () => state.CostCenterId = isRemove ? null : PersonnelFileTalentPatch.ReadNullableGuid(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "startDate"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "StartDate cannot be removed.")
                : Mutate(state, () => state.StartDate = PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "endDate"))
        {
            return Mutate(state, () => state.EndDate = isRemove ? null : PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "isPrimary"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "IsPrimary cannot be removed.")
                : Mutate(state, () => state.IsPrimary = PersonnelFileTalentPatch.ReadRequiredBoolean(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "notes"))
        {
            return Mutate(state, () => state.Notes = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "isActive"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "IsActive cannot be removed.")
                : Mutate(state, () =>
                {
                    state.IsActive = PersonnelFileTalentPatch.ReadRequiredBoolean(value, path);
                    state.IsActiveMutated = true;
                });
        }

        return PersonnelFileTalentPatch.ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static Result Mutate(PersonnelFileEmploymentAssignmentPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}

