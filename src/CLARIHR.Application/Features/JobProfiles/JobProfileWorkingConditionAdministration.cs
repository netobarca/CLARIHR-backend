using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record AddJobProfileWorkingConditionCommand(
    Guid JobProfileId,
    Guid? WorkConditionTypeCatalogItemId,
    Guid? CatalogItemId,
    string? Name,
    string? Notes,
    int SortOrder) : ICommand<JobProfileWorkingConditionResponse>;

public sealed record UpdateJobProfileWorkingConditionCommand(
    Guid JobProfileId,
    Guid WorkingConditionId,
    Guid? WorkConditionTypeCatalogItemId,
    Guid? CatalogItemId,
    string? Name,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileWorkingConditionResponse>;

public sealed record RemoveJobProfileWorkingConditionCommand(
    Guid JobProfileId,
    Guid WorkingConditionId,
    Guid ConcurrencyToken) : ICommand<bool>;

internal sealed class AddJobProfileWorkingConditionCommandValidator : AbstractValidator<AddJobProfileWorkingConditionCommand>
{
    public AddJobProfileWorkingConditionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.WorkConditionTypeCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.WorkConditionTypeCatalogItemId.HasValue);
        RuleFor(command => command.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemId.HasValue);
        RuleFor(command => command.Name).MaximumLength(200);
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
        RuleFor(command => command.WorkConditionTypeCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.WorkConditionTypeCatalogItemId.HasValue);
        RuleFor(command => command.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemId.HasValue);
        RuleFor(command => command.Name).MaximumLength(200);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
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

        var workConditionTypeInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            tenantContext.TenantId.Value,
            command.WorkConditionTypeCatalogItemId,
            PositionDescriptionCatalogType.WorkConditionType,
            PositionDescriptionCatalogErrors.WorkConditionTypeNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);

        if (workConditionTypeInternalIdResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(workConditionTypeInternalIdResult.Error);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItem?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(new Error("JobProfileWorkingCondition.NameRequired", "A name is required for the working condition.", ErrorType.Validation));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var workingCondition = JobProfileWorkingCondition.Create(
                workConditionTypeInternalIdResult.Value,
                catalogItemInternalId,
                null,
                name,
                command.Notes,
                command.SortOrder);

            profile.AddWorkingCondition(workingCondition);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = new JobProfileWorkingConditionResponse(
                workingCondition.PublicId,
                command.CatalogItemId,
                command.WorkConditionTypeCatalogItemId,
                workingCondition.Name,
                workingCondition.Notes,
                workingCondition.SortOrder,
                workingCondition.ConcurrencyToken);

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

        var workingCondition = profile.WorkingConditions.FirstOrDefault(x => x.PublicId == command.WorkingConditionId);
        if (workingCondition is null)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(
                new Error("JobProfileWorkingCondition.NotFound", "The working condition could not be found.", ErrorType.NotFound));
        }

        if (workingCondition.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var workConditionTypeInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            tenantContext.TenantId.Value,
            command.WorkConditionTypeCatalogItemId,
            PositionDescriptionCatalogType.WorkConditionType,
            PositionDescriptionCatalogErrors.WorkConditionTypeNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);

        if (workConditionTypeInternalIdResult.IsFailure)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(workConditionTypeInternalIdResult.Error);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItem?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileWorkingConditionResponse>.Failure(new Error("JobProfileWorkingCondition.NameRequired", "A name is required for the working condition.", ErrorType.Validation));
        }

        var before = new JobProfileWorkingConditionResponse(
            workingCondition.PublicId,
            workingCondition.CatalogItem?.PublicId,
            command.WorkConditionTypeCatalogItemId,
            workingCondition.Name,
            workingCondition.Notes,
            workingCondition.SortOrder,
            workingCondition.ConcurrencyToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workingCondition.Update(
                workConditionTypeInternalIdResult.Value,
                catalogItemInternalId,
                null,
                name,
                command.Notes,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = new JobProfileWorkingConditionResponse(
                workingCondition.PublicId,
                command.CatalogItemId,
                command.WorkConditionTypeCatalogItemId,
                workingCondition.Name,
                workingCondition.Notes,
                workingCondition.SortOrder,
                workingCondition.ConcurrencyToken);

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

internal sealed class RemoveJobProfileWorkingConditionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileWorkingConditionCommand, bool>
{
    public async Task<Result<bool>> Handle(RemoveJobProfileWorkingConditionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<bool>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithWorkingConditionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<bool>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var workingCondition = profile.WorkingConditions.FirstOrDefault(x => x.PublicId == command.WorkingConditionId);
        if (workingCondition is null)
        {
            return Result<bool>.Failure(
                new Error("JobProfileWorkingCondition.NotFound", "The working condition could not be found.", ErrorType.NotFound));
        }

        if (workingCondition.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = new JobProfileWorkingConditionResponse(
            workingCondition.PublicId,
            workingCondition.CatalogItem?.PublicId,
            null,
            workingCondition.Name,
            workingCondition.Notes,
            workingCondition.SortOrder,
            workingCondition.ConcurrencyToken);

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
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<bool>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
