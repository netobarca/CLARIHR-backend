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

/// <summary>
/// Coded errors for the manual contract-history endpoint. The code has a matching entry in
/// BackendMessages.resx and BackendMessages.es.resx (parity enforced by BackendMessageLocalizationTests).
/// </summary>
internal static class ContractHistoryErrors
{
    public static readonly Error ContractTypeCodeInvalid = new(
        "CONTRACT_TYPE_CODE_INVALID",
        "The contract type code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);
}

internal sealed class AddPersonnelFileContractHistoryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileContractHistoryCommand, PersonnelFileContractHistoryResponse>
{
    public async Task<Result<PersonnelFileContractHistoryResponse>> Handle(
        AddPersonnelFileContractHistoryCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileContractHistoryResponse>(
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
            return Result<PersonnelFileContractHistoryResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        // contractTypeCode is a country-scoped catalog code (general-catalogs key `contract-types`). An
        // inactive/unknown code returns a controlled 422 instead of crashing the insert with a free-text value.
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.ContractType, command.Item.ContractTypeCode, cancellationToken))
        {
            return Result<PersonnelFileContractHistoryResponse>.Failure(ContractHistoryErrors.ContractTypeCodeInvalid);
        }

        var entity = PersonnelFileContractHistory.Create(
            command.Item.ContractTypeCode,
            command.Item.ContractDate,
            command.Item.ContractEndDate,
            command.Item.PositionSlotId,
            command.Item.IsActive,
            command.Item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddContractHistoryAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Personnel file contract history response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added contract history to {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileContractHistoryResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileContractHistoryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileContractHistoryCommand, PersonnelFileContractHistoryResponse>
{
    public async Task<Result<PersonnelFileContractHistoryResponse>> Handle(
        UpdatePersonnelFileContractHistoryCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileContractHistoryResponse>(
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
            return Result<PersonnelFileContractHistoryResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetContractHistoryAsync(personnelFile.PublicId, command.ContractHistoryPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileContractHistoryResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileContractHistoryResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.ContractType, command.Item.ContractTypeCode, cancellationToken))
        {
            return Result<PersonnelFileContractHistoryResponse>.Failure(ContractHistoryErrors.ContractTypeCodeInvalid);
        }

        // PUT replaces business fields only; isActive is preserved (it is mutated exclusively via PATCH).
        var response = await employeeRepository.UpdateContractHistoryAsync(
            command.ContractHistoryPublicId,
            personnelFile.TenantId,
            command.Item.ContractTypeCode,
            command.Item.ContractDate,
            command.Item.ContractEndDate,
            command.Item.PositionSlotId,
            command.Item.Notes,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileContractHistoryResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated contract history for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileContractHistoryResponse>.Success(response);
    }
}

internal sealed class PatchPersonnelFileContractHistoryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileContractHistoryCommand, PersonnelFileContractHistoryResponse>
{
    public async Task<Result<PersonnelFileContractHistoryResponse>> Handle(
        PatchPersonnelFileContractHistoryCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileContractHistoryResponse>(
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
            return Result<PersonnelFileContractHistoryResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetContractHistoryAsync(personnelFile.PublicId, command.ContractHistoryPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileContractHistoryResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileContractHistoryResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFileContractHistoryPatchState.From(existing);
        var applyResult = PersonnelFileContractHistoryPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileContractHistoryResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileContractHistoryPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileContractHistoryResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileContractHistoryResponse>.Success(existing);
        }

        var input = state.ToInput();
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.ContractType, input.ContractTypeCode, cancellationToken))
        {
            return Result<PersonnelFileContractHistoryResponse>.Failure(ContractHistoryErrors.ContractTypeCodeInvalid);
        }

        var response = await employeeRepository.PatchContractHistoryAsync(
            command.ContractHistoryPublicId,
            personnelFile.TenantId,
            input.ContractTypeCode,
            input.ContractDate,
            input.ContractEndDate,
            input.PositionSlotId,
            input.Notes,
            input.IsActive,
            state.IsActiveMutated,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileContractHistoryResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched contract history for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileContractHistoryResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFileContractHistoryByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileContractHistoryByIdQuery, PersonnelFileContractHistoryResponse>
{
    public async Task<Result<PersonnelFileContractHistoryResponse>> Handle(
        GetPersonnelFileContractHistoryByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<PersonnelFileContractHistoryResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetContractHistoryAsync(personnelFile!.PublicId, query.ContractHistoryPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileContractHistoryResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileContractHistoryResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFileContractHistoryQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileContractHistoryQuery, IReadOnlyCollection<PersonnelFileContractHistoryResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>> Handle(
        GetPersonnelFileContractHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetContractHistoryAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>.Success(response);
    }
}

internal static class PersonnelFileContractHistoryPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFileContractHistoryPatchOperation> operations, PersonnelFileContractHistoryPatchState state)
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
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only root contract history properties can be patched.");
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

    public static Result Validate(PersonnelFileContractHistoryPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.ContractTypeCode))
        {
            errors["contractTypeCode"] = ["ContractTypeCode is required."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileContractHistoryPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "contractTypeCode"))
        {
            return Mutate(state, () => state.ContractTypeCode = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "contractDate"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "ContractDate cannot be removed.")
                : Mutate(state, () => state.ContractDate = PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "contractEndDate"))
        {
            return Mutate(state, () => state.ContractEndDate = isRemove ? null : PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "positionSlotId"))
        {
            return Mutate(state, () => state.PositionSlotId = isRemove ? null : PersonnelFileTalentPatch.ReadNullableGuid(value, path));
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

    private static Result Mutate(PersonnelFileContractHistoryPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}

