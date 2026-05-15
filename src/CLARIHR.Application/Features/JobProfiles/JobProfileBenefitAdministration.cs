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
using static CLARIHR.Application.Features.JobProfiles.JobProfileBenefitCommandSupport;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record GetJobProfileBenefitsQuery(Guid JobProfileId)
    : IQuery<IReadOnlyCollection<JobProfileBenefitResponse>>;

public sealed record GetJobProfileBenefitByIdQuery(Guid JobProfileId, Guid BenefitId)
    : IQuery<JobProfileBenefitResponse>;

public sealed record AddJobProfileBenefitCommand(
    Guid JobProfileId,
    Guid? CatalogItemPublicId,
    string? Name,
    string? Notes,
    int SortOrder) : ICommand<JobProfileBenefitResponse>;

public sealed record UpdateJobProfileBenefitCommand(
    Guid JobProfileId,
    Guid BenefitId,
    Guid? CatalogItemPublicId,
    string? Name,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileBenefitResponse>;

public sealed record JobProfileBenefitPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileBenefitCommand(
    Guid JobProfileId,
    Guid BenefitId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<JobProfileBenefitPatchOperation> Operations) : ICommand<JobProfileBenefitResponse>;

public sealed record RemoveJobProfileBenefitCommand(
    Guid JobProfileId,
    Guid BenefitId,
    Guid ConcurrencyToken) : ICommand<JobProfileParentConcurrencyResult>;

internal sealed class GetJobProfileBenefitsQueryValidator : AbstractValidator<GetJobProfileBenefitsQuery>
{
    public GetJobProfileBenefitsQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class GetJobProfileBenefitByIdQueryValidator : AbstractValidator<GetJobProfileBenefitByIdQuery>
{
    public GetJobProfileBenefitByIdQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
        RuleFor(query => query.BenefitId).NotEmpty();
    }
}

internal sealed class AddJobProfileBenefitCommandValidator : AbstractValidator<AddJobProfileBenefitCommand>
{
    public AddJobProfileBenefitCommandValidator()
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

internal sealed class UpdateJobProfileBenefitCommandValidator : AbstractValidator<UpdateJobProfileBenefitCommand>
{
    public UpdateJobProfileBenefitCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.BenefitId).NotEmpty();
        RuleFor(command => command.CatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemPublicId.HasValue);
        RuleFor(command => command.Name).MaximumLength(300);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchJobProfileBenefitCommandValidator : AbstractValidator<PatchJobProfileBenefitCommand>
{
    public PatchJobProfileBenefitCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.BenefitId).NotEmpty();
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

internal sealed class RemoveJobProfileBenefitCommandValidator : AbstractValidator<RemoveJobProfileBenefitCommand>
{
    public RemoveJobProfileBenefitCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.BenefitId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetJobProfileBenefitsQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileBenefitsQuery, IReadOnlyCollection<JobProfileBenefitResponse>>
{
    public async Task<Result<IReadOnlyCollection<JobProfileBenefitResponse>>> Handle(GetJobProfileBenefitsQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IReadOnlyCollection<JobProfileBenefitResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<JobProfileBenefitResponse>>.Failure(authorizationResult.Error);
        }

        var benefits = await repository.GetBenefitResponsesByProfileIdAsync(query.JobProfileId, cancellationToken);
        if (benefits is null)
        {
            return Result<IReadOnlyCollection<JobProfileBenefitResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.JobProfileNotFound);
        }

        return Result<IReadOnlyCollection<JobProfileBenefitResponse>>.Success(benefits);
    }
}

internal sealed class GetJobProfileBenefitByIdQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileBenefitByIdQuery, JobProfileBenefitResponse>
{
    public async Task<Result<JobProfileBenefitResponse>> Handle(GetJobProfileBenefitByIdQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileBenefitResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileBenefitResponse>.Failure(authorizationResult.Error);
        }

        var benefit = await repository.GetBenefitResponseAsync(query.JobProfileId, query.BenefitId, cancellationToken);
        if (benefit is null)
        {
            return Result<JobProfileBenefitResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.BenefitNotFound);
        }

        return Result<JobProfileBenefitResponse>.Success(benefit);
    }
}

internal sealed class AddJobProfileBenefitCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<AddJobProfileBenefitCommand, JobProfileBenefitResponse>
{
    public async Task<Result<JobProfileBenefitResponse>> Handle(AddJobProfileBenefitCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileBenefitResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileBenefitResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithBenefitsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileBenefitResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var catalogItemResult = await ResolveCatalogItemAsync(command.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileBenefitResponse>.Failure(catalogItemResult.Error);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItemResult.Value?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileBenefitResponse>.Failure(JobProfileErrors.BenefitNameRequired);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var benefit = JobProfileBenefit.Create(
                catalogItemResult.Value?.Id,
                catalogItemResult.Value,
                name,
                command.Notes,
                command.SortOrder);

            profile.AddBenefit(benefit);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetBenefitResponseAsync(profile.PublicId, benefit.PublicId, cancellationToken)
                ?? benefit.ToResponse(command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added benefit to job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileBenefitResponse>.Success(response);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileBenefitResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobProfileBenefitCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<UpdateJobProfileBenefitCommand, JobProfileBenefitResponse>
{
    public async Task<Result<JobProfileBenefitResponse>> Handle(UpdateJobProfileBenefitCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileBenefitResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileBenefitResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithBenefitsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileBenefitResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var benefit = profile.Benefits.FirstOrDefault(item => item.PublicId == command.BenefitId);
        if (benefit is null)
        {
            return Result<JobProfileBenefitResponse>.Failure(JobProfileErrors.BenefitNotFound);
        }

        if (benefit.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileBenefitResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var catalogItemResult = await ResolveCatalogItemAsync(command.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileBenefitResponse>.Failure(catalogItemResult.Error);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItemResult.Value?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileBenefitResponse>.Failure(JobProfileErrors.BenefitNameRequired);
        }

        var before = await repository.GetBenefitResponseAsync(profile.PublicId, benefit.PublicId, cancellationToken)
            ?? benefit.ToResponse(command.CatalogItemPublicId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            benefit.Update(
                catalogItemResult.Value?.Id,
                catalogItemResult.Value,
                name,
                command.Notes,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetBenefitResponseAsync(profile.PublicId, benefit.PublicId, cancellationToken)
                ?? benefit.ToResponse(command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated benefit in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileBenefitResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileBenefitResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchJobProfileBenefitCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<PatchJobProfileBenefitCommand, JobProfileBenefitResponse>
{
    public async Task<Result<JobProfileBenefitResponse>> Handle(PatchJobProfileBenefitCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileBenefitResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileBenefitResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithBenefitsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileBenefitResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var benefit = profile.Benefits.FirstOrDefault(item => item.PublicId == command.BenefitId);
        if (benefit is null)
        {
            return Result<JobProfileBenefitResponse>.Failure(JobProfileErrors.BenefitNotFound);
        }

        if (benefit.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileBenefitResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetBenefitResponseAsync(profile.PublicId, benefit.PublicId, cancellationToken)
            ?? benefit.ToResponse();
        var patchState = JobProfileBenefitPatchState.From(before);
        var patchApplication = JobProfileBenefitPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<JobProfileBenefitResponse>.Failure(patchApplication.Error);
        }

        var validation = JobProfileBenefitPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<JobProfileBenefitResponse>.Failure(validation.Error);
        }

        if (!patchState.HasMutation)
        {
            return Result<JobProfileBenefitResponse>.Success(before);
        }

        var catalogItemResult = await ResolveCatalogItemAsync(patchState.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileBenefitResponse>.Failure(catalogItemResult.Error);
        }

        var name = string.IsNullOrWhiteSpace(patchState.Name)
            ? catalogItemResult.Value?.Name ?? string.Empty
            : patchState.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileBenefitResponse>.Failure(JobProfileErrors.BenefitNameRequired);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            benefit.Update(
                catalogItemResult.Value?.Id,
                catalogItemResult.Value,
                name,
                patchState.Notes,
                patchState.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetBenefitResponseAsync(profile.PublicId, benefit.PublicId, cancellationToken)
                ?? benefit.ToResponse(patchState.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Patched benefit in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileBenefitResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileBenefitResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobProfileBenefitCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileBenefitCommand, JobProfileParentConcurrencyResult>
{
    public async Task<Result<JobProfileParentConcurrencyResult>> Handle(RemoveJobProfileBenefitCommand command, CancellationToken cancellationToken)
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

        var profile = await repository.GetWithBenefitsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var benefit = profile.Benefits.FirstOrDefault(item => item.PublicId == command.BenefitId);
        if (benefit is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(JobProfileErrors.BenefitNotFound);
        }

        if (benefit.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetBenefitResponseAsync(profile.PublicId, benefit.PublicId, cancellationToken)
            ?? benefit.ToResponse();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.RemoveBenefit(benefit);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed benefit from job profile {profile.Code}.",
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

internal static class JobProfileBenefitCommandSupport
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

internal sealed class JobProfileBenefitPatchState
{
    public Guid? CatalogItemPublicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public bool HasMutation { get; set; }

    public static JobProfileBenefitPatchState From(JobProfileBenefitResponse response) =>
        new()
        {
            CatalogItemPublicId = response.CatalogItemPublicId,
            Name = response.Name,
            Notes = response.Notes,
            SortOrder = response.SortOrder
        };
}

internal static class JobProfileBenefitPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfileBenefitPatchOperation> operations, JobProfileBenefitPatchState state)
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
                return ValidationFailure(operation.Path, "Only root benefit properties can be patched.");
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

    public static Result Validate(JobProfileBenefitPatchState state)
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
        JobProfileBenefitPatchState state,
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
