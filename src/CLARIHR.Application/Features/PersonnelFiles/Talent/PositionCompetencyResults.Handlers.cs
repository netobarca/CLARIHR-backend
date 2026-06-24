using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class AddPersonnelFilePositionCompetencyResultCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompetencyFrameworkRepository competencyFrameworkRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFilePositionCompetencyResultCommand, PersonnelFilePositionCompetencyResultResponse>
{
    public async Task<Result<PersonnelFilePositionCompetencyResultResponse>> Handle(
        AddPersonnelFilePositionCompetencyResultCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCompetenciesAsync<PersonnelFilePositionCompetencyResultResponse>(
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
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var resolved = await PositionCompetencyResultSupport.ResolveAsync(
            personnelFile.TenantId,
            personnelFile.PublicId,
            command.Item,
            competencyFrameworkRepository,
            employeeRepository,
            cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(resolved.Error);
        }

        var entity = PersonnelFilePositionCompetencyResult.Create(
            resolved.Value.CompetencyCatalogItemId,
            resolved.Value.CompetencyTypeCatalogItemId,
            resolved.Value.ExpectationInternalId,
            resolved.Value.ExpectedValue,
            command.Item.AchievedScore,
            command.Item.EvaluationDateUtc,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        employeeRepository.AddPositionCompetencyResult(entity);
        TouchPersonnelFile(personnelFile);

        PersonnelFilePositionCompetencyResultResponse response;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            response = await employeeRepository.GetPositionCompetencyResultAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file position competency result response could not be resolved after creation.");
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added position competency result for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFilePositionCompetencyResultResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFilePositionCompetencyResultCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompetencyFrameworkRepository competencyFrameworkRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFilePositionCompetencyResultCommand, PersonnelFilePositionCompetencyResultResponse>
{
    public async Task<Result<PersonnelFilePositionCompetencyResultResponse>> Handle(
        UpdatePersonnelFilePositionCompetencyResultCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCompetenciesAsync<PersonnelFilePositionCompetencyResultResponse>(
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
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetPositionCompetencyResultAsync(personnelFile.PublicId, command.PositionCompetencyResultPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var response = await PositionCompetencyResultSupport.MutateAsync(
            personnelFile,
            command.PositionCompetencyResultPublicId,
            command.Item,
            competencyFrameworkRepository,
            employeeRepository,
            cancellationToken);
        if (response.IsFailure)
        {
            return response;
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated position competency result for {personnelFile.FullName}.", existing, response.Value, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return response;
    }
}

internal sealed class PatchPersonnelFilePositionCompetencyResultCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompetencyFrameworkRepository competencyFrameworkRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFilePositionCompetencyResultCommand, PersonnelFilePositionCompetencyResultResponse>
{
    public async Task<Result<PersonnelFilePositionCompetencyResultResponse>> Handle(
        PatchPersonnelFilePositionCompetencyResultCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCompetenciesAsync<PersonnelFilePositionCompetencyResultResponse>(
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
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetPositionCompetencyResultAsync(personnelFile.PublicId, command.PositionCompetencyResultPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFilePositionCompetencyResultPatchState.From(existing);
        var applyResult = PersonnelFilePositionCompetencyResultPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFilePositionCompetencyResultPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFilePositionCompetencyResultResponse>.Success(existing);
        }

        var response = await PositionCompetencyResultSupport.MutateAsync(
            personnelFile,
            command.PositionCompetencyResultPublicId,
            state.ToInput(),
            competencyFrameworkRepository,
            employeeRepository,
            cancellationToken);
        if (response.IsFailure)
        {
            return response;
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched position competency result for {personnelFile.FullName}.", existing, response.Value, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return response;
    }
}

internal sealed class DeletePersonnelFilePositionCompetencyResultCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFilePositionCompetencyResultCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFilePositionCompetencyResultCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCompetenciesAsync<PersonnelFileParentConcurrencyResult>(
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

        var existing = await employeeRepository.GetPositionCompetencyResultAsync(personnelFile.PublicId, command.PositionCompetencyResultPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var removed = await employeeRepository.DeletePositionCompetencyResultAsync(command.PositionCompetencyResultPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deleted position competency result for {personnelFile.FullName}.", existing, null, cancellationToken);
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

internal sealed class GetPersonnelFilePositionCompetencyResultsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFilePositionCompetencyResultsQuery, IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>> Handle(
        GetPersonnelFilePositionCompetencyResultsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompetencyReadAsync<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>(
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

        var response = await employeeRepository.GetPositionCompetencyResultsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFilePositionCompetencyResultByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFilePositionCompetencyResultByIdQuery, PersonnelFilePositionCompetencyResultResponse>
{
    public async Task<Result<PersonnelFilePositionCompetencyResultResponse>> Handle(
        GetPersonnelFilePositionCompetencyResultByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompetencyReadAsync<PersonnelFilePositionCompetencyResultResponse>(
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

        var response = await employeeRepository.GetPositionCompetencyResultAsync(personnelFile!.PublicId, query.PositionCompetencyResultPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFilePositionCompetencyResultResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFilePositionCompetencyResultResponse>.Success(response);
    }
}

/// <summary>
/// Shared resolution/mutation for recording an employee competency result: validates the matrix expectation
/// (exists, belongs to the employee's assigned position — RF-011), the active rating scale and the achieved
/// score range (RF-005), and snapshots the expected value used to derive the gap (RF-002/D-05).
/// </summary>
internal static class PositionCompetencyResultSupport
{
    public static async Task<Result<ResolvedPositionCompetency>> ResolveAsync(
        Guid tenantId,
        Guid personnelFilePublicId,
        PositionCompetencyResultInput input,
        ICompetencyFrameworkRepository competencyFrameworkRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        var expectation = await competencyFrameworkRepository.GetExpectationReferenceAsync(tenantId, input.ExpectationPublicId, cancellationToken);
        if (expectation is null)
        {
            return Result<ResolvedPositionCompetency>.Failure(PositionCompetencyResultErrors.ExpectationInvalid);
        }

        var assignedJobProfileId = await employeeRepository.GetActiveAssignedJobProfileInternalIdAsync(personnelFilePublicId, cancellationToken);
        if (assignedJobProfileId is null || assignedJobProfileId.Value != expectation.JobProfileInternalId)
        {
            return Result<ResolvedPositionCompetency>.Failure(PositionCompetencyResultErrors.NotInProfile);
        }

        var scale = await competencyFrameworkRepository.GetActiveRatingScaleAsync(tenantId, cancellationToken);
        if (scale is null)
        {
            return Result<ResolvedPositionCompetency>.Failure(PositionCompetencyResultErrors.ScaleNotConfigured);
        }

        if (!scale.IsValueAllowed(input.AchievedScore))
        {
            return Result<ResolvedPositionCompetency>.Failure(PositionCompetencyResultErrors.ScoreOutOfRange);
        }

        return Result<ResolvedPositionCompetency>.Success(new ResolvedPositionCompetency(
            expectation.CompetencyCatalogItemId,
            expectation.CompetencyTypeCatalogItemId,
            expectation.ExpectationInternalId,
            expectation.ExpectedValue));
    }

    public static async Task<Result<PersonnelFilePositionCompetencyResultResponse>> MutateAsync(
        Domain.PersonnelFiles.PersonnelFile personnelFile,
        Guid itemPublicId,
        PositionCompetencyResultInput input,
        ICompetencyFrameworkRepository competencyFrameworkRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(
            personnelFile.TenantId,
            personnelFile.PublicId,
            input,
            competencyFrameworkRepository,
            employeeRepository,
            cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(resolved.Error);
        }

        var updated = await employeeRepository.UpdatePositionCompetencyResultAsync(
            itemPublicId,
            personnelFile.TenantId,
            resolved.Value.CompetencyCatalogItemId,
            resolved.Value.CompetencyTypeCatalogItemId,
            resolved.Value.ExpectationInternalId,
            resolved.Value.ExpectedValue,
            input.AchievedScore,
            input.EvaluationDateUtc,
            input.SourceSystem,
            input.SourceReference,
            input.SourceSyncedUtc,
            cancellationToken);
        if (!updated)
        {
            return Result<PersonnelFilePositionCompetencyResultResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var response = await employeeRepository.GetPositionCompetencyResultAsync(personnelFile.PublicId, itemPublicId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file position competency result response could not be resolved after update.");
        return Result<PersonnelFilePositionCompetencyResultResponse>.Success(response);
    }
}

internal sealed record ResolvedPositionCompetency(
    long CompetencyCatalogItemId,
    long CompetencyTypeCatalogItemId,
    long ExpectationInternalId,
    decimal? ExpectedValue);

internal static class PersonnelFilePositionCompetencyResultPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFilePositionCompetencyResultPatchOperation> operations, PersonnelFilePositionCompetencyResultPatchState state)
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
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only root position competency result properties can be patched.");
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

    public static Result Validate(PersonnelFilePositionCompetencyResultPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (state.ExpectationPublicId == Guid.Empty)
        {
            errors["expectationPublicId"] = ["ExpectationPublicId is required."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFilePositionCompetencyResultPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "expectationPublicId"))
        {
            if (isRemove)
            {
                return PersonnelFileTalentPatch.ValidationFailure(path, "ExpectationPublicId cannot be removed.");
            }

            return Mutate(state, () => state.ExpectationPublicId = PersonnelFileTalentPatch.ReadNullableGuid(value, path) ?? Guid.Empty);
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "achievedScore"))
        {
            if (isRemove)
            {
                return PersonnelFileTalentPatch.ValidationFailure(path, "AchievedScore cannot be removed.");
            }

            return Mutate(state, () => state.AchievedScore = PersonnelFileTalentPatch.ReadRequiredDecimal(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "evaluationDateUtc"))
        {
            if (isRemove)
            {
                return PersonnelFileTalentPatch.ValidationFailure(path, "EvaluationDateUtc cannot be removed.");
            }

            return Mutate(state, () => state.EvaluationDateUtc = PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "sourceSystem"))
        {
            return Mutate(state, () => state.SourceSystem = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "sourceReference"))
        {
            return Mutate(state, () => state.SourceReference = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "sourceSyncedUtc"))
        {
            return Mutate(state, () => state.SourceSyncedUtc = isRemove ? null : PersonnelFileTalentPatch.ReadRequiredDateTime(value, path));
        }

        return PersonnelFileTalentPatch.ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static Result Mutate(PersonnelFilePositionCompetencyResultPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}
