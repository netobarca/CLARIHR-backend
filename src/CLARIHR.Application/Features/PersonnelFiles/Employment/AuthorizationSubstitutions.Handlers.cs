using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
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
/// Shared cross-feature validation for the substitution write handlers (Add/Update/Patch): catalog type code
/// (RF-002), substitute eligibility (RF-001), the position belonging to one of the substitute's active
/// assignments + its title snapshot (RF-003/D-02), and the effective-period rules (D-04/D-06/D-07). Mirrors
/// <see cref="EmploymentAssignmentCommandSupport"/> so each invariant has a single home and the handlers stay thin.
/// </summary>
internal static class AuthorizationSubstitutionCommandSupport
{
    /// <summary>Successful validation: the resolved position-title snapshot to persist alongside the slot id (D-02).</summary>
    internal sealed record Validated(string? PositionTitleSnapshot);

    public static async Task<Result<Validated>> ValidateAsync(
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        IPositionSlotRepository positionSlotRepository,
        PersonnelFile titular,
        AuthorizationSubstitutionInput input,
        Guid? candidatePublicId,
        bool isActiveForThisOperation,
        CancellationToken cancellationToken)
    {
        // (RF-002 / D-08) Type code must be an active substitution-types catalog code for the company country.
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                titular.TenantId, PersonnelCurriculumCatalogCategories.SubstitutionType, input.SubstitutionTypeCode, cancellationToken))
        {
            return Result<Validated>.Failure(AuthorizationSubstitutionErrors.TypeCodeInvalid);
        }

        // (RF-001) Substitute must exist, be in the same tenant, and be an active completed employee.
        var substitute = await personnelFileRepository.GetForAccessCheckAsync(input.SubstitutePersonnelFileId, cancellationToken);
        if (substitute is null)
        {
            // A cross-tenant id is reported as "not eligible" so the response never reveals another tenant's data.
            return Result<Validated>.Failure(
                await personnelFileRepository.ExistsOutsideTenantAsync(input.SubstitutePersonnelFileId, cancellationToken)
                    ? AuthorizationSubstitutionErrors.SubstituteNotEligible
                    : AuthorizationSubstitutionErrors.SubstituteNotFound);
        }

        if (!substitute.IsCompletedEmployee || !substitute.IsActive)
        {
            return Result<Validated>.Failure(AuthorizationSubstitutionErrors.SubstituteNotEligible);
        }

        // (RF-003 / D-02) The position must be one of the substitute's ACTIVE employment assignments.
        var substituteAssignments = await employeeRepository.GetEmploymentAssignmentsAsync(input.SubstitutePersonnelFileId, cancellationToken);
        var ownsSlot = substituteAssignments.Any(assignment =>
            assignment.IsActive && assignment.PositionSlotId == input.SubstitutePositionSlotPublicId);
        if (!ownsSlot)
        {
            return Result<Validated>.Failure(AuthorizationSubstitutionErrors.PositionNotOwned);
        }

        // The assignment response carries no title, so snapshot it from the slot itself (D-02).
        var slot = await positionSlotRepository.GetByIdAsync(input.SubstitutePositionSlotPublicId, cancellationToken);
        var titleSnapshot = string.IsNullOrWhiteSpace(slot?.Title) ? slot?.Code : slot.Title;

        // (D-04/D-06/D-07) Effective-period rules: a required end date is guaranteed by the validator, so Value is safe.
        var titularSubstitutions = (await employeeRepository.GetAuthorizationSubstitutionsAsync(titular.PublicId, cancellationToken))
            .Select(s => new AuthorizationSubstitutionRules.ExistingSubstitution(s.Id, s.StartDate, s.EndDate, s.IsActive))
            .ToArray();
        var substituteAsTitular = (await employeeRepository.GetAuthorizationSubstitutionsAsync(input.SubstitutePersonnelFileId, cancellationToken))
            .Select(s => new AuthorizationSubstitutionRules.ExistingSubstitution(s.Id, s.StartDate, s.EndDate, s.IsActive))
            .ToArray();

        var candidate = new AuthorizationSubstitutionRules.Candidate(
            candidatePublicId,
            input.StartDate,
            input.EndDate!.Value,
            isActiveForThisOperation);

        var evaluation = AuthorizationSubstitutionRules.Evaluate(candidate, titularSubstitutions, substituteAsTitular);
        return evaluation.IsFailure
            ? Result<Validated>.Failure(evaluation.Error)
            : Result<Validated>.Success(new Validated(titleSnapshot));
    }
}

internal sealed class AddPersonnelFileAuthorizationSubstitutionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionSlotRepository positionSlotRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileAuthorizationSubstitutionCommand, PersonnelFileAuthorizationSubstitutionResponse>
{
    public async Task<Result<PersonnelFileAuthorizationSubstitutionResponse>> Handle(
        AddPersonnelFileAuthorizationSubstitutionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageSubstitutionsAsync<PersonnelFileAuthorizationSubstitutionResponse>(
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
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (command.Item.SubstitutePersonnelFileId == command.PersonnelFileId)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["substitutePersonnelFileId"] = ["Self-substitution is not allowed."]
                }));
        }

        var validation = await AuthorizationSubstitutionCommandSupport.ValidateAsync(
            personnelFileRepository,
            employeeRepository,
            positionSlotRepository,
            personnelFile,
            command.Item,
            candidatePublicId: null,
            command.Item.IsActive,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(validation.Error);
        }

        var entity = PersonnelFileAuthorizationSubstitution.Create(
            command.Item.SubstitutionTypeCode,
            command.Item.SubstitutePersonnelFileId,
            command.Item.SubstitutePositionSlotPublicId,
            validation.Value.PositionTitleSnapshot,
            command.Item.StartDate,
            command.Item.EndDate!.Value,
            command.Item.IsActive,
            command.Item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddAuthorizationSubstitutionAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Personnel file authorization substitution response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added authorization substitution to {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileAuthorizationSubstitutionResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileAuthorizationSubstitutionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionSlotRepository positionSlotRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileAuthorizationSubstitutionCommand, PersonnelFileAuthorizationSubstitutionResponse>
{
    public async Task<Result<PersonnelFileAuthorizationSubstitutionResponse>> Handle(
        UpdatePersonnelFileAuthorizationSubstitutionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageSubstitutionsAsync<PersonnelFileAuthorizationSubstitutionResponse>(
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
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (command.Item.SubstitutePersonnelFileId == command.PersonnelFileId)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["substitutePersonnelFileId"] = ["Self-substitution is not allowed."]
                }));
        }

        var existing = await employeeRepository.GetAuthorizationSubstitutionAsync(personnelFile.PublicId, command.AuthorizationSubstitutionPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // PUT replaces business fields only; isActive is preserved (it is mutated exclusively via PATCH), so the
        // rules are evaluated against the stored active state.
        var validation = await AuthorizationSubstitutionCommandSupport.ValidateAsync(
            personnelFileRepository,
            employeeRepository,
            positionSlotRepository,
            personnelFile,
            command.Item,
            command.AuthorizationSubstitutionPublicId,
            existing.IsActive,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(validation.Error);
        }

        var response = await employeeRepository.UpdateAuthorizationSubstitutionAsync(
            command.AuthorizationSubstitutionPublicId,
            personnelFile.TenantId,
            command.Item.SubstitutionTypeCode,
            command.Item.SubstitutePersonnelFileId,
            command.Item.SubstitutePositionSlotPublicId,
            validation.Value.PositionTitleSnapshot,
            command.Item.StartDate,
            command.Item.EndDate!.Value,
            command.Item.Notes,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated authorization substitution for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileAuthorizationSubstitutionResponse>.Success(response);
    }
}

internal sealed class PatchPersonnelFileAuthorizationSubstitutionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionSlotRepository positionSlotRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileAuthorizationSubstitutionCommand, PersonnelFileAuthorizationSubstitutionResponse>
{
    public async Task<Result<PersonnelFileAuthorizationSubstitutionResponse>> Handle(
        PatchPersonnelFileAuthorizationSubstitutionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageSubstitutionsAsync<PersonnelFileAuthorizationSubstitutionResponse>(
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
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetAuthorizationSubstitutionAsync(personnelFile.PublicId, command.AuthorizationSubstitutionPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(existing);
        var applyResult = PersonnelFileAuthorizationSubstitutionPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileAuthorizationSubstitutionPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(validation.Error);
        }

        if (state.SubstitutePersonnelFileId == command.PersonnelFileId)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["substitutePersonnelFileId"] = ["Self-substitution is not allowed."]
                }));
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Success(existing);
        }

        var input = state.ToInput();
        var crossFeatureValidation = await AuthorizationSubstitutionCommandSupport.ValidateAsync(
            personnelFileRepository,
            employeeRepository,
            positionSlotRepository,
            personnelFile,
            input,
            command.AuthorizationSubstitutionPublicId,
            state.IsActive,
            cancellationToken);
        if (crossFeatureValidation.IsFailure)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(crossFeatureValidation.Error);
        }

        var response = await employeeRepository.PatchAuthorizationSubstitutionAsync(
            command.AuthorizationSubstitutionPublicId,
            personnelFile.TenantId,
            input.SubstitutionTypeCode,
            input.SubstitutePersonnelFileId,
            input.SubstitutePositionSlotPublicId,
            crossFeatureValidation.Value.PositionTitleSnapshot,
            input.StartDate,
            input.EndDate!.Value,
            input.Notes,
            input.IsActive,
            state.IsActiveMutated,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched authorization substitution for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileAuthorizationSubstitutionResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileAuthorizationSubstitutionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileAuthorizationSubstitutionCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileAuthorizationSubstitutionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageSubstitutionsAsync<PersonnelFileParentConcurrencyResult>(
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

        var existing = await employeeRepository.GetAuthorizationSubstitutionAsync(personnelFile.PublicId, command.AuthorizationSubstitutionPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var removed = await employeeRepository.DeleteAuthorizationSubstitutionAsync(command.AuthorizationSubstitutionPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deleted authorization substitution for {personnelFile.FullName}.", null, cancellationToken);
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

internal sealed class GetPersonnelFileAuthorizationSubstitutionByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAuthorizationSubstitutionByIdQuery, PersonnelFileAuthorizationSubstitutionResponse>
{
    public async Task<Result<PersonnelFileAuthorizationSubstitutionResponse>> Handle(
        GetPersonnelFileAuthorizationSubstitutionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<PersonnelFileAuthorizationSubstitutionResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetAuthorizationSubstitutionAsync(personnelFile!.PublicId, query.AuthorizationSubstitutionPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileAuthorizationSubstitutionResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileAuthorizationSubstitutionResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFileAuthorizationSubstitutionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileAuthorizationSubstitutionsQuery, IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>> Handle(
        GetPersonnelFileAuthorizationSubstitutionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetAuthorizationSubstitutionsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>.Success(response);
    }
}

internal static class PersonnelFileAuthorizationSubstitutionPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionPatchOperation> operations, PersonnelFileAuthorizationSubstitutionPatchState state)
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
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only root authorization substitution properties can be patched.");
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

    public static Result Validate(PersonnelFileAuthorizationSubstitutionPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.SubstitutionTypeCode))
        {
            errors["substitutionTypeCode"] = ["SubstitutionTypeCode is required."];
        }

        if (state.SubstitutePersonnelFileId == Guid.Empty)
        {
            errors["substitutePersonnelFileId"] = ["SubstitutePersonnelFileId is required."];
        }

        if (state.SubstitutePositionSlotId == Guid.Empty)
        {
            errors["substitutePositionSlotId"] = ["SubstitutePositionSlotId is required."];
        }

        if (state.EndDate is null)
        {
            errors["endDate"] = ["EndDate is required."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileAuthorizationSubstitutionPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "substitutionTypeCode"))
        {
            return Mutate(state, () => state.SubstitutionTypeCode = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "substitutePersonnelFileId"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "SubstitutePersonnelFileId cannot be removed.")
                : Mutate(state, () => state.SubstitutePersonnelFileId = PersonnelFileTalentPatch.ReadNullableGuid(value, path) ?? Guid.Empty);
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "substitutePositionSlotId"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "SubstitutePositionSlotId cannot be removed.")
                : Mutate(state, () => state.SubstitutePositionSlotId = PersonnelFileTalentPatch.ReadNullableGuid(value, path) ?? Guid.Empty);
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "startDate"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "StartDate cannot be removed.")
                : Mutate(state, () => state.StartDate = PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "endDate"))
        {
            return isRemove
                ? PersonnelFileTalentPatch.ValidationFailure(path, "EndDate cannot be removed.")
                : Mutate(state, () => state.EndDate = PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
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

    private static Result Mutate(PersonnelFileAuthorizationSubstitutionPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}

