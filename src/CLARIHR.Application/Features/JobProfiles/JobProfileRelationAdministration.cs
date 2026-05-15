using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;
using static CLARIHR.Application.Features.JobProfiles.JobProfileRelationCommandSupport;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record GetJobProfileRelationsQuery(Guid JobProfileId)
    : IQuery<IReadOnlyCollection<JobProfileRelationResponse>>;

public sealed record GetJobProfileRelationByIdQuery(Guid JobProfileId, Guid RelationId)
    : IQuery<JobProfileRelationResponse>;

public sealed record AddJobProfileRelationCommand(
    Guid JobProfileId,
    JobRelationType RelationType,
    Guid? CatalogItemPublicId,
    string Counterpart,
    string? Notes,
    int SortOrder) : ICommand<JobProfileRelationResponse>;

public sealed record UpdateJobProfileRelationCommand(
    Guid JobProfileId,
    Guid RelationId,
    JobRelationType RelationType,
    Guid? CatalogItemPublicId,
    string Counterpart,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileRelationResponse>;

public sealed record JobProfileRelationPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileRelationCommand(
    Guid JobProfileId,
    Guid RelationId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<JobProfileRelationPatchOperation> Operations) : ICommand<JobProfileRelationResponse>;

public sealed record RemoveJobProfileRelationCommand(
    Guid JobProfileId,
    Guid RelationId,
    Guid ConcurrencyToken) : ICommand<JobProfileParentConcurrencyResult>;

internal sealed class GetJobProfileRelationsQueryValidator : AbstractValidator<GetJobProfileRelationsQuery>
{
    public GetJobProfileRelationsQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class GetJobProfileRelationByIdQueryValidator : AbstractValidator<GetJobProfileRelationByIdQuery>
{
    public GetJobProfileRelationByIdQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
        RuleFor(query => query.RelationId).NotEmpty();
    }
}

internal sealed class AddJobProfileRelationCommandValidator : AbstractValidator<AddJobProfileRelationCommand>
{
    public AddJobProfileRelationCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RelationType).IsInEnum();
        RuleFor(command => command.CatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemPublicId.HasValue);
        RuleFor(command => command.Counterpart).NotEmpty().MaximumLength(500);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateJobProfileRelationCommandValidator : AbstractValidator<UpdateJobProfileRelationCommand>
{
    public UpdateJobProfileRelationCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RelationId).NotEmpty();
        RuleFor(command => command.RelationType).IsInEnum();
        RuleFor(command => command.CatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemPublicId.HasValue);
        RuleFor(command => command.Counterpart).NotEmpty().MaximumLength(500);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchJobProfileRelationCommandValidator : AbstractValidator<PatchJobProfileRelationCommand>
{
    public PatchJobProfileRelationCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RelationId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class RemoveJobProfileRelationCommandValidator : AbstractValidator<RemoveJobProfileRelationCommand>
{
    public RemoveJobProfileRelationCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RelationId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetJobProfileRelationsQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileRelationsQuery, IReadOnlyCollection<JobProfileRelationResponse>>
{
    public async Task<Result<IReadOnlyCollection<JobProfileRelationResponse>>> Handle(GetJobProfileRelationsQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IReadOnlyCollection<JobProfileRelationResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<JobProfileRelationResponse>>.Failure(authorizationResult.Error);
        }

        var relations = await repository.GetRelationResponsesByProfileIdAsync(query.JobProfileId, cancellationToken);
        if (relations is null)
        {
            return Result<IReadOnlyCollection<JobProfileRelationResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.JobProfileNotFound);
        }

        return Result<IReadOnlyCollection<JobProfileRelationResponse>>.Success(relations);
    }
}

internal sealed class GetJobProfileRelationByIdQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileRelationByIdQuery, JobProfileRelationResponse>
{
    public async Task<Result<JobProfileRelationResponse>> Handle(GetJobProfileRelationByIdQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileRelationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileRelationResponse>.Failure(authorizationResult.Error);
        }

        var relation = await repository.GetRelationResponseAsync(query.JobProfileId, query.RelationId, cancellationToken);
        if (relation is null)
        {
            return Result<JobProfileRelationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.RelationNotFound);
        }

        return Result<JobProfileRelationResponse>.Success(relation);
    }
}

internal sealed class AddJobProfileRelationCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<AddJobProfileRelationCommand, JobProfileRelationResponse>
{
    public async Task<Result<JobProfileRelationResponse>> Handle(AddJobProfileRelationCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileRelationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileRelationResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithRelationsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileRelationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var catalogItemResult = await ResolveCatalogItemInternalIdAsync(command.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileRelationResponse>.Failure(catalogItemResult.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var relation = JobProfileRelation.Create(
                command.RelationType,
                catalogItemResult.Value,
                null,
                command.Counterpart,
                command.Notes,
                command.SortOrder);

            profile.AddRelation(relation);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetRelationResponseAsync(profile.PublicId, relation.PublicId, cancellationToken)
                ?? relation.ToResponse(command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added relation to job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileRelationResponse>.Success(response);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileRelationResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobProfileRelationCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<UpdateJobProfileRelationCommand, JobProfileRelationResponse>
{
    public async Task<Result<JobProfileRelationResponse>> Handle(UpdateJobProfileRelationCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileRelationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileRelationResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithRelationsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileRelationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var relation = profile.Relations.FirstOrDefault(item => item.PublicId == command.RelationId);
        if (relation is null)
        {
            return Result<JobProfileRelationResponse>.Failure(JobProfileErrors.RelationNotFound);
        }

        if (relation.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileRelationResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var catalogItemResult = await ResolveCatalogItemInternalIdAsync(command.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileRelationResponse>.Failure(catalogItemResult.Error);
        }

        var before = await repository.GetRelationResponseAsync(profile.PublicId, relation.PublicId, cancellationToken)
            ?? relation.ToResponse(command.CatalogItemPublicId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            relation.Update(
                command.RelationType,
                catalogItemResult.Value,
                null,
                command.Counterpart,
                command.Notes,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetRelationResponseAsync(profile.PublicId, relation.PublicId, cancellationToken)
                ?? relation.ToResponse(command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated relation in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileRelationResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileRelationResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchJobProfileRelationCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<PatchJobProfileRelationCommand, JobProfileRelationResponse>
{
    public async Task<Result<JobProfileRelationResponse>> Handle(PatchJobProfileRelationCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileRelationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileRelationResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithRelationsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileRelationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var relation = profile.Relations.FirstOrDefault(item => item.PublicId == command.RelationId);
        if (relation is null)
        {
            return Result<JobProfileRelationResponse>.Failure(JobProfileErrors.RelationNotFound);
        }

        if (relation.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileRelationResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetRelationResponseAsync(profile.PublicId, relation.PublicId, cancellationToken)
            ?? relation.ToResponse(null);
        var patchState = JobProfileRelationPatchState.From(before);
        var patchApplication = JobProfileRelationPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<JobProfileRelationResponse>.Failure(patchApplication.Error);
        }

        var validation = JobProfileRelationPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<JobProfileRelationResponse>.Failure(validation.Error);
        }

        if (!patchState.HasMutation)
        {
            return Result<JobProfileRelationResponse>.Success(before);
        }

        var catalogItemResult = await ResolveCatalogItemInternalIdAsync(patchState.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileRelationResponse>.Failure(catalogItemResult.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            relation.Update(
                patchState.RelationType,
                catalogItemResult.Value,
                null,
                patchState.Counterpart,
                patchState.Notes,
                patchState.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetRelationResponseAsync(profile.PublicId, relation.PublicId, cancellationToken)
                ?? relation.ToResponse(patchState.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Patched relation in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileRelationResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileRelationResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobProfileRelationCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileRelationCommand, JobProfileParentConcurrencyResult>
{
    public async Task<Result<JobProfileParentConcurrencyResult>> Handle(RemoveJobProfileRelationCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithRelationsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var relation = profile.Relations.FirstOrDefault(item => item.PublicId == command.RelationId);
        if (relation is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(JobProfileErrors.RelationNotFound);
        }

        if (relation.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetRelationResponseAsync(profile.PublicId, relation.PublicId, cancellationToken)
            ?? relation.ToResponse(null);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.RemoveRelation(relation);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed relation from job profile {profile.Code}.",
                    Before: before),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileParentConcurrencyResult>.Success(new JobProfileParentConcurrencyResult(profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileParentConcurrencyResult>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class JobProfileRelationPatchState
{
    public JobRelationType RelationType { get; set; }
    public Guid? CatalogItemPublicId { get; set; }
    public string Counterpart { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public bool HasMutation { get; set; }

    public static JobProfileRelationPatchState From(JobProfileRelationResponse response) =>
        new()
        {
            RelationType = response.RelationType,
            CatalogItemPublicId = response.CatalogItemPublicId,
            Counterpart = response.Counterpart,
            Notes = response.Notes,
            SortOrder = response.SortOrder
        };
}

internal static class JobProfileRelationPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfileRelationPatchOperation> operations, JobProfileRelationPatchState state)
    {
        foreach (var operation in operations)
        {
            var op = operation.Op.Trim();
            if (!SupportedOperations.Contains(op))
            {
                return ValidationFailure(operation.Path, $"Unsupported JSON Patch operation '{operation.Op}'.");
            }

            var segments = ParsePath(operation.Path);
            if (segments.Length != 1)
            {
                return ValidationFailure(operation.Path, "Only root relation properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (JobProfilePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(JobProfileRelationPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (state.CatalogItemPublicId == Guid.Empty)
        {
            errors["catalogItemPublicId"] = ["CatalogItemPublicId must be a valid UUID."];
        }

        if (string.IsNullOrWhiteSpace(state.Counterpart))
        {
            errors["counterpart"] = ["Counterpart is required."];
        }
        else if (state.Counterpart.Length > 500)
        {
            errors["counterpart"] = ["Counterpart must be 500 characters or fewer."];
        }

        if (state.Notes is { Length: > 1000 })
        {
            errors["notes"] = ["Notes must be 1000 characters or fewer."];
        }

        if (state.SortOrder < 0)
        {
            errors["sortOrder"] = ["SortOrder must be greater than or equal to 0."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        JobProfileRelationPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "relationType"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "RelationType cannot be removed.");
            }

            state.RelationType = ReadRelationType(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsAnySegment(property, "catalogItemPublicId", "catalogItemId"))
        {
            state.CatalogItemPublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "counterpart"))
        {
            state.Counterpart = isRemove ? string.Empty : ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "notes"))
        {
            state.Notes = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "sortOrder"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "SortOrder cannot be removed.");
            }

            state.SortOrder = ReadInt(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static JobRelationType ReadRelationType(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new JobProfilePatchValueException(path, "RelationType is required.");
        }

        if (value!.Value.ValueKind == JsonValueKind.String)
        {
            var raw = value.Value.GetString();
            return Enum.TryParse<JobRelationType>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(typeof(JobRelationType), parsed)
                ? parsed
                : throw new JobProfilePatchValueException(path, $"RelationType '{raw}' is not a valid value.");
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var numeric))
        {
            var parsed = (JobRelationType)numeric;
            return Enum.IsDefined(typeof(JobRelationType), parsed)
                ? parsed
                : throw new JobProfilePatchValueException(path, $"RelationType '{numeric}' is not a valid value.");
        }

        throw new JobProfilePatchValueException(path, "RelationType value must be a string or integer.");
    }

    private static string[] ParsePath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(UnescapeJsonPointerSegment)
            .ToArray();

    private static string UnescapeJsonPointerSegment(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

    private static bool IsNull(JsonElement? value) =>
        !value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    private static bool IsSegment(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsAnySegment(string actual, params string[] expected) =>
        expected.Any(item => IsSegment(actual, item));

    private static string ReadRequiredString(JsonElement? value, string path) =>
        ReadNullableString(value, path) ?? string.Empty;

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new JobProfilePatchValueException(path, "Value must be a string or null.");
    }

    private static Guid ReadRequiredGuid(JsonElement? value, string path) =>
        ReadNullableGuid(value, path) ?? Guid.Empty;

    private static Guid? ReadNullableGuid(JsonElement? value, string path)
    {
        var raw = ReadNullableString(value, path);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return Guid.TryParse(raw, out var parsed)
            ? parsed
            : throw new JobProfilePatchValueException(path, "Value must be a valid UUID.");
    }

    private static int ReadInt(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new JobProfilePatchValueException(path, "Value must be an integer.");
        }

        if (value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var parsed))
        {
            return parsed;
        }

        var raw = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out parsed))
        {
            return parsed;
        }

        throw new JobProfilePatchValueException(path, "Value must be an integer.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

internal static class JobProfileRelationCommandSupport
{
    public static async Task<Result<long?>> ResolveCatalogItemInternalIdAsync(
        Guid? catalogItemPublicId,
        IJobCatalogRepository catalogRepository,
        CancellationToken cancellationToken)
    {
        if (!catalogItemPublicId.HasValue)
        {
            return Result<long?>.Success(null);
        }

        var catalogItem = await catalogRepository.GetByIdAsync(catalogItemPublicId.Value, cancellationToken);
        if (catalogItem is null)
        {
            return Result<long?>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        return Result<long?>.Success(catalogItem.Id);
    }
}
