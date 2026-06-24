using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Resolved snapshot values for an off-payroll-transaction write, produced after validating the catalog type,
/// resolving the default currency, validating the optional AssetAccess link and the correction reference.
/// </summary>
internal sealed record OffPayrollTransactionResolved(
    string CurrencyCode,
    string? TransactionTypeName,
    string? AssetName);

/// <summary>
/// Cross-aggregate validation + snapshot resolution shared by the off-payroll write handlers (database-backed,
/// so it lives outside the pure <see cref="OffPayrollTransactionRules"/> module): the type must be an active
/// catalog code (D-03) and its description is snapshotted (RN-09); the currency defaults from the company
/// preference when omitted and is mandatory (D-08); the optional AssetAccess link must belong to the same
/// employee (D-01); a negative amount must reference a valid original transaction (D-12).
/// </summary>
internal static class OffPayrollTransactionWriteSupport
{
    public static async Task<Result<OffPayrollTransactionResolved>> ResolveAndValidateAsync(
        OffPayrollTransactionInput input,
        PersonnelFile personnelFile,
        IPersonnelFileEmployeeRepository employeeRepository,
        IPersonnelFileRepository personnelFileRepository,
        ICompanyPreferenceRepository companyPreferenceRepository,
        CancellationToken cancellationToken)
    {
        // 1) Type must be an active code in the company's country catalog (D-03); snapshot its description (RN-09).
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.OffPayrollTransactionType, input.TransactionTypeCode, cancellationToken))
        {
            return Result<OffPayrollTransactionResolved>.Failure(OffPayrollTransactionErrors.TypeCodeInvalid);
        }

        var typeName = await personnelFileRepository.GetCatalogItemNameAsync(
            personnelFile.TenantId, PersonnelCurriculumCatalogCategories.OffPayrollTransactionType, input.TransactionTypeCode, cancellationToken);

        // 2) Currency is mandatory (D-08); default from the company preference when omitted.
        var currency = input.CurrencyCode;
        if (string.IsNullOrWhiteSpace(currency))
        {
            var preference = await companyPreferenceRepository.GetByTenantIdAsync(personnelFile.TenantId, cancellationToken);
            currency = preference?.CurrencyCode;
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result<OffPayrollTransactionResolved>.Failure(OffPayrollTransactionErrors.CurrencyRequired);
        }

        currency = currency.Trim().ToUpperInvariant();

        // 3) Optional link to an AssetAccess of the SAME employee (D-01); snapshot its name. GetAssetAccessAsync
        //    already filters by personnel file, so a null result means "not found or not owned".
        string? assetName = null;
        if (input.AssetAccessPublicId is { } assetAccessId && assetAccessId != Guid.Empty)
        {
            var asset = await employeeRepository.GetAssetAccessAsync(personnelFile.PublicId, assetAccessId, cancellationToken);
            if (asset is null)
            {
                return Result<OffPayrollTransactionResolved>.Failure(OffPayrollTransactionErrors.AssetAccessNotFound);
            }

            assetName = asset.AssetOrAccessName;
        }

        // 4) A negative amount (adjustment) must reference a valid original transaction (D-12): it must exist for
        //    this employee, be active, be an original expense (not itself an adjustment), and share the currency.
        if (OffPayrollTransactionRules.RequiresCorrectionReference(input.Amount, input.CorrectsTransactionPublicId))
        {
            return Result<OffPayrollTransactionResolved>.Failure(OffPayrollTransactionErrors.CorrectionRequired);
        }

        if (input.CorrectsTransactionPublicId is { } correctedId && correctedId != Guid.Empty)
        {
            var original = await employeeRepository.GetOffPayrollTransactionAsync(personnelFile.PublicId, correctedId, cancellationToken);
            if (original is null)
            {
                return Result<OffPayrollTransactionResolved>.Failure(OffPayrollTransactionErrors.CorrectedNotFound);
            }

            if (!original.IsActive
                || original.CorrectsTransactionPublicId is not null
                || !string.Equals(original.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase))
            {
                return Result<OffPayrollTransactionResolved>.Failure(OffPayrollTransactionErrors.CorrectedInvalid);
            }
        }

        return Result<OffPayrollTransactionResolved>.Success(new OffPayrollTransactionResolved(currency, typeName, assetName));
    }
}

internal sealed class AddPersonnelFileOffPayrollTransactionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileOffPayrollTransactionCommand, PersonnelFileOffPayrollTransactionResponse>
{
    public async Task<Result<PersonnelFileOffPayrollTransactionResponse>> Handle(
        AddPersonnelFileOffPayrollTransactionCommand command,
        CancellationToken cancellationToken)
    {
        // HR-only (D-06): the dedicated manage permission. No self-service.
        var (failure, personnelFile) = await LoadForManageOffPayrollTransactionsAsync<PersonnelFileOffPayrollTransactionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var resolveResult = await OffPayrollTransactionWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, employeeRepository, personnelFileRepository, companyPreferenceRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(resolveResult.Error);
        }

        var resolved = resolveResult.Value;
        var entity = PersonnelFileOffPayrollTransaction.Create(
            command.Item.TransactionTypeCode,
            resolved.TransactionTypeName,
            command.Item.TransactionDateUtc,
            resolved.CurrencyCode,
            command.Item.Amount,
            command.Item.Year,
            command.Item.Month,
            command.Item.Comment,
            command.Item.AssetAccessPublicId,
            resolved.AssetName,
            command.Item.CorrectsTransactionPublicId);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddOffPayrollTransactionAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Personnel file off-payroll transaction response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added off-payroll transaction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileOffPayrollTransactionResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileOffPayrollTransactionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileOffPayrollTransactionCommand, PersonnelFileOffPayrollTransactionResponse>
{
    public async Task<Result<PersonnelFileOffPayrollTransactionResponse>> Handle(
        UpdatePersonnelFileOffPayrollTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOffPayrollTransactionsAsync<PersonnelFileOffPayrollTransactionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetOffPayrollTransactionAsync(personnelFile.PublicId, command.OffPayrollTransactionPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var resolveResult = await OffPayrollTransactionWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, employeeRepository, personnelFileRepository, companyPreferenceRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(resolveResult.Error);
        }

        var resolved = resolveResult.Value;

        // PUT replaces business fields only; isActive is preserved (it is mutated exclusively via PATCH/DELETE).
        var response = await employeeRepository.UpdateOffPayrollTransactionAsync(
            command.OffPayrollTransactionPublicId,
            personnelFile.TenantId,
            command.Item,
            resolved.CurrencyCode,
            resolved.TransactionTypeName,
            resolved.AssetName,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated off-payroll transaction for {personnelFile.FullName}.", existing, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileOffPayrollTransactionResponse>.Success(response);
    }
}

internal sealed class PatchPersonnelFileOffPayrollTransactionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileOffPayrollTransactionCommand, PersonnelFileOffPayrollTransactionResponse>
{
    public async Task<Result<PersonnelFileOffPayrollTransactionResponse>> Handle(
        PatchPersonnelFileOffPayrollTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOffPayrollTransactionsAsync<PersonnelFileOffPayrollTransactionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetOffPayrollTransactionAsync(personnelFile.PublicId, command.OffPayrollTransactionPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFileOffPayrollTransactionPatchState.From(existing);
        var applyResult = PersonnelFileOffPayrollTransactionPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileOffPayrollTransactionPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Success(existing);
        }

        var input = state.ToInput();
        var resolveResult = await OffPayrollTransactionWriteSupport.ResolveAndValidateAsync(
            input, personnelFile, employeeRepository, personnelFileRepository, companyPreferenceRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(resolveResult.Error);
        }

        var resolved = resolveResult.Value;
        var response = await employeeRepository.PatchOffPayrollTransactionAsync(
            command.OffPayrollTransactionPublicId,
            personnelFile.TenantId,
            input,
            resolved.CurrencyCode,
            resolved.TransactionTypeName,
            resolved.AssetName,
            state.IsActive,
            state.IsActiveMutated,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileOffPayrollTransactionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched off-payroll transaction for {personnelFile.FullName}.", existing, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileOffPayrollTransactionResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileOffPayrollTransactionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileOffPayrollTransactionCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileOffPayrollTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOffPayrollTransactionsAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetOffPayrollTransactionAsync(personnelFile.PublicId, command.OffPayrollTransactionPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Soft delete (RN-10): no physical removal — deactivate the record so it is preserved for audit/history.
        var removed = await employeeRepository.SoftDeleteOffPayrollTransactionAsync(command.OffPayrollTransactionPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated off-payroll transaction for {personnelFile.FullName}.", existing, null, cancellationToken);
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

internal sealed class GetPersonnelFileOffPayrollTransactionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileOffPayrollTransactionsQuery, IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>>> Handle(
        GetPersonnelFileOffPayrollTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOffPayrollReadAsync<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetOffPayrollTransactionsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileOffPayrollTransactionByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileOffPayrollTransactionByIdQuery, PersonnelFileOffPayrollTransactionResponse>
{
    public async Task<Result<PersonnelFileOffPayrollTransactionResponse>> Handle(
        GetPersonnelFileOffPayrollTransactionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOffPayrollReadAsync<PersonnelFileOffPayrollTransactionResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetOffPayrollTransactionAsync(personnelFile!.PublicId, query.OffPayrollTransactionPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileOffPayrollTransactionResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileOffPayrollTransactionResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFileOffPayrollTransactionTotalsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileOffPayrollTransactionTotalsQuery, IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>>
{
    public async Task<Result<IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>>> Handle(
        GetPersonnelFileOffPayrollTransactionTotalsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOffPayrollReadAsync<IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetOffPayrollTransactionTotalsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>>.Success(response);
    }
}

internal static class PersonnelFileOffPayrollTransactionPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFileOffPayrollTransactionPatchOperation> operations, PersonnelFileOffPayrollTransactionPatchState state)
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
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only root off-payroll transaction properties can be patched.");
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

    public static Result Validate(PersonnelFileOffPayrollTransactionPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.TransactionTypeCode))
        {
            errors["transactionTypeCode"] = ["TransactionTypeCode is required."];
        }

        if (state.Amount == 0)
        {
            errors["amount"] = ["Amount must be a non-zero value."];
        }

        if (!OffPayrollTransactionRules.IsValidPeriod(state.Year, state.Month))
        {
            errors["period"] = ["Month must be between 1 and 12 and year within the supported range."];
        }

        if (state.Amount < 0 && (state.CorrectsTransactionPublicId is null || state.CorrectsTransactionPublicId == Guid.Empty))
        {
            errors["correctsTransactionPublicId"] = ["A negative amount must reference the original transaction it corrects."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileOffPayrollTransactionPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "transactionTypeCode"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "TransactionTypeCode cannot be removed.")
                : Mutate(state, () => state.TransactionTypeCode = PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "transactionDateUtc"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "TransactionDateUtc cannot be removed.")
                : Mutate(state, () => state.TransactionDateUtc = PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "currencyCode"))
        {
            return Mutate(state, () => state.CurrencyCode = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "amount"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "Amount cannot be removed.")
                : Mutate(state, () => state.Amount = PersonnelFileTalentPatch.ReadRequiredDecimal(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "year"))
        {
            if (isRemove)
            {
                return PersonnelFileTalentPatch.ValidationFailure(path, "Year cannot be removed.");
            }

            var year = PersonnelFileTalentPatch.ReadNullableInt(value, path);
            return year is null
                ? PersonnelFileTalentPatch.ValidationFailure(path, "Year is required.")
                : Mutate(state, () => state.Year = year.Value);
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "month"))
        {
            if (isRemove)
            {
                return PersonnelFileTalentPatch.ValidationFailure(path, "Month cannot be removed.");
            }

            var month = PersonnelFileTalentPatch.ReadNullableInt(value, path);
            return month is null
                ? PersonnelFileTalentPatch.ValidationFailure(path, "Month is required.")
                : Mutate(state, () => state.Month = month.Value);
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "comment"))
        {
            return Mutate(state, () => state.Comment = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "assetAccessPublicId"))
        {
            return Mutate(state, () => state.AssetAccessPublicId = isRemove ? null : PersonnelFileTalentPatch.ReadNullableGuid(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "correctsTransactionPublicId"))
        {
            return Mutate(state, () => state.CorrectsTransactionPublicId = isRemove ? null : PersonnelFileTalentPatch.ReadNullableGuid(value, path));
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

    private static Result Mutate(PersonnelFileOffPayrollTransactionPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}
