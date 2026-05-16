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
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;
using static CLARIHR.Application.Features.JobProfiles.JobProfileTrainingCommandSupport;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record GetJobProfileTrainingsQuery(
    Guid JobProfileId,
    int PageNumber = 1,
    int PageSize = JobProfileValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<JobProfileTrainingResponse>>;

public sealed record GetJobProfileTrainingByIdQuery(Guid JobProfileId, Guid TrainingId)
    : IQuery<JobProfileTrainingResponse>;

public sealed record AddJobProfileTrainingCommand(
    Guid JobProfileId,
    Guid? CatalogItemPublicId,
    string? Name,
    string? Notes,
    int SortOrder) : ICommand<JobProfileTrainingResponse>;

public sealed record UpdateJobProfileTrainingCommand(
    Guid JobProfileId,
    Guid TrainingId,
    Guid? CatalogItemPublicId,
    string? Name,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileTrainingResponse>;

public sealed record JobProfileTrainingPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileTrainingCommand(
    Guid JobProfileId,
    Guid TrainingId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<JobProfileTrainingPatchOperation> Operations) : ICommand<JobProfileTrainingResponse>;

public sealed record RemoveJobProfileTrainingCommand(
    Guid JobProfileId,
    Guid TrainingId,
    Guid ConcurrencyToken) : ICommand<JobProfileParentConcurrencyResult>;

internal sealed class GetJobProfileTrainingsQueryValidator : AbstractValidator<GetJobProfileTrainingsQuery>
{
    public GetJobProfileTrainingsQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, JobProfileValidationRules.MaxPageSize);
    }
}

internal sealed class GetJobProfileTrainingByIdQueryValidator : AbstractValidator<GetJobProfileTrainingByIdQuery>
{
    public GetJobProfileTrainingByIdQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
        RuleFor(query => query.TrainingId).NotEmpty();
    }
}

internal sealed class AddJobProfileTrainingCommandValidator : AbstractValidator<AddJobProfileTrainingCommand>
{
    public AddJobProfileTrainingCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemPublicId.HasValue);
        RuleFor(command => command.Name).MaximumLength(300);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateJobProfileTrainingCommandValidator : AbstractValidator<UpdateJobProfileTrainingCommand>
{
    public UpdateJobProfileTrainingCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.TrainingId).NotEmpty();
        RuleFor(command => command.CatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemPublicId.HasValue);
        RuleFor(command => command.Name).MaximumLength(300);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchJobProfileTrainingCommandValidator : AbstractValidator<PatchJobProfileTrainingCommand>
{
    public PatchJobProfileTrainingCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.TrainingId).NotEmpty();
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

internal sealed class RemoveJobProfileTrainingCommandValidator : AbstractValidator<RemoveJobProfileTrainingCommand>
{
    public RemoveJobProfileTrainingCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.TrainingId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetJobProfileTrainingsQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileTrainingsQuery, PagedResponse<JobProfileTrainingResponse>>
{
    public async Task<Result<PagedResponse<JobProfileTrainingResponse>>> Handle(GetJobProfileTrainingsQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PagedResponse<JobProfileTrainingResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<JobProfileTrainingResponse>>.Failure(authorizationResult.Error);
        }

        var trainings = await repository.GetTrainingResponsesByProfileIdAsync(
            query.JobProfileId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        if (trainings is null)
        {
            return Result<PagedResponse<JobProfileTrainingResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.JobProfileNotFound);
        }

        return Result<PagedResponse<JobProfileTrainingResponse>>.Success(trainings);
    }
}

internal sealed class GetJobProfileTrainingByIdQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileTrainingByIdQuery, JobProfileTrainingResponse>
{
    public async Task<Result<JobProfileTrainingResponse>> Handle(GetJobProfileTrainingByIdQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileTrainingResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileTrainingResponse>.Failure(authorizationResult.Error);
        }

        var training = await repository.GetTrainingResponseAsync(query.JobProfileId, query.TrainingId, cancellationToken);
        if (training is null)
        {
            return Result<JobProfileTrainingResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.TrainingNotFound);
        }

        return Result<JobProfileTrainingResponse>.Success(training);
    }
}

internal sealed class AddJobProfileTrainingCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<AddJobProfileTrainingCommand, JobProfileTrainingResponse>
{
    public async Task<Result<JobProfileTrainingResponse>> Handle(AddJobProfileTrainingCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileTrainingResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileTrainingResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithTrainingsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileTrainingResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var catalogItemResult = await ResolveCatalogItemAsync(command.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileTrainingResponse>.Failure(catalogItemResult.Error);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItemResult.Value?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileTrainingResponse>.Failure(JobProfileErrors.TrainingNameRequired);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var training = JobProfileTraining.Create(
                catalogItemResult.Value?.Id,
                catalogItemResult.Value,
                name,
                command.Notes,
                command.SortOrder);

            profile.AddTraining(training);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetTrainingResponseAsync(profile.PublicId, training.PublicId, cancellationToken)
                ?? training.ToResponse(command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added training to job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileTrainingResponse>.Success(response);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileTrainingResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobProfileTrainingCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<UpdateJobProfileTrainingCommand, JobProfileTrainingResponse>
{
    public async Task<Result<JobProfileTrainingResponse>> Handle(UpdateJobProfileTrainingCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileTrainingResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileTrainingResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithTrainingsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileTrainingResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var training = profile.Trainings.FirstOrDefault(item => item.PublicId == command.TrainingId);
        if (training is null)
        {
            return Result<JobProfileTrainingResponse>.Failure(JobProfileErrors.TrainingNotFound);
        }

        if (training.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileTrainingResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var catalogItemResult = await ResolveCatalogItemAsync(command.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileTrainingResponse>.Failure(catalogItemResult.Error);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItemResult.Value?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileTrainingResponse>.Failure(JobProfileErrors.TrainingNameRequired);
        }

        var before = await repository.GetTrainingResponseAsync(profile.PublicId, training.PublicId, cancellationToken)
            ?? training.ToResponse(command.CatalogItemPublicId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            training.Update(
                catalogItemResult.Value?.Id,
                catalogItemResult.Value,
                name,
                command.Notes,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetTrainingResponseAsync(profile.PublicId, training.PublicId, cancellationToken)
                ?? training.ToResponse(command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated training in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileTrainingResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileTrainingResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchJobProfileTrainingCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<PatchJobProfileTrainingCommand, JobProfileTrainingResponse>
{
    public async Task<Result<JobProfileTrainingResponse>> Handle(PatchJobProfileTrainingCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileTrainingResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileTrainingResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithTrainingsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileTrainingResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var training = profile.Trainings.FirstOrDefault(item => item.PublicId == command.TrainingId);
        if (training is null)
        {
            return Result<JobProfileTrainingResponse>.Failure(JobProfileErrors.TrainingNotFound);
        }

        if (training.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileTrainingResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetTrainingResponseAsync(profile.PublicId, training.PublicId, cancellationToken)
            ?? training.ToResponse();
        var patchState = JobProfileTrainingPatchState.From(before);
        var patchApplication = JobProfileTrainingPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<JobProfileTrainingResponse>.Failure(patchApplication.Error);
        }

        var validation = JobProfileTrainingPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<JobProfileTrainingResponse>.Failure(validation.Error);
        }

        if (!patchState.HasMutation)
        {
            return Result<JobProfileTrainingResponse>.Success(before);
        }

        var catalogItemResult = await ResolveCatalogItemAsync(patchState.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileTrainingResponse>.Failure(catalogItemResult.Error);
        }

        var name = string.IsNullOrWhiteSpace(patchState.Name)
            ? catalogItemResult.Value?.Name ?? string.Empty
            : patchState.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileTrainingResponse>.Failure(JobProfileErrors.TrainingNameRequired);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            training.Update(
                catalogItemResult.Value?.Id,
                catalogItemResult.Value,
                name,
                patchState.Notes,
                patchState.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetTrainingResponseAsync(profile.PublicId, training.PublicId, cancellationToken)
                ?? training.ToResponse(patchState.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Patched training in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileTrainingResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileTrainingResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobProfileTrainingCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileTrainingCommand, JobProfileParentConcurrencyResult>
{
    public async Task<Result<JobProfileParentConcurrencyResult>> Handle(RemoveJobProfileTrainingCommand command, CancellationToken cancellationToken)
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

        var profile = await repository.GetWithTrainingsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var training = profile.Trainings.FirstOrDefault(item => item.PublicId == command.TrainingId);
        if (training is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(JobProfileErrors.TrainingNotFound);
        }

        if (training.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetTrainingResponseAsync(profile.PublicId, training.PublicId, cancellationToken)
            ?? training.ToResponse();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.RemoveTraining(training);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed training from job profile {profile.Code}.",
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

internal static class JobProfileTrainingCommandSupport
{
    public static async Task<Result<JobCatalogItem?>> ResolveCatalogItemAsync(
        Guid? catalogItemPublicId,
        IJobCatalogRepository catalogRepository,
        CancellationToken cancellationToken)
    {
        if (!catalogItemPublicId.HasValue)
        {
            return Result<JobCatalogItem?>.Success(null);
        }

        var catalogItem = await catalogRepository.GetByIdAsync(catalogItemPublicId.Value, cancellationToken);
        return catalogItem is null
            ? Result<JobCatalogItem?>.Failure(JobProfileErrors.CatalogItemNotFound)
            : Result<JobCatalogItem?>.Success(catalogItem);
    }
}

internal sealed class JobProfileTrainingPatchState
{
    public Guid? CatalogItemPublicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public bool HasMutation { get; set; }

    public static JobProfileTrainingPatchState From(JobProfileTrainingResponse response) =>
        new()
        {
            CatalogItemPublicId = response.CatalogItemPublicId,
            Name = response.Name,
            Notes = response.Notes,
            SortOrder = response.SortOrder
        };
}

internal static class JobProfileTrainingPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfileTrainingPatchOperation> operations, JobProfileTrainingPatchState state)
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
                return ValidationFailure(operation.Path, "Only root training properties can be patched.");
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

    public static Result Validate(JobProfileTrainingPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (state.CatalogItemPublicId == Guid.Empty)
        {
            errors["catalogItemPublicId"] = ["CatalogItemPublicId must be a valid UUID."];
        }

        if (!string.IsNullOrEmpty(state.Name) && state.Name.Length > 300)
        {
            errors["name"] = ["Name must be 300 characters or fewer."];
        }

        if (state.Notes is not null && state.Notes.Length > 1000)
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
        JobProfileTrainingPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "catalogItemPublicId"))
        {
            state.CatalogItemPublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "name"))
        {
            state.Name = isRemove ? string.Empty : ReadRequiredString(value, path);
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
