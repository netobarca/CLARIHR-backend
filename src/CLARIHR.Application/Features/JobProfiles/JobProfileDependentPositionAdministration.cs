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

public sealed record AddJobProfileDependentPositionCommand(
    Guid JobProfileId,
    Guid DependentJobProfileId,
    int Quantity,
    string? Notes,
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>;

public sealed record UpdateJobProfileDependentPositionCommand(
    Guid JobProfileId,
    Guid DependentPositionId,
    Guid DependentJobProfileId,
    int Quantity,
    string? Notes,
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>;

public sealed record RemoveJobProfileDependentPositionCommand(
    Guid JobProfileId,
    Guid DependentPositionId,
    Guid ConcurrencyToken) : ICommand<JobProfileParentConcurrencyResult>;

internal sealed class AddJobProfileDependentPositionCommandValidator : AbstractValidator<AddJobProfileDependentPositionCommand>
{
    public AddJobProfileDependentPositionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.DependentJobProfileId).NotEmpty();
        RuleFor(command => command.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class UpdateJobProfileDependentPositionCommandValidator : AbstractValidator<UpdateJobProfileDependentPositionCommand>
{
    public UpdateJobProfileDependentPositionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.DependentPositionId).NotEmpty();
        RuleFor(command => command.DependentJobProfileId).NotEmpty();
        RuleFor(command => command.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class RemoveJobProfileDependentPositionCommandValidator : AbstractValidator<RemoveJobProfileDependentPositionCommand>
{
    public RemoveJobProfileDependentPositionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.DependentPositionId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddJobProfileDependentPositionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddJobProfileDependentPositionCommand, JobProfileSubResourceResult<JobProfileDependentPositionResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>> Handle(AddJobProfileDependentPositionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(authorizationResult.Error);
        }

        if (command.JobProfileId == command.DependentJobProfileId)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(JobProfileErrors.DependencyCycle);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var dependentInternalId = await repository.ResolveProfileIdAsync(tenantContext.TenantId.Value, command.DependentJobProfileId, cancellationToken);
        if (!dependentInternalId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.DependentJobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        // We must fetch all existing dependent IDs to check for cycles
        var dependentInternalIds = profile.DependentPositions.Select(x => x.DependentJobProfileId).ToList();
        dependentInternalIds.Add(dependentInternalId.Value);

        var graph = await repository.GetDependencyGraphAsync(tenantContext.TenantId.Value, cancellationToken);
        if (JobProfileDependencyAnalyzer.WouldCreateDependentCycle(profile.Id, dependentInternalIds, graph))
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(JobProfileErrors.DependencyCycle);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var dependentPosition = JobProfileDependentPosition.Create(
                dependentInternalId.Value,
                command.Quantity,
                command.Notes);

            profile.AddDependentPosition(dependentPosition);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var dependentProfile = await repository.GetReferenceByIdAsync(tenantContext.TenantId.Value, command.DependentJobProfileId, cancellationToken)
                ?? throw new InvalidOperationException("Dependent profile reference could not be resolved.");
            var response = dependentPosition.ToResponse(dependentProfile);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added dependent position to job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileDependentPositionResponse>(response, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobProfileDependentPositionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateJobProfileDependentPositionCommand, JobProfileSubResourceResult<JobProfileDependentPositionResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>> Handle(UpdateJobProfileDependentPositionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(authorizationResult.Error);
        }

        if (command.JobProfileId == command.DependentJobProfileId)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(JobProfileErrors.DependencyCycle);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var dependentInternalId = await repository.ResolveProfileIdAsync(tenantContext.TenantId.Value, command.DependentJobProfileId, cancellationToken);
        if (!dependentInternalId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.DependentJobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var existingDependentPosition = profile.DependentPositions.FirstOrDefault(x => x.PublicId == command.DependentPositionId);
        if (existingDependentPosition is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(new Error("JobProfileDependentPosition.NotFound", "Dependent position not found.", ErrorType.NotFound));
        }

        var dependentInternalIds = profile.DependentPositions
            .Where(x => x.PublicId != command.DependentPositionId)
            .Select(x => x.DependentJobProfileId)
            .ToList();
        dependentInternalIds.Add(dependentInternalId.Value);

        var graph = await repository.GetDependencyGraphAsync(tenantContext.TenantId.Value, cancellationToken);
        if (JobProfileDependencyAnalyzer.WouldCreateDependentCycle(profile.Id, dependentInternalIds, graph))
        {
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(JobProfileErrors.DependencyCycle);
        }

        var beforeDependentProfile = existingDependentPosition.DependentJobProfile is not null
            ? new JobProfileReferenceResponse(
                existingDependentPosition.DependentJobProfile.PublicId,
                existingDependentPosition.DependentJobProfile.Code,
                existingDependentPosition.DependentJobProfile.Title)
            : await repository.GetReferenceByInternalIdAsync(tenantContext.TenantId.Value, existingDependentPosition.DependentJobProfileId, cancellationToken)
                ?? throw new InvalidOperationException("Dependent profile reference could not be resolved.");
        var before = existingDependentPosition.ToResponse(beforeDependentProfile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var dependentPosition = profile.GetDependentPosition(command.DependentPositionId);
            dependentPosition.Update(
                dependentInternalId.Value,
                command.Quantity,
                command.Notes);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var dependentProfile = await repository.GetReferenceByIdAsync(tenantContext.TenantId.Value, command.DependentJobProfileId, cancellationToken)
                ?? throw new InvalidOperationException("Dependent profile reference could not be resolved.");
            var after = dependentPosition.ToResponse(dependentProfile);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated dependent position in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileDependentPositionResponse>(after, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobProfileDependentPositionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileDependentPositionCommand, JobProfileParentConcurrencyResult>
{
    public async Task<Result<JobProfileParentConcurrencyResult>> Handle(RemoveJobProfileDependentPositionCommand command, CancellationToken cancellationToken)
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

        var dependentPosition = profile.GetDependentPosition(command.DependentPositionId);
        var beforeDependentProfile = dependentPosition.DependentJobProfile is not null
            ? new JobProfileReferenceResponse(
                dependentPosition.DependentJobProfile.PublicId,
                dependentPosition.DependentJobProfile.Code,
                dependentPosition.DependentJobProfile.Title)
            : await repository.GetReferenceByInternalIdAsync(tenantContext.TenantId.Value, dependentPosition.DependentJobProfileId, cancellationToken)
                ?? throw new InvalidOperationException("Dependent profile reference could not be resolved.");
        var before = dependentPosition.ToResponse(beforeDependentProfile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.RemoveDependentPosition(dependentPosition);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed dependent position from job profile {profile.Code}.",
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
