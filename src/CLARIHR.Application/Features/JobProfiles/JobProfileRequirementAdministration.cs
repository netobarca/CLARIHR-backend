using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.InternalCatalogs;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.InternalCatalogs;
using CLARIHR.Application.Features.InternalCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.InternalCatalogs;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using FluentValidation;
using static CLARIHR.Application.Features.JobProfiles.JobProfileRequirementCommandSupport;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record GetJobProfileRequirementsQuery(
    Guid JobProfileId,
    int PageNumber = 1,
    int PageSize = JobProfileValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<JobProfileRequirementResponse>>;

public sealed record GetJobProfileRequirementByIdQuery(Guid JobProfileId, Guid RequirementId)
    : IQuery<JobProfileRequirementResponse>;

public sealed record AddJobProfileRequirementCommand(
    Guid JobProfileId,
    JobRequirementType RequirementType,
    Guid? RequirementTypeCatalogItemPublicId,
    Guid? CatalogItemPublicId,
    string? CatalogCode,
    string? CatalogName,
    string Description,
    int SortOrder) : ICommand<JobProfileRequirementResponse>;

public sealed record UpdateJobProfileRequirementCommand(
    Guid JobProfileId,
    Guid RequirementId,
    JobRequirementType RequirementType,
    Guid? RequirementTypeCatalogItemPublicId,
    Guid? CatalogItemPublicId,
    string? CatalogCode,
    string? CatalogName,
    string Description,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileRequirementResponse>;

public sealed record JobProfileRequirementPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileRequirementCommand(
    Guid JobProfileId,
    Guid RequirementId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<JobProfileRequirementPatchOperation> Operations) : ICommand<JobProfileRequirementResponse>;

public sealed record RemoveJobProfileRequirementCommand(
    Guid JobProfileId,
    Guid RequirementId,
    Guid ConcurrencyToken) : ICommand<JobProfileParentConcurrencyResult>;

internal sealed class GetJobProfileRequirementsQueryValidator : AbstractValidator<GetJobProfileRequirementsQuery>
{
    public GetJobProfileRequirementsQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, JobProfileValidationRules.MaxPageSize);
    }
}

internal sealed class GetJobProfileRequirementByIdQueryValidator : AbstractValidator<GetJobProfileRequirementByIdQuery>
{
    public GetJobProfileRequirementByIdQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
        RuleFor(query => query.RequirementId).NotEmpty();
    }
}

internal sealed class AddJobProfileRequirementCommandValidator : AbstractValidator<AddJobProfileRequirementCommand>
{
    public AddJobProfileRequirementCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemPublicId.HasValue);
        RuleFor(command => command.RequirementTypeCatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.RequirementTypeCatalogItemPublicId.HasValue);
        RuleFor(command => command.CatalogCode)
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .When(static command => !string.IsNullOrWhiteSpace(command.CatalogCode))
            .WithMessage("CatalogCode format is invalid.");
        RuleFor(command => command.CatalogName).MaximumLength(120);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateJobProfileRequirementCommandValidator : AbstractValidator<UpdateJobProfileRequirementCommand>
{
    public UpdateJobProfileRequirementCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RequirementId).NotEmpty();
        RuleFor(command => command.CatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemPublicId.HasValue);
        RuleFor(command => command.RequirementTypeCatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.RequirementTypeCatalogItemPublicId.HasValue);
        RuleFor(command => command.CatalogCode)
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .When(static command => !string.IsNullOrWhiteSpace(command.CatalogCode))
            .WithMessage("CatalogCode format is invalid.");
        RuleFor(command => command.CatalogName).MaximumLength(120);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchJobProfileRequirementCommandValidator : AbstractValidator<PatchJobProfileRequirementCommand>
{
    public PatchJobProfileRequirementCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RequirementId).NotEmpty();
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

internal sealed class RemoveJobProfileRequirementCommandValidator : AbstractValidator<RemoveJobProfileRequirementCommand>
{
    public RemoveJobProfileRequirementCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RequirementId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetJobProfileRequirementsQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileRequirementsQuery, PagedResponse<JobProfileRequirementResponse>>
{
    public async Task<Result<PagedResponse<JobProfileRequirementResponse>>> Handle(GetJobProfileRequirementsQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PagedResponse<JobProfileRequirementResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<JobProfileRequirementResponse>>.Failure(authorizationResult.Error);
        }

        var requirements = await repository.GetRequirementResponsesByProfileIdAsync(
            query.JobProfileId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        if (requirements is null)
        {
            return Result<PagedResponse<JobProfileRequirementResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.JobProfileNotFound);
        }

        return Result<PagedResponse<JobProfileRequirementResponse>>.Success(requirements);
    }
}

internal sealed class GetJobProfileRequirementByIdQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileRequirementByIdQuery, JobProfileRequirementResponse>
{
    public async Task<Result<JobProfileRequirementResponse>> Handle(GetJobProfileRequirementByIdQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileRequirementResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(authorizationResult.Error);
        }

        var requirement = await repository.GetRequirementResponseAsync(query.JobProfileId, query.RequirementId, cancellationToken);
        if (requirement is null)
        {
            return Result<JobProfileRequirementResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.RequirementNotFound);
        }

        return Result<JobProfileRequirementResponse>.Success(requirement);
    }
}

internal sealed class AddJobProfileRequirementCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
    IJobCatalogRepository catalogRepository,
    IInternalCatalogRepository internalCatalogRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AddJobProfileRequirementCommand, JobProfileRequirementResponse>
{
    public async Task<Result<JobProfileRequirementResponse>> Handle(AddJobProfileRequirementCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileRequirementResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithRequirementsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileRequirementResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var requirementTypeInternalIdResult = await ResolveRequirementTypeInternalIdAsync(
            tenantContext.TenantId.Value,
            command.RequirementTypeCatalogItemPublicId,
            positionDescriptionCatalogRepository,
            cancellationToken);

        if (requirementTypeInternalIdResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(requirementTypeInternalIdResult.Error);
        }

        var catalogItemResult = await ResolveCatalogItemInternalIdAsync(command.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(catalogItemResult.Error);
        }

        var descriptionResult = await ResolveDescriptionInternalCatalogUsageAsync(
            command.RequirementType,
            command.Description,
            internalCatalogRepository,
            currentUserService,
            dateTimeProvider,
            cancellationToken);
        if (descriptionResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(descriptionResult.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (descriptionResult.Value.CreatedValue is not null)
            {
                internalCatalogRepository.Add(descriptionResult.Value.CreatedValue);
            }

            var requirement = JobProfileRequirement.Create(
                command.RequirementType,
                requirementTypeInternalIdResult.Value,
                catalogItemResult.Value,
                null,
                descriptionResult.Value.Description,
                command.SortOrder);

            profile.AddRequirement(requirement);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetRequirementResponseAsync(profile.PublicId, requirement.PublicId, cancellationToken)
                ?? requirement.ToResponse(command.RequirementTypeCatalogItemPublicId, command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added requirement to job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileRequirementResponse>.Success(response);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileRequirementResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobProfileRequirementCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
    IJobCatalogRepository catalogRepository,
    IInternalCatalogRepository internalCatalogRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateJobProfileRequirementCommand, JobProfileRequirementResponse>
{
    public async Task<Result<JobProfileRequirementResponse>> Handle(UpdateJobProfileRequirementCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileRequirementResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithRequirementsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileRequirementResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var requirement = profile.Requirements.FirstOrDefault(item => item.PublicId == command.RequirementId);
        if (requirement is null)
        {
            return Result<JobProfileRequirementResponse>.Failure(JobProfileErrors.RequirementNotFound);
        }

        if (requirement.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileRequirementResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var requirementTypeInternalIdResult = await ResolveRequirementTypeInternalIdAsync(
            tenantContext.TenantId.Value,
            command.RequirementTypeCatalogItemPublicId,
            positionDescriptionCatalogRepository,
            cancellationToken);

        if (requirementTypeInternalIdResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(requirementTypeInternalIdResult.Error);
        }

        var catalogItemResult = await ResolveCatalogItemInternalIdAsync(command.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(catalogItemResult.Error);
        }

        var descriptionResult = await ResolveDescriptionInternalCatalogUsageAsync(
            command.RequirementType,
            command.Description,
            internalCatalogRepository,
            currentUserService,
            dateTimeProvider,
            cancellationToken);
        if (descriptionResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(descriptionResult.Error);
        }

        var before = await repository.GetRequirementResponseAsync(profile.PublicId, requirement.PublicId, cancellationToken)
            ?? requirement.ToResponse(command.RequirementTypeCatalogItemPublicId, command.CatalogItemPublicId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (descriptionResult.Value.CreatedValue is not null)
            {
                internalCatalogRepository.Add(descriptionResult.Value.CreatedValue);
            }

            requirement.Update(
                command.RequirementType,
                requirementTypeInternalIdResult.Value,
                catalogItemResult.Value,
                null,
                descriptionResult.Value.Description,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetRequirementResponseAsync(profile.PublicId, requirement.PublicId, cancellationToken)
                ?? requirement.ToResponse(command.RequirementTypeCatalogItemPublicId, command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated requirement in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileRequirementResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileRequirementResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchJobProfileRequirementCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
    IJobCatalogRepository catalogRepository,
    IInternalCatalogRepository internalCatalogRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PatchJobProfileRequirementCommand, JobProfileRequirementResponse>
{
    public async Task<Result<JobProfileRequirementResponse>> Handle(PatchJobProfileRequirementCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileRequirementResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithRequirementsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileRequirementResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var requirement = profile.Requirements.FirstOrDefault(item => item.PublicId == command.RequirementId);
        if (requirement is null)
        {
            return Result<JobProfileRequirementResponse>.Failure(JobProfileErrors.RequirementNotFound);
        }

        if (requirement.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileRequirementResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetRequirementResponseAsync(profile.PublicId, requirement.PublicId, cancellationToken)
            ?? requirement.ToResponse(null);
        var patchState = JobProfileRequirementPatchState.From(before);
        var patchApplication = JobProfileRequirementPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(patchApplication.Error);
        }

        var validation = JobProfileRequirementPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(validation.Error);
        }

        if (!patchState.HasMutation)
        {
            return Result<JobProfileRequirementResponse>.Success(before);
        }

        var requirementTypeInternalIdResult = await ResolveRequirementTypeInternalIdAsync(
            tenantContext.TenantId.Value,
            patchState.RequirementTypeCatalogItemPublicId,
            positionDescriptionCatalogRepository,
            cancellationToken);

        if (requirementTypeInternalIdResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(requirementTypeInternalIdResult.Error);
        }

        var catalogItemResult = await ResolveCatalogItemInternalIdAsync(patchState.CatalogItemPublicId, catalogRepository, cancellationToken);
        if (catalogItemResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(catalogItemResult.Error);
        }

        var descriptionResult = await ResolveDescriptionInternalCatalogUsageAsync(
            patchState.RequirementType,
            patchState.Description,
            internalCatalogRepository,
            currentUserService,
            dateTimeProvider,
            cancellationToken);
        if (descriptionResult.IsFailure)
        {
            return Result<JobProfileRequirementResponse>.Failure(descriptionResult.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (descriptionResult.Value.CreatedValue is not null)
            {
                internalCatalogRepository.Add(descriptionResult.Value.CreatedValue);
            }

            requirement.Update(
                patchState.RequirementType,
                requirementTypeInternalIdResult.Value,
                catalogItemResult.Value,
                null,
                descriptionResult.Value.Description,
                patchState.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetRequirementResponseAsync(profile.PublicId, requirement.PublicId, cancellationToken)
                ?? requirement.ToResponse(patchState.RequirementTypeCatalogItemPublicId, patchState.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Patched requirement in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileRequirementResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileRequirementResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobProfileRequirementCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileRequirementCommand, JobProfileParentConcurrencyResult>
{
    public async Task<Result<JobProfileParentConcurrencyResult>> Handle(RemoveJobProfileRequirementCommand command, CancellationToken cancellationToken)
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

        var profile = await repository.GetWithRequirementsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var requirement = profile.Requirements.FirstOrDefault(item => item.PublicId == command.RequirementId);
        if (requirement is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(JobProfileErrors.RequirementNotFound);
        }

        if (requirement.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetRequirementResponseAsync(profile.PublicId, requirement.PublicId, cancellationToken)
            ?? requirement.ToResponse(null);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.RemoveRequirement(requirement);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed requirement from job profile {profile.Code}.",
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

internal sealed class JobProfileRequirementPatchState
{
    public JobRequirementType RequirementType { get; set; }
    public Guid? RequirementTypeCatalogItemPublicId { get; set; }
    public Guid? CatalogItemPublicId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool HasMutation { get; set; }

    public static JobProfileRequirementPatchState From(JobProfileRequirementResponse response) =>
        new()
        {
            RequirementType = response.RequirementType,
            RequirementTypeCatalogItemPublicId = response.RequirementTypeCatalogItemPublicId,
            CatalogItemPublicId = response.CatalogItemPublicId,
            Description = response.Description,
            SortOrder = response.SortOrder
        };
}

internal static class JobProfileRequirementPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfileRequirementPatchOperation> operations, JobProfileRequirementPatchState state)
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
                return ValidationFailure(operation.Path, "Only root requirement properties can be patched.");
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

    public static Result Validate(JobProfileRequirementPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (state.CatalogItemPublicId == Guid.Empty)
        {
            errors["catalogItemPublicId"] = ["CatalogItemPublicId must be a valid UUID."];
        }

        if (state.RequirementTypeCatalogItemPublicId == Guid.Empty)
        {
            errors["requirementTypeCatalogItemPublicId"] = ["RequirementTypeCatalogItemPublicId must be a valid UUID."];
        }

        if (string.IsNullOrWhiteSpace(state.Description))
        {
            errors["description"] = ["Description is required."];
        }
        else if (state.Description.Length > 1000)
        {
            errors["description"] = ["Description must be 1000 characters or fewer."];
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
        JobProfileRequirementPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "requirementType"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "RequirementType cannot be removed.");
            }

            state.RequirementType = ReadRequirementType(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsAnySegment(property, "requirementTypeCatalogItemPublicId", "requirementTypeCatalogItemId"))
        {
            state.RequirementTypeCatalogItemPublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsAnySegment(property, "catalogItemPublicId", "catalogItemId"))
        {
            state.CatalogItemPublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "description"))
        {
            state.Description = isRemove ? string.Empty : ReadRequiredString(value, path);
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

    private static JobRequirementType ReadRequirementType(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new JobProfilePatchValueException(path, "RequirementType is required.");
        }

        if (value!.Value.ValueKind == JsonValueKind.String)
        {
            var raw = value.Value.GetString();
            return Enum.TryParse<JobRequirementType>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(typeof(JobRequirementType), parsed)
                ? parsed
                : throw new JobProfilePatchValueException(path, $"RequirementType '{raw}' is not a valid value.");
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var numeric))
        {
            var parsed = (JobRequirementType)numeric;
            return Enum.IsDefined(typeof(JobRequirementType), parsed)
                ? parsed
                : throw new JobProfilePatchValueException(path, $"RequirementType '{numeric}' is not a valid value.");
        }

        throw new JobProfilePatchValueException(path, "RequirementType value must be a string or integer.");
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

internal static class JobProfileRequirementCommandSupport
{
    public sealed record RequirementDescriptionResolution(string Description, InternalCatalogValue? CreatedValue);

    public static async Task<Result<long?>> ResolveRequirementTypeInternalIdAsync(
        Guid tenantId,
        Guid? requirementTypeCatalogItemPublicId,
        IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
        CancellationToken cancellationToken) =>
        await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            tenantId,
            requirementTypeCatalogItemPublicId,
            PositionDescriptionCatalogType.RequirementType,
            PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);

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

    public static async Task<Result<RequirementDescriptionResolution>> ResolveDescriptionInternalCatalogUsageAsync(
        JobRequirementType requirementType,
        string description,
        IInternalCatalogRepository internalCatalogRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken)
    {
        if (!InternalCatalogRegistry.TryGetRequirementDefinition(requirementType, out var definition) ||
            definition.RenderType != InternalCatalogRenderType.Search ||
            string.IsNullOrWhiteSpace(definition.CatalogKey) ||
            string.IsNullOrWhiteSpace(description))
        {
            return Result<RequirementDescriptionResolution>.Success(new RequirementDescriptionResolution(description, null));
        }

        if (!Guid.TryParse(currentUserService.UserId, out var actorUserPublicId))
        {
            return Result<RequirementDescriptionResolution>.Failure(InternalCatalogErrors.InvalidCurrentUser);
        }

        var resolution = await InternalCatalogValueResolver.ResolveForUsageAsync(
            definition.CatalogKey,
            description,
            actorUserPublicId,
            internalCatalogRepository,
            dateTimeProvider,
            cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<RequirementDescriptionResolution>.Failure(resolution.Error);
        }

        return Result<RequirementDescriptionResolution>.Success(
            new RequirementDescriptionResolution(
                resolution.Value.ResolvedValue,
                resolution.Value.CreatedValue));
    }
}
