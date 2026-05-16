using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using FluentValidation;
using static CLARIHR.Application.Features.JobProfiles.JobProfileWorkingConditionCommandSupport;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record GetJobProfileWorkingConditionsQuery(
    Guid JobProfileId,
    int PageNumber = 1,
    int PageSize = JobProfileValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<JobProfileWorkingConditionResponse>>;

public sealed record GetJobProfileWorkingConditionByIdQuery(Guid JobProfileId, Guid WorkingConditionId)
    : IQuery<JobProfileWorkingConditionResponse>;

public sealed record AddJobProfileWorkingConditionCommand(
    Guid JobProfileId,
    Guid? WorkConditionTypeCatalogItemPublicId,
    Guid? CatalogItemPublicId,
    string? Name,
    string? Notes,
    int SortOrder) : ICommand<JobProfileWorkingConditionResponse>;

public sealed record UpdateJobProfileWorkingConditionCommand(
    Guid JobProfileId,
    Guid WorkingConditionId,
    Guid? WorkConditionTypeCatalogItemPublicId,
    Guid? CatalogItemPublicId,
    string? Name,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileWorkingConditionResponse>;

public sealed record JobProfileWorkingConditionPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileWorkingConditionCommand(
    Guid JobProfileId,
    Guid WorkingConditionId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<JobProfileWorkingConditionPatchOperation> Operations) : ICommand<JobProfileWorkingConditionResponse>;

public sealed record RemoveJobProfileWorkingConditionCommand(
    Guid JobProfileId,
    Guid WorkingConditionId,
    Guid ConcurrencyToken) : ICommand<JobProfileParentConcurrencyResult>;

internal sealed class GetJobProfileWorkingConditionsQueryValidator : AbstractValidator<GetJobProfileWorkingConditionsQuery>
{
    public GetJobProfileWorkingConditionsQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, JobProfileValidationRules.MaxPageSize);
    }
}

internal sealed class GetJobProfileWorkingConditionByIdQueryValidator : AbstractValidator<GetJobProfileWorkingConditionByIdQuery>
{
    public GetJobProfileWorkingConditionByIdQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
        RuleFor(query => query.WorkingConditionId).NotEmpty();
    }
}

internal sealed class AddJobProfileWorkingConditionCommandValidator : AbstractValidator<AddJobProfileWorkingConditionCommand>
{
    public AddJobProfileWorkingConditionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.WorkConditionTypeCatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.WorkConditionTypeCatalogItemPublicId.HasValue);
        RuleFor(command => command.CatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemPublicId.HasValue);
        RuleFor(command => command.Name).MaximumLength(300);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateJobProfileWorkingConditionCommandValidator : AbstractValidator<UpdateJobProfileWorkingConditionCommand>
{
    public UpdateJobProfileWorkingConditionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.WorkingConditionId).NotEmpty();
        RuleFor(command => command.WorkConditionTypeCatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.WorkConditionTypeCatalogItemPublicId.HasValue);
        RuleFor(command => command.CatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemPublicId.HasValue);
        RuleFor(command => command.Name).MaximumLength(300);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchJobProfileWorkingConditionCommandValidator : AbstractValidator<PatchJobProfileWorkingConditionCommand>
{
    public PatchJobProfileWorkingConditionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.WorkingConditionId).NotEmpty();
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

internal sealed class RemoveJobProfileWorkingConditionCommandValidator : AbstractValidator<RemoveJobProfileWorkingConditionCommand>
{
    public RemoveJobProfileWorkingConditionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.WorkingConditionId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetJobProfileWorkingConditionsQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileWorkingConditionsQuery, PagedResponse<JobProfileWorkingConditionResponse>>
{
    public async Task<Result<PagedResponse<JobProfileWorkingConditionResponse>>> Handle(GetJobProfileWorkingConditionsQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PagedResponse<JobProfileWorkingConditionResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<JobProfileWorkingConditionResponse>>.Failure(authorizationResult.Error);
        }

        var conditions = await repository.GetWorkingConditionResponsesByProfileIdAsync(
            query.JobProfileId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        if (conditions is null)
        {
            return Result<PagedResponse<JobProfileWorkingConditionResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.JobProfileNotFound);
        }

        return Result<PagedResponse<JobProfileWorkingConditionResponse>>.Success(conditions);
    }
}

internal sealed class GetJobProfileWorkingConditionByIdQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileWorkingConditionByIdQuery, JobProfileWorkingConditionResponse>
{
    public async Task<Result<JobProfileWorkingConditionResponse>> Handle(GetJobProfileWorkingConditionByIdQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(authorizationResult.Error);
        }

        var workingCondition = await repository.GetWorkingConditionResponseAsync(query.JobProfileId, query.WorkingConditionId, cancellationToken);
        if (workingCondition is null)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.WorkingConditionNotFound);
        }

        return Result<JobProfileWorkingConditionResponse>.Success(workingCondition);
    }
}

internal sealed class AddJobProfileWorkingConditionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository)
    : ICommandHandler<AddJobProfileWorkingConditionCommand, JobProfileWorkingConditionResponse>
{
    public async Task<Result<JobProfileWorkingConditionResponse>> Handle(AddJobProfileWorkingConditionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithWorkingConditionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var workConditionTypeInternalIdResult = await ResolveWorkConditionTypeInternalIdAsync(
            tenantContext.TenantId.Value,
            command.WorkConditionTypeCatalogItemPublicId,
            positionDescriptionCatalogRepository,
            cancellationToken);

        if (workConditionTypeInternalIdResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(workConditionTypeInternalIdResult.Error);
        }

        var catalogItemResult = await ResolveCatalogItemAsync(command.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(catalogItemResult.Error);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItemResult.Value?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(JobProfileErrors.WorkingConditionNameRequired);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var workingCondition = JobProfileWorkingCondition.Create(
                workConditionTypeInternalIdResult.Value,
                catalogItemResult.Value?.Id,
                catalogItemResult.Value,
                name,
                command.Notes,
                command.SortOrder);

            profile.AddWorkingCondition(workingCondition);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetWorkingConditionResponseAsync(profile.PublicId, workingCondition.PublicId, cancellationToken)
                ?? workingCondition.ToResponse(command.WorkConditionTypeCatalogItemPublicId, command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added working condition to job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileWorkingConditionResponse>.Success(response);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileWorkingConditionResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobProfileWorkingConditionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository)
    : ICommandHandler<UpdateJobProfileWorkingConditionCommand, JobProfileWorkingConditionResponse>
{
    public async Task<Result<JobProfileWorkingConditionResponse>> Handle(UpdateJobProfileWorkingConditionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithWorkingConditionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var workingCondition = profile.WorkingConditions.FirstOrDefault(item => item.PublicId == command.WorkingConditionId);
        if (workingCondition is null)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(JobProfileErrors.WorkingConditionNotFound);
        }

        if (workingCondition.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var workConditionTypeInternalIdResult = await ResolveWorkConditionTypeInternalIdAsync(
            tenantContext.TenantId.Value,
            command.WorkConditionTypeCatalogItemPublicId,
            positionDescriptionCatalogRepository,
            cancellationToken);

        if (workConditionTypeInternalIdResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(workConditionTypeInternalIdResult.Error);
        }

        var catalogItemResult = await ResolveCatalogItemAsync(command.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(catalogItemResult.Error);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItemResult.Value?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(JobProfileErrors.WorkingConditionNameRequired);
        }

        var before = await repository.GetWorkingConditionResponseAsync(profile.PublicId, workingCondition.PublicId, cancellationToken)
            ?? workingCondition.ToResponse(command.WorkConditionTypeCatalogItemPublicId, command.CatalogItemPublicId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workingCondition.Update(
                workConditionTypeInternalIdResult.Value,
                catalogItemResult.Value?.Id,
                catalogItemResult.Value,
                name,
                command.Notes,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetWorkingConditionResponseAsync(profile.PublicId, workingCondition.PublicId, cancellationToken)
                ?? workingCondition.ToResponse(command.WorkConditionTypeCatalogItemPublicId, command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated working condition in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileWorkingConditionResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileWorkingConditionResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchJobProfileWorkingConditionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository)
    : ICommandHandler<PatchJobProfileWorkingConditionCommand, JobProfileWorkingConditionResponse>
{
    public async Task<Result<JobProfileWorkingConditionResponse>> Handle(PatchJobProfileWorkingConditionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithWorkingConditionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var workingCondition = profile.WorkingConditions.FirstOrDefault(item => item.PublicId == command.WorkingConditionId);
        if (workingCondition is null)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(JobProfileErrors.WorkingConditionNotFound);
        }

        if (workingCondition.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetWorkingConditionResponseAsync(profile.PublicId, workingCondition.PublicId, cancellationToken)
            ?? workingCondition.ToResponse(null);
        var patchState = JobProfileWorkingConditionPatchState.From(before);
        var patchApplication = JobProfileWorkingConditionPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(patchApplication.Error);
        }

        var validation = JobProfileWorkingConditionPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(validation.Error);
        }

        if (!patchState.HasMutation)
        {
            return Result<JobProfileWorkingConditionResponse>.Success(before);
        }

        var workConditionTypeInternalIdResult = await ResolveWorkConditionTypeInternalIdAsync(
            tenantContext.TenantId.Value,
            patchState.WorkConditionTypeCatalogItemPublicId,
            positionDescriptionCatalogRepository,
            cancellationToken);

        if (workConditionTypeInternalIdResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(workConditionTypeInternalIdResult.Error);
        }

        var catalogItemResult = await ResolveCatalogItemAsync(patchState.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(catalogItemResult.Error);
        }

        var name = string.IsNullOrWhiteSpace(patchState.Name)
            ? catalogItemResult.Value?.Name ?? string.Empty
            : patchState.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(JobProfileErrors.WorkingConditionNameRequired);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workingCondition.Update(
                workConditionTypeInternalIdResult.Value,
                catalogItemResult.Value?.Id,
                catalogItemResult.Value,
                name,
                patchState.Notes,
                patchState.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetWorkingConditionResponseAsync(profile.PublicId, workingCondition.PublicId, cancellationToken)
                ?? workingCondition.ToResponse(patchState.WorkConditionTypeCatalogItemPublicId, patchState.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Patched working condition in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileWorkingConditionResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileWorkingConditionResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobProfileWorkingConditionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileWorkingConditionCommand, JobProfileParentConcurrencyResult>
{
    public async Task<Result<JobProfileParentConcurrencyResult>> Handle(RemoveJobProfileWorkingConditionCommand command, CancellationToken cancellationToken)
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

        var profile = await repository.GetWithWorkingConditionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var workingCondition = profile.WorkingConditions.FirstOrDefault(item => item.PublicId == command.WorkingConditionId);
        if (workingCondition is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(JobProfileErrors.WorkingConditionNotFound);
        }

        if (workingCondition.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetWorkingConditionResponseAsync(profile.PublicId, workingCondition.PublicId, cancellationToken)
            ?? workingCondition.ToResponse(null);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.RemoveWorkingCondition(workingCondition);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed working condition from job profile {profile.Code}.",
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

internal static class JobProfileWorkingConditionCommandSupport
{
    public static async Task<Result<long?>> ResolveWorkConditionTypeInternalIdAsync(
        Guid tenantId,
        Guid? workConditionTypeCatalogItemPublicId,
        IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
        CancellationToken cancellationToken) =>
        await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            tenantId,
            workConditionTypeCatalogItemPublicId,
            PositionDescriptionCatalogType.WorkConditionType,
            PositionDescriptionCatalogErrors.WorkConditionTypeNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);

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

internal sealed class JobProfileWorkingConditionPatchState
{
    public Guid? CatalogItemPublicId { get; set; }
    public Guid? WorkConditionTypeCatalogItemPublicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public bool HasMutation { get; set; }

    public static JobProfileWorkingConditionPatchState From(JobProfileWorkingConditionResponse response) =>
        new()
        {
            CatalogItemPublicId = response.CatalogItemPublicId,
            WorkConditionTypeCatalogItemPublicId = response.WorkConditionTypeCatalogItemPublicId,
            Name = response.Name,
            Notes = response.Notes,
            SortOrder = response.SortOrder
        };
}

internal static class JobProfileWorkingConditionPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfileWorkingConditionPatchOperation> operations, JobProfileWorkingConditionPatchState state)
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
                return ValidationFailure(operation.Path, "Only root working condition properties can be patched.");
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

    public static Result Validate(JobProfileWorkingConditionPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (state.CatalogItemPublicId == Guid.Empty)
        {
            errors["catalogItemPublicId"] = ["CatalogItemPublicId must be a valid UUID."];
        }

        if (state.WorkConditionTypeCatalogItemPublicId == Guid.Empty)
        {
            errors["workConditionTypeCatalogItemPublicId"] = ["WorkConditionTypeCatalogItemPublicId must be a valid UUID."];
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
        JobProfileWorkingConditionPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "workConditionTypeCatalogItemPublicId"))
        {
            state.WorkConditionTypeCatalogItemPublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

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
