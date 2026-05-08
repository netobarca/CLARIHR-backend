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
    Guid ConcurrencyToken) : ICommand<JobProfileResponse>;

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
    Guid ConcurrencyToken) : ICommand<JobProfileResponse>;

public sealed record RemoveJobProfileRequirementCommand(
    Guid JobProfileId,
    Guid RequirementId,
    Guid ConcurrencyToken) : ICommand<JobProfileResponse>;

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
    : ICommandHandler<AddJobProfileRequirementCommand, JobProfileResponse>
{
    public async Task<Result<JobProfileResponse>> Handle(AddJobProfileRequirementCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
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
            return Result<JobProfileResponse>.Failure(requirementTypeInternalIdResult.Error);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileResponse>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var before = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile response could not be resolved.");

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

            var after = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added requirement to job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
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
    : ICommandHandler<UpdateJobProfileRequirementCommand, JobProfileResponse>
{
    public async Task<Result<JobProfileResponse>> Handle(UpdateJobProfileRequirementCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
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
            return Result<JobProfileResponse>.Failure(requirementTypeInternalIdResult.Error);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileResponse>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var before = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile response could not be resolved.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var requirement = profile.GetRequirement(command.RequirementId);
            
            requirement.Update(
                command.RequirementType,
                requirementTypeInternalIdResult.Value,
                catalogItemInternalId,
                null,
                command.Description,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile response could not be resolved after update.");

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
            return Result<JobProfileResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
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
    : ICommandHandler<RemoveJobProfileRequirementCommand, JobProfileResponse>
{
    public async Task<Result<JobProfileResponse>> Handle(RemoveJobProfileRequirementCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile response could not be resolved.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var requirement = profile.GetRequirement(command.RequirementId);
            profile.RemoveRequirement(requirement);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed requirement from job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
