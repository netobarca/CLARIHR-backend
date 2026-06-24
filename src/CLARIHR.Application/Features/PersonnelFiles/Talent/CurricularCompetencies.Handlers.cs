using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Impure validation orchestration for curricular competencies (mirrors <c>AssetAccessCommandSupport</c>):
/// resolves the requirement-type (D-01/D-02) and competency-domain (D-03) tenant catalogs by code, validates the
/// optional metric against the country catalog (D-04), and enforces experience coherence (D-06) and the
/// anti-duplicate invariant (D-05). Returns the canonical catalog codes so the handlers persist a stable,
/// de-dup-safe form.
/// </summary>
internal static class CurricularCompetencyCommandValidation
{
    public static async Task<Result<CurricularCompetencyResolved>> ValidateAsync(
        IPositionCatalogLookup positionCatalog,
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        PersonnelFile file,
        Guid? candidatePublicId,
        CurricularCompetencyInput input,
        CancellationToken cancellationToken)
    {
        // (D-01/D-02) Requirement type must be an active RequirementType catalog code for the tenant.
        var type = await positionCatalog.GetActiveCatalogReferenceByCodeAsync(
            file.TenantId, PositionDescriptionCatalogType.RequirementType, input.RequirementTypeCode, cancellationToken);
        if (type is null)
        {
            return Result<CurricularCompetencyResolved>.Failure(CurricularCompetencyErrors.RequirementTypeInvalid);
        }

        // (D-03) Competency domain must be an active CompetencyDomain catalog code for the tenant.
        var domain = await positionCatalog.GetActiveCatalogReferenceByCodeAsync(
            file.TenantId, PositionDescriptionCatalogType.CompetencyDomain, input.CompetencyDomain, cancellationToken);
        if (domain is null)
        {
            return Result<CurricularCompetencyResolved>.Failure(CurricularCompetencyErrors.DomainInvalid);
        }

        // (D-06 + coherence) Experience must be >= 0 and carry a metric when a value is supplied.
        var experience = CurricularCompetencyRules.ValidateExperience(input.ExperienceTimeValue, input.MetricCode);
        if (experience.IsFailure)
        {
            return Result<CurricularCompetencyResolved>.Failure(experience.Error);
        }

        // (D-04) Metric is optional; when supplied it must be an active experience-metric catalog code.
        string? metricCode = null;
        if (!string.IsNullOrWhiteSpace(input.MetricCode))
        {
            if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                    file.TenantId, PersonnelCurriculumCatalogCategories.ExperienceMetric, input.MetricCode, cancellationToken))
            {
                return Result<CurricularCompetencyResolved>.Failure(CurricularCompetencyErrors.MetricInvalid);
            }

            metricCode = input.MetricCode.Trim().ToUpperInvariant();
        }

        // (D-05) No duplicate requirement type + name within the same file (candidate excludes itself on update).
        var siblings = (await employeeRepository.GetCurricularCompetenciesAsync(file.PublicId, cancellationToken))
            .Select(existing => new CurricularCompetencyRules.Existing(
                existing.CurricularCompetencyPublicId,
                CurricularCompetencyRules.Key(existing.RequirementTypeCode, existing.RequirementName)))
            .ToArray();
        var duplicate = CurricularCompetencyRules.CheckDuplicate(candidatePublicId, type.Code, input.RequirementName, siblings);
        if (duplicate.IsFailure)
        {
            return Result<CurricularCompetencyResolved>.Failure(duplicate.Error);
        }

        return Result<CurricularCompetencyResolved>.Success(
            new CurricularCompetencyResolved(type.Code, domain.Code, metricCode));
    }
}

internal sealed class AddPersonnelFileCurricularCompetencyCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionCatalogLookup positionCatalog,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileCurricularCompetencyCommand, PersonnelFileCurricularCompetencyResponse>
{
    public async Task<Result<PersonnelFileCurricularCompetencyResponse>> Handle(
        AddPersonnelFileCurricularCompetencyCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileCurricularCompetencyResponse>(
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
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var catalogValidation = await CurricularCompetencyCommandValidation.ValidateAsync(
            positionCatalog, personnelFileRepository, employeeRepository, personnelFile,
            candidatePublicId: null, command.Item, cancellationToken);
        if (catalogValidation.IsFailure)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(catalogValidation.Error);
        }

        var resolved = catalogValidation.Value;
        var entity = PersonnelFileCurricularCompetency.Create(
            resolved.RequirementTypeCode,
            command.Item.RequirementName,
            resolved.CompetencyDomain,
            command.Item.ExperienceTimeValue,
            resolved.MetricCode,
            command.Item.Notes,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddCurricularCompetencyAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Personnel file curricular competency response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added curricular competency for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileCurricularCompetencyResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileCurricularCompetencyCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionCatalogLookup positionCatalog,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileCurricularCompetencyCommand, PersonnelFileCurricularCompetencyResponse>
{
    public async Task<Result<PersonnelFileCurricularCompetencyResponse>> Handle(
        UpdatePersonnelFileCurricularCompetencyCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileCurricularCompetencyResponse>(
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
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetCurricularCompetencyAsync(personnelFile.PublicId, command.CurricularCompetencyPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var catalogValidation = await CurricularCompetencyCommandValidation.ValidateAsync(
            positionCatalog, personnelFileRepository, employeeRepository, personnelFile,
            command.CurricularCompetencyPublicId, command.Item, cancellationToken);
        if (catalogValidation.IsFailure)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(catalogValidation.Error);
        }

        var resolved = catalogValidation.Value;
        var response = await employeeRepository.UpdateCurricularCompetencyAsync(
            command.CurricularCompetencyPublicId,
            personnelFile.TenantId,
            resolved.RequirementTypeCode,
            command.Item.RequirementName,
            resolved.CompetencyDomain,
            command.Item.ExperienceTimeValue,
            resolved.MetricCode,
            command.Item.Notes,
            command.Item.SourceSystem,
            command.Item.SourceReference,
            command.Item.SourceSyncedUtc,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated curricular competency for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileCurricularCompetencyResponse>.Success(response);
    }
}

internal sealed class PatchPersonnelFileCurricularCompetencyCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPositionCatalogLookup positionCatalog,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<PatchPersonnelFileCurricularCompetencyCommand, PersonnelFileCurricularCompetencyResponse>
{
    public async Task<Result<PersonnelFileCurricularCompetencyResponse>> Handle(
        PatchPersonnelFileCurricularCompetencyCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileCurricularCompetencyResponse>(
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
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetCurricularCompetencyAsync(personnelFile.PublicId, command.CurricularCompetencyPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var state = PersonnelFileCurricularCompetencyPatchState.From(existing);
        var applyResult = PersonnelFileCurricularCompetencyPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileCurricularCompetencyPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Success(existing);
        }

        var input = state.ToInput();
        var catalogValidation = await CurricularCompetencyCommandValidation.ValidateAsync(
            positionCatalog, personnelFileRepository, employeeRepository, personnelFile,
            command.CurricularCompetencyPublicId, input, cancellationToken);
        if (catalogValidation.IsFailure)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(catalogValidation.Error);
        }

        var resolved = catalogValidation.Value;
        var response = await employeeRepository.UpdateCurricularCompetencyAsync(
            command.CurricularCompetencyPublicId,
            personnelFile.TenantId,
            resolved.RequirementTypeCode,
            input.RequirementName,
            resolved.CompetencyDomain,
            input.ExperienceTimeValue,
            resolved.MetricCode,
            input.Notes,
            input.SourceSystem,
            input.SourceReference,
            input.SourceSyncedUtc,
            cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Patched curricular competency for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileCurricularCompetencyResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileCurricularCompetencyCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileCurricularCompetencyCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileCurricularCompetencyCommand command,
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

        var existing = await employeeRepository.GetCurricularCompetencyAsync(personnelFile.PublicId, command.CurricularCompetencyPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var removed = await employeeRepository.DeleteCurricularCompetencyAsync(command.CurricularCompetencyPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deleted curricular competency for {personnelFile.FullName}.", null, cancellationToken);
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

internal sealed class GetPersonnelFileCurricularCompetenciesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFileCurricularCompetenciesQuery, IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>> Handle(
        GetPersonnelFileCurricularCompetenciesQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>(
            query.PersonnelFileId,
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
            return Result<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.GetCurricularCompetenciesAsync(personnelFile.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileCurricularCompetencyByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFileCurricularCompetencyByIdQuery, PersonnelFileCurricularCompetencyResponse>
{
    public async Task<Result<PersonnelFileCurricularCompetencyResponse>> Handle(
        GetPersonnelFileCurricularCompetencyByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<PersonnelFileCurricularCompetencyResponse>(
            query.PersonnelFileId,
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
            return Result<PersonnelFileCurricularCompetencyResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.GetCurricularCompetencyAsync(personnelFile!.PublicId, query.CurricularCompetencyPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileCurricularCompetencyResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileCurricularCompetencyResponse>.Success(response);
    }
}

internal static class PersonnelFileCurricularCompetencyPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PersonnelFileCurricularCompetencyPatchOperation> operations, PersonnelFileCurricularCompetencyPatchState state)
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
                return PersonnelFileTalentPatch.ValidationFailure(operation.Path, "Only root curricular competency properties can be patched.");
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

    public static Result Validate(PersonnelFileCurricularCompetencyPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.RequirementTypeCode))
        {
            errors["requirementTypeCode"] = ["RequirementTypeCode is required."];
        }

        if (string.IsNullOrWhiteSpace(state.RequirementName))
        {
            errors["requirementName"] = ["RequirementName is required."];
        }

        if (string.IsNullOrWhiteSpace(state.CompetencyDomain))
        {
            errors["competencyDomain"] = ["CompetencyDomain is required."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileCurricularCompetencyPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (PersonnelFileTalentPatch.IsSegment(property, "requirementTypeCode"))
        {
            return Mutate(state, () => state.RequirementTypeCode = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "requirementName"))
        {
            return Mutate(state, () => state.RequirementName = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "competencyDomain"))
        {
            return Mutate(state, () => state.CompetencyDomain = isRemove ? string.Empty : PersonnelFileTalentPatch.ReadRequiredString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "experienceTimeValue"))
        {
            return Mutate(state, () => state.ExperienceTimeValue = isRemove ? null : PersonnelFileTalentPatch.ReadNullableDecimal(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "metricCode"))
        {
            return Mutate(state, () => state.MetricCode = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
        }

        if (PersonnelFileTalentPatch.IsSegment(property, "notes"))
        {
            return Mutate(state, () => state.Notes = isRemove ? null : PersonnelFileTalentPatch.ReadNullableString(value, path));
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

    private static Result Mutate(PersonnelFileCurricularCompetencyPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
    }
}

