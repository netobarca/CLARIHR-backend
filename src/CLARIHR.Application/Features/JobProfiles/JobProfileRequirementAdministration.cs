using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Abstractions.InternalCatalogs;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record AddJobProfileRequirementCommand(
    Guid JobProfileId,
    JobRequirementType RequirementType,
    Guid? RequirementTypeCatalogItemId,
    Guid? CatalogItemId,
    string? CatalogCode,
    string? CatalogName,
    string Description,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileRequirementResponse>>;

public sealed record UpdateJobProfileRequirementCommand(
    Guid JobProfileId,
    Guid RequirementId,
    JobRequirementType RequirementType,
    Guid? RequirementTypeCatalogItemId,
    Guid? CatalogItemId,
    string? CatalogCode,
    string? CatalogName,
    string Description,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileRequirementResponse>>;

public sealed record RemoveJobProfileRequirementCommand(
    Guid JobProfileId,
    Guid RequirementId,
    Guid ConcurrencyToken) : ICommand<JobProfileParentConcurrencyResult>;

internal sealed class AddJobProfileRequirementCommandValidator : AbstractValidator<AddJobProfileRequirementCommand>
{
    public AddJobProfileRequirementCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemId.HasValue);
        RuleFor(command => command.RequirementTypeCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.RequirementTypeCatalogItemId.HasValue);
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

internal sealed class UpdateJobProfileRequirementCommandValidator : AbstractValidator<UpdateJobProfileRequirementCommand>
{
    public UpdateJobProfileRequirementCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RequirementId).NotEmpty();
        RuleFor(command => command.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemId.HasValue);
        RuleFor(command => command.RequirementTypeCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.RequirementTypeCatalogItemId.HasValue);
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

internal sealed class RemoveJobProfileRequirementCommandValidator : AbstractValidator<RemoveJobProfileRequirementCommand>
{
    public RemoveJobProfileRequirementCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RequirementId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddJobProfileRequirementCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<AddJobProfileRequirementCommand, JobProfileSubResourceResult<JobProfileRequirementResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>> Handle(AddJobProfileRequirementCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var requirementTypeInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            tenantContext.TenantId.Value,
            command.RequirementTypeCatalogItemId,
            CLARIHR.Domain.PositionDescriptionCatalogs.PositionDescriptionCatalogType.RequirementType,
            CLARIHR.Application.Features.PositionDescriptionCatalogs.Common.PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);

        if (requirementTypeInternalIdResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(requirementTypeInternalIdResult.Error);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var requirement = JobProfileRequirement.Create(
                command.RequirementType,
                requirementTypeInternalIdResult.Value,
                catalogItemInternalId,
                null,
                command.Description,
                command.SortOrder);

            profile.AddRequirement(requirement);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = requirement.ToResponse(command.RequirementTypeCatalogItemId, command.CatalogItemId);

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
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileRequirementResponse>(response, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
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
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<UpdateJobProfileRequirementCommand, JobProfileSubResourceResult<JobProfileRequirementResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>> Handle(UpdateJobProfileRequirementCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var requirementTypeInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            tenantContext.TenantId.Value,
            command.RequirementTypeCatalogItemId,
            CLARIHR.Domain.PositionDescriptionCatalogs.PositionDescriptionCatalogType.RequirementType,
            CLARIHR.Application.Features.PositionDescriptionCatalogs.Common.PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);

        if (requirementTypeInternalIdResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(requirementTypeInternalIdResult.Error);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var requirement = profile.GetRequirement(command.RequirementId);
        var before = requirement.ToResponse(command.RequirementTypeCatalogItemId, command.CatalogItemId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            requirement.Update(
                command.RequirementType,
                requirementTypeInternalIdResult.Value,
                catalogItemInternalId,
                null,
                command.Description,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = requirement.ToResponse(command.RequirementTypeCatalogItemId, command.CatalogItemId);

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
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileRequirementResponse>(after, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileRequirementResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
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

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var requirement = profile.GetRequirement(command.RequirementId);
        var before = requirement.ToResponse(null);

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
