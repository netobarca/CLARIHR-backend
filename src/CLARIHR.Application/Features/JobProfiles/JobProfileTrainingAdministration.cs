using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record AddJobProfileTrainingCommand(
    Guid JobProfileId,
    Guid? CatalogItemId,
    string? Name,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileTrainingResponse>>;

public sealed record UpdateJobProfileTrainingCommand(
    Guid JobProfileId,
    Guid TrainingId,
    Guid? CatalogItemId,
    string? Name,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileTrainingResponse>>;

public sealed record RemoveJobProfileTrainingCommand(
    Guid JobProfileId,
    Guid TrainingId,
    Guid ConcurrencyToken) : ICommand<JobProfileParentConcurrencyResult>;

internal sealed class AddJobProfileTrainingCommandValidator : AbstractValidator<AddJobProfileTrainingCommand>
{
    public AddJobProfileTrainingCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemId.HasValue);
        RuleFor(command => command.Name).MaximumLength(200);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class UpdateJobProfileTrainingCommandValidator : AbstractValidator<UpdateJobProfileTrainingCommand>
{
    public UpdateJobProfileTrainingCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.TrainingId).NotEmpty();
        RuleFor(command => command.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemId.HasValue);
        RuleFor(command => command.Name).MaximumLength(200);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
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

internal sealed class AddJobProfileTrainingCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<AddJobProfileTrainingCommand, JobProfileSubResourceResult<JobProfileTrainingResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>> Handle(AddJobProfileTrainingCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItem?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(new Error("JobProfileTraining.NameRequired", "A name is required for the training.", ErrorType.Validation));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var training = JobProfileTraining.Create(
                catalogItemInternalId,
                null,
                name,
                command.Notes,
                command.SortOrder);

            profile.AddTraining(training);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = training.ToResponse(command.CatalogItemId);

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
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileTrainingResponse>(response, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
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
    : ICommandHandler<UpdateJobProfileTrainingCommand, JobProfileSubResourceResult<JobProfileTrainingResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>> Handle(UpdateJobProfileTrainingCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItem?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(new Error("JobProfileTraining.NameRequired", "A name is required for the training.", ErrorType.Validation));
        }

        var training = profile.GetTraining(command.TrainingId);
        var before = training.ToResponse(command.CatalogItemId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            training.Update(
                catalogItemInternalId,
                null,
                name,
                command.Notes,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = training.ToResponse(command.CatalogItemId);

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
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileTrainingResponse>(after, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileTrainingResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
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

        var training = profile.GetTraining(command.TrainingId);
        var before = training.ToResponse();

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
