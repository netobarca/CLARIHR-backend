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

internal sealed class AddPersonnelFileInsuranceBeneficiaryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileInsuranceBeneficiaryCommand, PersonnelFileInsuranceBeneficiaryResponse>
{
    public async Task<Result<PersonnelFileInsuranceBeneficiaryResponse>> Handle(
        AddPersonnelFileInsuranceBeneficiaryCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileInsuranceBeneficiaryResponse>(
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
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var kinshipValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
            personnelFileRepository, personnelFile.TenantId, "item.kinshipCode", command.Item.KinshipCode, cancellationToken);
        if (kinshipValidation != Error.None)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(kinshipValidation);
        }

        var beneficiaryError = await InsuranceBeneficiaryCommandValidation.ValidateAsync(
            personnelFileRepository, employeeRepository, personnelFile, command.InsurancePublicId, null, true, command.Item, cancellationToken);
        if (beneficiaryError != Error.None)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(beneficiaryError);
        }

        var response = await employeeRepository.AddInsuranceBeneficiaryAsync(
            personnelFile.PublicId, command.InsurancePublicId, personnelFile.TenantId, command.Item, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added insurance beneficiary for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileInsuranceBeneficiaryResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileInsuranceBeneficiaryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileInsuranceBeneficiaryCommand, PersonnelFileInsuranceBeneficiaryResponse>
{
    public async Task<Result<PersonnelFileInsuranceBeneficiaryResponse>> Handle(
        UpdatePersonnelFileInsuranceBeneficiaryCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileInsuranceBeneficiaryResponse>(
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
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var kinshipValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
            personnelFileRepository, personnelFile.TenantId, "item.kinshipCode", command.Item.KinshipCode, cancellationToken);
        if (kinshipValidation != Error.None)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(kinshipValidation);
        }

        var existing = await employeeRepository.GetInsuranceBeneficiaryAsync(
            personnelFile.PublicId, command.InsurancePublicId, command.BeneficiaryPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var beneficiaryError = await InsuranceBeneficiaryCommandValidation.ValidateAsync(
            personnelFileRepository, employeeRepository, personnelFile, command.InsurancePublicId, command.BeneficiaryPublicId, existing.IsActive, command.Item, cancellationToken);
        if (beneficiaryError != Error.None)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(beneficiaryError);
        }

        var response = await employeeRepository.UpdateInsuranceBeneficiaryAsync(
            personnelFile.PublicId,
            command.InsurancePublicId,
            command.BeneficiaryPublicId,
            personnelFile.TenantId,
            command.Item.FullName,
            command.Item.DocumentNumber,
            command.Item.BirthDate,
            command.Item.KinshipCode,
            command.Item.DocumentTypeCode,
            command.Item.AllocationPercentage,
            command.Item.BeneficiaryType,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated insurance beneficiary for {personnelFile.FullName}.", existing, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileInsuranceBeneficiaryResponse>.Success(response);
    }
}

internal sealed class PatchPersonnelFileInsuranceBeneficiaryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileInsuranceBeneficiaryCommand, PersonnelFileInsuranceBeneficiaryResponse>
{
    public async Task<Result<PersonnelFileInsuranceBeneficiaryResponse>> Handle(
        PatchPersonnelFileInsuranceBeneficiaryCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileInsuranceBeneficiaryResponse>(
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
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetInsuranceBeneficiaryAsync(
            personnelFile.PublicId, command.InsurancePublicId, command.BeneficiaryPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(existing);
        var applyResult = PersonnelFileInsuranceBeneficiaryPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileInsuranceBeneficiaryPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Success(existing);
        }

        if (state.KinshipCodeMutated)
        {
            var kinshipValidation = await PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync(
                personnelFileRepository, personnelFile.TenantId, "kinshipCode", state.KinshipCode, cancellationToken);
            if (kinshipValidation != Error.None)
            {
                return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(kinshipValidation);
            }
        }

        var input = state.ToInput();
        var beneficiaryError = await InsuranceBeneficiaryCommandValidation.ValidateAsync(
            personnelFileRepository, employeeRepository, personnelFile, command.InsurancePublicId, command.BeneficiaryPublicId, state.IsActive, input, cancellationToken);
        if (beneficiaryError != Error.None)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(beneficiaryError);
        }

        var response = await employeeRepository.PatchInsuranceBeneficiaryAsync(
            personnelFile.PublicId,
            command.InsurancePublicId,
            command.BeneficiaryPublicId,
            personnelFile.TenantId,
            input.FullName,
            input.DocumentNumber,
            input.BirthDate,
            input.KinshipCode,
            input.DocumentTypeCode,
            input.AllocationPercentage,
            input.BeneficiaryType,
            state.IsActive,
            state.IsActiveMutated,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched insurance beneficiary for {personnelFile.FullName}.", existing, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileInsuranceBeneficiaryResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileInsuranceBeneficiaryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileInsuranceBeneficiaryCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileInsuranceBeneficiaryCommand command,
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

        var existing = await employeeRepository.GetInsuranceBeneficiaryAsync(
            personnelFile.PublicId, command.InsurancePublicId, command.BeneficiaryPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var removed = await employeeRepository.DeleteInsuranceBeneficiaryAsync(
            personnelFile.PublicId, command.InsurancePublicId, command.BeneficiaryPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deleted insurance beneficiary for {personnelFile.FullName}.", existing, null, cancellationToken);
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

internal sealed class GetPersonnelFileInsuranceBeneficiariesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileInsuranceBeneficiariesQuery, IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryResponse>>> Handle(
        GetPersonnelFileInsuranceBeneficiariesQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForInsuranceReadAsync<IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetInsuranceBeneficiariesAsync(personnelFile!.PublicId, query.InsurancePublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileInsuranceBeneficiaryByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileInsuranceBeneficiaryByIdQuery, PersonnelFileInsuranceBeneficiaryResponse>
{
    public async Task<Result<PersonnelFileInsuranceBeneficiaryResponse>> Handle(
        GetPersonnelFileInsuranceBeneficiaryByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForInsuranceReadAsync<PersonnelFileInsuranceBeneficiaryResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetInsuranceBeneficiaryAsync(
            personnelFile!.PublicId, query.InsurancePublicId, query.BeneficiaryPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileInsuranceBeneficiaryResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileInsuranceBeneficiaryResponse>.Success(response);
    }
}

internal static class PersonnelFileInsuranceBeneficiaryPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryPatchOperation> operations, PersonnelFileInsuranceBeneficiaryPatchState state)
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
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only root insurance beneficiary properties can be patched.");
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

    public static Result Validate(PersonnelFileInsuranceBeneficiaryPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.FullName))
        {
            errors["fullName"] = ["FullName is required."];
        }

        if (string.IsNullOrWhiteSpace(state.KinshipCode))
        {
            errors["kinshipCode"] = ["KinshipCode is required."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileInsuranceBeneficiaryPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "fullName"))
        {
            return Mutate(state, () => state.FullName = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "documentNumber"))
        {
            return Mutate(state, () => state.DocumentNumber = isRemove ? null : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "birthDate"))
        {
            return Mutate(state, () => state.BirthDate = isRemove ? null : PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "kinshipCode"))
        {
            return Mutate(state, () =>
            {
                state.KinshipCode = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path);
                state.KinshipCodeMutated = true;
            });
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "documentTypeCode"))
        {
            return Mutate(state, () =>
            {
                state.DocumentTypeCode = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path);
                state.DocumentTypeCodeMutated = true;
            });
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "allocationPercentage"))
        {
            return Mutate(state, () => state.AllocationPercentage = isRemove ? null : PersonnelFileTalentPatch.ReadNullableDecimal(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "beneficiaryType"))
        {
            return Mutate(state, () => state.BeneficiaryType = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
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

    private static Result Mutate(PersonnelFileInsuranceBeneficiaryPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}

internal static class InsuranceBeneficiaryCommandValidation
{
    public static async Task<Error> ValidateAsync(
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        PersonnelFile personnelFile,
        Guid insurancePublicId,
        Guid? candidateBeneficiaryPublicId,
        bool candidateIsActive,
        InsuranceBeneficiaryInput item,
        CancellationToken cancellationToken)
    {
        // Document type from the existing IdentificationType catalog (D-10).
        if (!string.IsNullOrWhiteSpace(item.DocumentTypeCode))
        {
            var documentTypeError = await PersonnelReferenceCatalogValidation.ValidateIdentificationTypeCodeAsync(
                personnelFileRepository, personnelFile.TenantId, item.DocumentTypeCode!, cancellationToken);
            if (documentTypeError != Error.None)
            {
                return documentTypeError;
            }
        }

        var siblings = (await employeeRepository.GetInsuranceBeneficiariesAsync(personnelFile.PublicId, insurancePublicId, cancellationToken))
            .Select(beneficiary => new InsuranceRules.ExistingBeneficiary(
                beneficiary.Id,
                InsuranceRules.NormalizeDocumentKey(beneficiary.DocumentTypeCode, beneficiary.DocumentNumber),
                beneficiary.IsActive,
                InsuranceRules.IsPrimary(beneficiary.BeneficiaryType),
                beneficiary.AllocationPercentage))
            .ToArray();

        // Anti-duplicate beneficiary per insurance (D-13).
        var duplicateCheck = InsuranceRules.CheckBeneficiaryUnique(
            candidateBeneficiaryPublicId, item.DocumentTypeCode, item.DocumentNumber, siblings);
        if (duplicateCheck.IsFailure)
        {
            return duplicateCheck.Error;
        }

        // Primary-beneficiary allocation cap ≤ 100% (D-09).
        var allocationCheck = InsuranceRules.CheckPrimaryAllocation(
            candidateBeneficiaryPublicId, candidateIsActive, item.BeneficiaryType, item.AllocationPercentage, siblings);
        return allocationCheck.IsFailure ? allocationCheck.Error : Error.None;
    }
}

