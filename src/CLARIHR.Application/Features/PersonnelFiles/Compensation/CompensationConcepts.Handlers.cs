using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Shared validation for compensation-concept writes: catalog-code validity (async) + intrinsic pure
/// rules (<see cref="CompensationConceptRules"/>). The stateful base-salary rules (single active per plaza,
/// negotiated-within-range) are layered on in a later phase.
/// </summary>
internal static class CompensationConceptCommandSupport
{
    public static async Task<Result> ValidateAsync(
        IPersonnelFileRepository personnelFileRepository,
        Guid tenantId,
        CompensationConceptInput item,
        CancellationToken cancellationToken)
    {
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
            tenantId, PersonnelCurriculumCatalogCategories.CompensationConceptType, item.ConceptTypeCode, cancellationToken))
        {
            return Result.Failure(CompensationErrors.ConceptTypeCodeInvalid);
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
            tenantId, PersonnelCurriculumCatalogCategories.Currency, item.CurrencyCode, cancellationToken))
        {
            return Result.Failure(CompensationErrors.CurrencyInvalid);
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
            tenantId, PersonnelCurriculumCatalogCategories.PayPeriod, item.PayPeriodCode, cancellationToken))
        {
            return Result.Failure(CompensationErrors.PayPeriodInvalid);
        }

        if (item.CalculationType == CompensationCalculationType.Percentage
            && !string.IsNullOrWhiteSpace(item.CalculationBaseCode)
            && !await personnelFileRepository.CatalogCodeIsActiveAsync(
                tenantId, PersonnelCurriculumCatalogCategories.CalculationBase, item.CalculationBaseCode, cancellationToken))
        {
            return Result.Failure(CompensationErrors.CalculationBaseInvalid);
        }

        return CompensationConceptRules.Evaluate(new CompensationConceptRules.Candidate(
            item.Nature,
            item.DeductionClass,
            item.CalculationType,
            item.Value,
            item.CalculationBaseCode));
    }

    /// <summary>
    /// Base-salary specific rules (RF-002, R-3): only one active base salary per plaza, and the
    /// negotiated amount must fall within the plaza's salary range (from the job profile's tabulator
    /// line). Only applies to a plaza-scoped income whose concept type is the base salary.
    /// </summary>
    public static async Task<Result> ValidateBaseSalaryAsync(
        IPersonnelFileEmployeeRepository employeeRepository,
        IPositionSlotRepository positionSlotRepository,
        Guid personnelFilePublicId,
        CompensationConceptInput item,
        Guid? excludeConceptPublicId,
        CancellationToken cancellationToken)
    {
        if (item.Nature != CompensationNature.Ingreso
            || !string.Equals(item.ConceptTypeCode.Trim(), CompensationConceptRules.BaseSalaryConceptTypeCode, StringComparison.OrdinalIgnoreCase)
            || item.AssignedPositionPublicId is not { } assignedPositionPublicId)
        {
            return Result.Success();
        }

        var assignment = await employeeRepository.GetEmploymentAssignmentAsync(personnelFilePublicId, assignedPositionPublicId, cancellationToken);
        if (assignment is null)
        {
            return Result.Failure(CompensationErrors.AssignedPositionNotFound);
        }

        var concepts = await employeeRepository.GetCompensationConceptsAsync(personnelFilePublicId, cancellationToken);
        var anotherActiveBaseSalaryExists = concepts.Any(concept =>
            concept.IsActive
            && concept.Nature == CompensationNature.Ingreso
            && string.Equals(concept.ConceptTypeCode, CompensationConceptRules.BaseSalaryConceptTypeCode, StringComparison.OrdinalIgnoreCase)
            && concept.AssignedPositionPublicId == assignedPositionPublicId
            && (excludeConceptPublicId is null || concept.CompensationConceptPublicId != excludeConceptPublicId.Value));

        CompensationConceptRules.SalaryRange? range = null;
        if (item.CalculationType == CompensationCalculationType.Fixed && assignment.PositionSlotId is { } positionSlotPublicId)
        {
            var slotRange = await positionSlotRepository.GetSalaryRangeAsync(positionSlotPublicId, cancellationToken);
            if (slotRange is not null)
            {
                range = new CompensationConceptRules.SalaryRange(slotRange.MinAmount, slotRange.MaxAmount);
            }
        }

        return CompensationConceptRules.EvaluateBaseSalary(item.Value, anotherActiveBaseSalaryExists, range);
    }
}

internal sealed class AddPersonnelFileCompensationConceptCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionSlotRepository positionSlotRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileCompensationConceptCommand, PersonnelFileCompensationConceptResponse>
{
    public async Task<Result<PersonnelFileCompensationConceptResponse>> Handle(
        AddPersonnelFileCompensationConceptCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileCompensationConceptResponse>(
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

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var validation = await CompensationConceptCommandSupport.ValidateAsync(
            personnelFileRepository, personnelFile.TenantId, command.Item, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(validation.Error);
        }

        var baseSalaryValidation = await CompensationConceptCommandSupport.ValidateBaseSalaryAsync(
            employeeRepository, positionSlotRepository, personnelFile.PublicId, command.Item, excludeConceptPublicId: null, cancellationToken);
        if (baseSalaryValidation.IsFailure)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(baseSalaryValidation.Error);
        }

        var item = command.Item;
        var entity = PersonnelFileCompensationConcept.Create(
            item.AssignedPositionPublicId,
            item.Nature,
            item.ConceptTypeCode,
            item.DeductionClass,
            item.CalculationType,
            item.Value,
            item.CalculationBaseCode,
            item.EmployerRate,
            item.ContributionCap,
            item.CurrencyCode,
            item.PayPeriodCode,
            item.CounterpartyName,
            item.ExternalReference,
            item.StartDate,
            item.EndDate,
            item.IsActive,
            isSystemSuggested: false,
            item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddCompensationConceptAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(concept => concept.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Personnel file compensation concept response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added compensation concept for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileCompensationConceptResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileCompensationConceptCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionSlotRepository positionSlotRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileCompensationConceptCommand, PersonnelFileCompensationConceptResponse>
{
    public async Task<Result<PersonnelFileCompensationConceptResponse>> Handle(
        UpdatePersonnelFileCompensationConceptCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileCompensationConceptResponse>(
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

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetCompensationConceptAsync(personnelFile.PublicId, command.CompensationConceptPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var validation = await CompensationConceptCommandSupport.ValidateAsync(
            personnelFileRepository, personnelFile.TenantId, command.Item, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(validation.Error);
        }

        var baseSalaryValidation = await CompensationConceptCommandSupport.ValidateBaseSalaryAsync(
            employeeRepository, positionSlotRepository, personnelFile.PublicId, command.Item, command.CompensationConceptPublicId, cancellationToken);
        if (baseSalaryValidation.IsFailure)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(baseSalaryValidation.Error);
        }

        var item = command.Item;
        // PUT replaces business fields only; isActive is preserved (it is mutated exclusively via PATCH).
        var response = await employeeRepository.UpdateCompensationConceptAsync(
            command.CompensationConceptPublicId,
            personnelFile.TenantId,
            item.AssignedPositionPublicId,
            item.Nature,
            item.ConceptTypeCode,
            item.DeductionClass,
            item.CalculationType,
            item.Value,
            item.CalculationBaseCode,
            item.EmployerRate,
            item.ContributionCap,
            item.CurrencyCode,
            item.PayPeriodCode,
            item.CounterpartyName,
            item.ExternalReference,
            item.StartDate,
            item.EndDate,
            item.Notes,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated compensation concept for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileCompensationConceptResponse>.Success(response);
    }
}

internal sealed class PatchPersonnelFileCompensationConceptCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionSlotRepository positionSlotRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileCompensationConceptCommand, PersonnelFileCompensationConceptResponse>
{
    public async Task<Result<PersonnelFileCompensationConceptResponse>> Handle(
        PatchPersonnelFileCompensationConceptCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileCompensationConceptResponse>(
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

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetCompensationConceptAsync(personnelFile.PublicId, command.CompensationConceptPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFileCompensationConceptPatchState.From(existing);
        var applyResult = PersonnelFileCompensationConceptPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(applyResult.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Success(existing);
        }

        var input = state.ToInput();
        var validation = await CompensationConceptCommandSupport.ValidateAsync(
            personnelFileRepository, personnelFile.TenantId, input, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(validation.Error);
        }

        var baseSalaryValidation = await CompensationConceptCommandSupport.ValidateBaseSalaryAsync(
            employeeRepository, positionSlotRepository, personnelFile.PublicId, input, command.CompensationConceptPublicId, cancellationToken);
        if (baseSalaryValidation.IsFailure)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(baseSalaryValidation.Error);
        }

        var response = await employeeRepository.PatchCompensationConceptAsync(
            command.CompensationConceptPublicId,
            personnelFile.TenantId,
            input.AssignedPositionPublicId,
            input.Nature,
            input.ConceptTypeCode,
            input.DeductionClass,
            input.CalculationType,
            input.Value,
            input.CalculationBaseCode,
            input.EmployerRate,
            input.ContributionCap,
            input.CurrencyCode,
            input.PayPeriodCode,
            input.CounterpartyName,
            input.ExternalReference,
            input.StartDate,
            input.EndDate,
            input.Notes,
            input.IsActive,
            state.IsActiveMutated,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileCompensationConceptResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched compensation concept for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileCompensationConceptResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileCompensationConceptCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileCompensationConceptCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileCompensationConceptCommand command,
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

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetCompensationConceptAsync(personnelFile.PublicId, command.CompensationConceptPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var removed = await employeeRepository.DeleteCompensationConceptAsync(command.CompensationConceptPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deleted compensation concept for {personnelFile.FullName}.", null, cancellationToken);
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

internal sealed class GetPersonnelFileCompensationConceptsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileCompensationConceptsQuery, IReadOnlyCollection<PersonnelFileCompensationConceptResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileCompensationConceptResponse>>> Handle(
        GetPersonnelFileCompensationConceptsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompensationReadAsync<IReadOnlyCollection<PersonnelFileCompensationConceptResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            currentUserService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetCompensationConceptsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileCompensationConceptResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileCompensationConceptByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileCompensationConceptByIdQuery, PersonnelFileCompensationConceptResponse>
{
    public async Task<Result<PersonnelFileCompensationConceptResponse>> Handle(
        GetPersonnelFileCompensationConceptByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompensationReadAsync<PersonnelFileCompensationConceptResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            currentUserService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetCompensationConceptAsync(personnelFile!.PublicId, query.CompensationConceptPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileCompensationConceptResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileCompensationConceptResponse>.Success(response);
    }
}

internal static class PersonnelFileCompensationConceptPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFileCompensationConceptPatchOperation> operations, PersonnelFileCompensationConceptPatchState state)
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
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only root compensation concept properties can be patched.");
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

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileCompensationConceptPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "assignedPositionPublicId"))
        {
            return Mutate(state, () => state.AssignedPositionPublicId = isRemove ? null : PersonnelFileTalentPatch.ReadNullableGuid(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "nature"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "Nature cannot be removed.")
                : Mutate(state, () => state.Nature = ReadEnum<CompensationNature>(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "conceptTypeCode"))
        {
            return Mutate(state, () => state.ConceptTypeCode = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "deductionClass"))
        {
            return Mutate(state, () => state.DeductionClass = isRemove ? null : ReadEnum<DeductionClass>(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "calculationType"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "CalculationType cannot be removed.")
                : Mutate(state, () => state.CalculationType = ReadEnum<CompensationCalculationType>(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "value"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "Value cannot be removed.")
                : Mutate(state, () => state.Value = PersonnelFileTalentPatch.ReadRequiredDecimal(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "calculationBaseCode"))
        {
            return Mutate(state, () => state.CalculationBaseCode = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "employerRate"))
        {
            return Mutate(state, () => state.EmployerRate = isRemove ? null : PersonnelFileTalentPatch.ReadNullableDecimal(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "contributionCap"))
        {
            return Mutate(state, () => state.ContributionCap = isRemove ? null : PersonnelFileTalentPatch.ReadNullableDecimal(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "currencyCode"))
        {
            return Mutate(state, () => state.CurrencyCode = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "payPeriodCode"))
        {
            return Mutate(state, () => state.PayPeriodCode = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "counterpartyName"))
        {
            return Mutate(state, () => state.CounterpartyName = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "externalReference"))
        {
            return Mutate(state, () => state.ExternalReference = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
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

    private static TEnum ReadEnum<TEnum>(JsonElement? value, string path)
        where TEnum : struct, Enum
    {
        var raw = PersonnelFileTalentPatch.ReadRequiredString(value, path);
        if (!Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new PersonnelFilePatchValueException(path, $"'{raw}' is not a valid value for '{path}'.");
        }

        return parsed;
    }

    private static Result Mutate(PersonnelFileCompensationConceptPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}
