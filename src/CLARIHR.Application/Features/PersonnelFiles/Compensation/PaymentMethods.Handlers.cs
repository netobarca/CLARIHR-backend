using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
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

internal sealed class AddPersonnelFilePaymentMethodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFilePaymentMethodCommand, PersonnelFilePaymentMethodResponse>
{
    public async Task<Result<PersonnelFilePaymentMethodResponse>> Handle(
        AddPersonnelFilePaymentMethodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFilePaymentMethodResponse>(
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
            return Result<PersonnelFilePaymentMethodResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (command.Item.BankAccountPublicId.HasValue)
        {
            var bankAccountIds = (await personnelFileRepository.GetBankAccountIdsAsync(personnelFile.PublicId, cancellationToken)).ToHashSet();
            if (!bankAccountIds.Contains(command.Item.BankAccountPublicId.Value))
            {
                return Result<PersonnelFilePaymentMethodResponse>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]> { ["bankAccountPublicId"] = ["Bank account does not exist in this personnel file."] }));
            }
        }

        var entity = PersonnelFilePaymentMethod.Create(
            command.Item.PaymentMethodCode,
            command.Item.BankAccountPublicId,
            command.Item.IsPrimary,
            command.Item.IsActive,
            command.Item.EffectiveFromUtc,
            command.Item.EffectiveToUtc,
            command.Item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddPaymentMethodAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Personnel file payment method response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added payment method for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFilePaymentMethodResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFilePaymentMethodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFilePaymentMethodCommand, PersonnelFilePaymentMethodResponse>
{
    public async Task<Result<PersonnelFilePaymentMethodResponse>> Handle(
        UpdatePersonnelFilePaymentMethodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFilePaymentMethodResponse>(
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
            return Result<PersonnelFilePaymentMethodResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetPaymentMethodAsync(personnelFile.PublicId, command.PaymentMethodPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFilePaymentMethodResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFilePaymentMethodResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (command.Item.BankAccountPublicId.HasValue)
        {
            var bankAccountIds = (await personnelFileRepository.GetBankAccountIdsAsync(personnelFile.PublicId, cancellationToken)).ToHashSet();
            if (!bankAccountIds.Contains(command.Item.BankAccountPublicId.Value))
            {
                return Result<PersonnelFilePaymentMethodResponse>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]> { ["bankAccountPublicId"] = ["Bank account does not exist in this personnel file."] }));
            }
        }

        // PUT replaces business fields only; isActive is preserved (it is mutated exclusively via PATCH).
        var response = await employeeRepository.UpdatePaymentMethodAsync(
            command.PaymentMethodPublicId,
            personnelFile.TenantId,
            command.Item.PaymentMethodCode,
            command.Item.BankAccountPublicId,
            command.Item.IsPrimary,
            command.Item.EffectiveFromUtc,
            command.Item.EffectiveToUtc,
            command.Item.Notes,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFilePaymentMethodResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated payment method for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFilePaymentMethodResponse>.Success(response);
    }
}

internal sealed class PatchPersonnelFilePaymentMethodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFilePaymentMethodCommand, PersonnelFilePaymentMethodResponse>
{
    public async Task<Result<PersonnelFilePaymentMethodResponse>> Handle(
        PatchPersonnelFilePaymentMethodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFilePaymentMethodResponse>(
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
            return Result<PersonnelFilePaymentMethodResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetPaymentMethodAsync(personnelFile.PublicId, command.PaymentMethodPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFilePaymentMethodResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFilePaymentMethodResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFilePaymentMethodPatchState.From(existing);
        var applyResult = PersonnelFilePaymentMethodPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFilePaymentMethodResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFilePaymentMethodPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFilePaymentMethodResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFilePaymentMethodResponse>.Success(existing);
        }

        if (state.BankAccountPublicId.HasValue)
        {
            var bankAccountIds = (await personnelFileRepository.GetBankAccountIdsAsync(personnelFile.PublicId, cancellationToken)).ToHashSet();
            if (!bankAccountIds.Contains(state.BankAccountPublicId.Value))
            {
                return Result<PersonnelFilePaymentMethodResponse>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]> { ["bankAccountPublicId"] = ["Bank account does not exist in this personnel file."] }));
            }
        }

        var input = state.ToInput();
        var response = await employeeRepository.PatchPaymentMethodAsync(
            command.PaymentMethodPublicId,
            personnelFile.TenantId,
            input.PaymentMethodCode,
            input.BankAccountPublicId,
            input.IsPrimary,
            input.EffectiveFromUtc,
            input.EffectiveToUtc,
            input.Notes,
            input.IsActive,
            state.IsActiveMutated,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFilePaymentMethodResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched payment method for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFilePaymentMethodResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFilePaymentMethodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFilePaymentMethodCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFilePaymentMethodCommand command,
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

        var existing = await employeeRepository.GetPaymentMethodAsync(personnelFile.PublicId, command.PaymentMethodPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var removed = await employeeRepository.DeletePaymentMethodAsync(command.PaymentMethodPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deleted payment method for {personnelFile.FullName}.", null, cancellationToken);
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

internal sealed class GetPersonnelFilePaymentMethodsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFilePaymentMethodsQuery, IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>> Handle(
        GetPersonnelFilePaymentMethodsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetPaymentMethodsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFilePaymentMethodByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFilePaymentMethodByIdQuery, PersonnelFilePaymentMethodResponse>
{
    public async Task<Result<PersonnelFilePaymentMethodResponse>> Handle(
        GetPersonnelFilePaymentMethodByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<PersonnelFilePaymentMethodResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetPaymentMethodAsync(personnelFile!.PublicId, query.PaymentMethodPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFilePaymentMethodResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFilePaymentMethodResponse>.Success(response);
    }
}

internal static class PersonnelFilePaymentMethodPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFilePaymentMethodPatchOperation> operations, PersonnelFilePaymentMethodPatchState state)
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
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only root payment method properties can be patched.");
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

    public static Result Validate(PersonnelFilePaymentMethodPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.PaymentMethodCode))
        {
            errors["paymentMethodCode"] = ["PaymentMethodCode is required."];
        }

        if (state.EffectiveToUtc.HasValue && state.EffectiveFromUtc > state.EffectiveToUtc.Value)
        {
            errors["effectiveFromUtc"] = ["EffectiveFromUtc must be on or before EffectiveToUtc."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFilePaymentMethodPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "paymentMethodCode"))
        {
            return Mutate(state, () => state.PaymentMethodCode = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "bankAccountPublicId"))
        {
            return Mutate(state, () => state.BankAccountPublicId = isRemove ? null : PersonnelFileTalentPatch.ReadNullableGuid(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "isPrimary"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "IsPrimary cannot be removed.")
                : Mutate(state, () => state.IsPrimary = PersonnelFileTalentPatch.ReadRequiredBoolean(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "effectiveFromUtc"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "EffectiveFromUtc cannot be removed.")
                : Mutate(state, () => state.EffectiveFromUtc = PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "effectiveToUtc"))
        {
            return Mutate(state, () => state.EffectiveToUtc = isRemove ? null : PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
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

    private static Result Mutate(PersonnelFilePaymentMethodPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}

