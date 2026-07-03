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

internal sealed class AddPersonnelFileAdditionalBenefitCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileAdditionalBenefitCommand, PersonnelFileAdditionalBenefitResponse>
{
    public async Task<Result<PersonnelFileAdditionalBenefitResponse>> Handle(
        AddPersonnelFileAdditionalBenefitCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileAdditionalBenefitResponse>(
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
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        // benefitTypeCode is catalog-backed now (RF-010): must be an active AdditionalBenefitType code.
        var benefitTypeValidation = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            personnelFileRepository,
            personnelFile.TenantId,
            "benefitTypeCode",
            PersonnelCurriculumCatalogCategories.AdditionalBenefitType,
            command.Item.BenefitTypeCode,
            cancellationToken);
        if (benefitTypeValidation != Error.None)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(benefitTypeValidation);
        }

        var entity = PersonnelFileAdditionalBenefit.Create(
            command.Item.BenefitTypeCode,
            command.Item.StartDate,
            command.Item.EndDate,
            command.Item.IsActive,
            command.Item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddAdditionalBenefitAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Personnel file additional benefit response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added additional benefit for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileAdditionalBenefitResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileAdditionalBenefitCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileAdditionalBenefitCommand, PersonnelFileAdditionalBenefitResponse>
{
    public async Task<Result<PersonnelFileAdditionalBenefitResponse>> Handle(
        UpdatePersonnelFileAdditionalBenefitCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileAdditionalBenefitResponse>(
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
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetAdditionalBenefitAsync(personnelFile.PublicId, command.AdditionalBenefitPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // benefitTypeCode is catalog-backed now (RF-010): must be an active AdditionalBenefitType code.
        var benefitTypeValidation = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            personnelFileRepository,
            personnelFile.TenantId,
            "benefitTypeCode",
            PersonnelCurriculumCatalogCategories.AdditionalBenefitType,
            command.Item.BenefitTypeCode,
            cancellationToken);
        if (benefitTypeValidation != Error.None)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(benefitTypeValidation);
        }

        // PUT replaces business fields only; isActive is preserved (it is mutated exclusively via PATCH).
        var response = await employeeRepository.UpdateAdditionalBenefitAsync(
            command.AdditionalBenefitPublicId,
            personnelFile.TenantId,
            command.Item.BenefitTypeCode,
            command.Item.StartDate,
            command.Item.EndDate,
            command.Item.Notes,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated additional benefit for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileAdditionalBenefitResponse>.Success(response);
    }
}

internal sealed class PatchPersonnelFileAdditionalBenefitCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileAdditionalBenefitCommand, PersonnelFileAdditionalBenefitResponse>
{
    public async Task<Result<PersonnelFileAdditionalBenefitResponse>> Handle(
        PatchPersonnelFileAdditionalBenefitCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileAdditionalBenefitResponse>(
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
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetAdditionalBenefitAsync(personnelFile.PublicId, command.AdditionalBenefitPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFileAdditionalBenefitPatchState.From(existing);
        var applyResult = PersonnelFileAdditionalBenefitPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileAdditionalBenefitPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Success(existing);
        }

        var input = state.ToInput();

        // benefitTypeCode is catalog-backed now (RF-010): must be an active AdditionalBenefitType code.
        var benefitTypeValidation = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            personnelFileRepository,
            personnelFile.TenantId,
            "benefitTypeCode",
            PersonnelCurriculumCatalogCategories.AdditionalBenefitType,
            input.BenefitTypeCode,
            cancellationToken);
        if (benefitTypeValidation != Error.None)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(benefitTypeValidation);
        }

        var response = await employeeRepository.PatchAdditionalBenefitAsync(
            command.AdditionalBenefitPublicId,
            personnelFile.TenantId,
            input.BenefitTypeCode,
            input.StartDate,
            input.EndDate,
            input.Notes,
            input.IsActive,
            state.IsActiveMutated,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileAdditionalBenefitResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched additional benefit for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileAdditionalBenefitResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileAdditionalBenefitCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileAdditionalBenefitCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileAdditionalBenefitCommand command,
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

        var existing = await employeeRepository.GetAdditionalBenefitAsync(personnelFile.PublicId, command.AdditionalBenefitPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var removed = await employeeRepository.DeleteAdditionalBenefitAsync(command.AdditionalBenefitPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deleted additional benefit for {personnelFile.FullName}.", null, cancellationToken);
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

internal sealed class GetPersonnelFileAdditionalBenefitsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAdditionalBenefitsQuery, IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>> Handle(
        GetPersonnelFileAdditionalBenefitsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetAdditionalBenefitsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileAdditionalBenefitByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAdditionalBenefitByIdQuery, PersonnelFileAdditionalBenefitResponse>
{
    public async Task<Result<PersonnelFileAdditionalBenefitResponse>> Handle(
        GetPersonnelFileAdditionalBenefitByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<PersonnelFileAdditionalBenefitResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetAdditionalBenefitAsync(personnelFile!.PublicId, query.AdditionalBenefitPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileAdditionalBenefitResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileAdditionalBenefitResponse>.Success(response);
    }
}

internal static class PersonnelFileAdditionalBenefitPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFileAdditionalBenefitPatchOperation> operations, PersonnelFileAdditionalBenefitPatchState state)
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
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only root additional benefit properties can be patched.");
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

    public static Result Validate(PersonnelFileAdditionalBenefitPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.BenefitTypeCode))
        {
            errors["benefitTypeCode"] = ["BenefitTypeCode is required."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileAdditionalBenefitPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "benefitTypeCode"))
        {
            return Mutate(state, () => state.BenefitTypeCode = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "startDate"))
        {
            return Mutate(state, () => state.StartDate = isRemove ? null : PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
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

    private static Result Mutate(PersonnelFileAdditionalBenefitPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}

