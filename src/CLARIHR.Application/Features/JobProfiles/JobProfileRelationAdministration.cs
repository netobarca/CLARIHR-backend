using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record AddJobProfileRelationCommand(
    Guid JobProfileId,
    JobRelationType RelationType,
    Guid? CatalogItemId,
    string Counterpart,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileRelationResponse>>;

public sealed record UpdateJobProfileRelationCommand(
    Guid JobProfileId,
    Guid RelationId,
    JobRelationType RelationType,
    Guid? CatalogItemId,
    string Counterpart,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileRelationResponse>>;

public sealed record RemoveJobProfileRelationCommand(
    Guid JobProfileId,
    Guid RelationId,
    Guid ConcurrencyToken) : ICommand<JobProfileParentConcurrencyResult>;

internal sealed class AddJobProfileRelationCommandValidator : AbstractValidator<AddJobProfileRelationCommand>
{
    public AddJobProfileRelationCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemId.HasValue);
        RuleFor(command => command.Counterpart).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class UpdateJobProfileRelationCommandValidator : AbstractValidator<UpdateJobProfileRelationCommand>
{
    public UpdateJobProfileRelationCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RelationId).NotEmpty();
        RuleFor(command => command.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemId.HasValue);
        RuleFor(command => command.Counterpart).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
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

internal sealed class AddJobProfileRelationCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<AddJobProfileRelationCommand, JobProfileSubResourceResult<JobProfileRelationResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileRelationResponse>>> Handle(AddJobProfileRelationCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var relation = JobProfileRelation.Create(
                command.RelationType,
                catalogItemInternalId,
                null,
                command.Counterpart,
                command.Notes,
                command.SortOrder);

            profile.AddRelation(relation);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = relation.ToResponse(command.CatalogItemId);

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
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileRelationResponse>(response, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
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
    : ICommandHandler<UpdateJobProfileRelationCommand, JobProfileSubResourceResult<JobProfileRelationResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileRelationResponse>>> Handle(UpdateJobProfileRelationCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var relation = profile.GetRelation(command.RelationId);
        var before = relation.ToResponse(command.CatalogItemId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            relation.Update(
                command.RelationType,
                catalogItemInternalId,
                null,
                command.Counterpart,
                command.Notes,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = relation.ToResponse(command.CatalogItemId);

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
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileRelationResponse>(after, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileRelationResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
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

        var relation = profile.GetRelation(command.RelationId);
        var before = relation.ToResponse();

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
