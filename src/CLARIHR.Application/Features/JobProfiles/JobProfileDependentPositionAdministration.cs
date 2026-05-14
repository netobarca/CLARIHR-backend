using System.Text.Json;
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

public sealed record GetJobProfileDependentPositionsQuery(Guid JobProfileId)
    : IQuery<IReadOnlyCollection<JobProfileDependentPositionResponse>>;

public sealed record AddJobProfileDependentPositionCommand(
    Guid JobProfileId,
    Guid DependentJobProfileId,
    int Quantity,
    string? Notes) : ICommand<JobProfileDependentPositionResponse>;

public sealed record UpdateJobProfileDependentPositionCommand(
    Guid JobProfileId,
    Guid DependentPositionId,
    Guid DependentJobProfileId,
    int Quantity,
    string? Notes,
    Guid ConcurrencyToken) : ICommand<JobProfileDependentPositionResponse>;

public sealed record JobProfileDependentPositionPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileDependentPositionCommand(
    Guid JobProfileId,
    Guid DependentPositionId,
    IReadOnlyCollection<JobProfileDependentPositionPatchOperation> Operations) : ICommand<JobProfileDependentPositionResponse>;

public sealed record RemoveJobProfileDependentPositionCommand(
    Guid JobProfileId,
    Guid DependentPositionId,
    Guid ConcurrencyToken) : ICommand<JobProfileDependentPositionResponse>;

internal sealed class GetJobProfileDependentPositionsQueryValidator : AbstractValidator<GetJobProfileDependentPositionsQuery>
{
    public GetJobProfileDependentPositionsQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class AddJobProfileDependentPositionCommandValidator : AbstractValidator<AddJobProfileDependentPositionCommand>
{
    public AddJobProfileDependentPositionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.DependentJobProfileId).NotEmpty();
        RuleFor(command => command.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Notes).MaximumLength(1000);
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

internal sealed class PatchJobProfileDependentPositionCommandValidator : AbstractValidator<PatchJobProfileDependentPositionCommand>
{
    public PatchJobProfileDependentPositionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.DependentPositionId).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(ContainsConcurrencyToken)
            .WithMessage("Patch document must include a non-remove operation for /concurrencyToken.");
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }

    private static bool ContainsConcurrencyToken(IReadOnlyCollection<JobProfileDependentPositionPatchOperation>? operations) =>
        operations is not null &&
        operations.Any(static operation =>
            !string.Equals(operation.Op, "remove", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(operation.Path) &&
            string.Equals(operation.Path.Trim(), "/concurrencyToken", StringComparison.OrdinalIgnoreCase));
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

internal sealed class GetJobProfileDependentPositionsQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileDependentPositionsQuery, IReadOnlyCollection<JobProfileDependentPositionResponse>>
{
    public async Task<Result<IReadOnlyCollection<JobProfileDependentPositionResponse>>> Handle(GetJobProfileDependentPositionsQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IReadOnlyCollection<JobProfileDependentPositionResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<JobProfileDependentPositionResponse>>.Failure(authorizationResult.Error);
        }

        var positions = await repository.GetDependentPositionResponsesByProfileIdAsync(query.JobProfileId, cancellationToken);
        if (positions is null)
        {
            return Result<IReadOnlyCollection<JobProfileDependentPositionResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.JobProfileNotFound);
        }

        return Result<IReadOnlyCollection<JobProfileDependentPositionResponse>>.Success(positions);
    }
}

internal sealed class AddJobProfileDependentPositionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddJobProfileDependentPositionCommand, JobProfileDependentPositionResponse>
{
    public async Task<Result<JobProfileDependentPositionResponse>> Handle(AddJobProfileDependentPositionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(authorizationResult.Error);
        }

        if (command.JobProfileId == command.DependentJobProfileId)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.DependencyCycle);
        }

        var profile = await repository.GetWithDependentPositionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var dependentInternalId = await repository.ResolveProfileIdAsync(tenantContext.TenantId.Value, command.DependentJobProfileId, cancellationToken);
        if (!dependentInternalId.HasValue)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.DependentJobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var dependentInternalIds = profile.DependentPositions.Select(position => position.DependentJobProfileId).ToList();
        dependentInternalIds.Add(dependentInternalId.Value);

        var graph = await repository.GetDependencyGraphAsync(tenantContext.TenantId.Value, cancellationToken);
        if (JobProfileDependencyAnalyzer.WouldCreateDependentCycle(profile.Id, dependentInternalIds, graph))
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.DependencyCycle);
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

            var response = await repository.GetDependentPositionResponseAsync(profile.PublicId, dependentPosition.PublicId, cancellationToken)
                ?? await BuildResponseAsync(tenantContext.TenantId.Value, dependentPosition, command.DependentJobProfileId, cancellationToken);

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
            return Result<JobProfileDependentPositionResponse>.Success(response);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileDependentPositionResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<JobProfileDependentPositionResponse> BuildResponseAsync(
        Guid tenantId,
        JobProfileDependentPosition dependentPosition,
        Guid dependentJobProfileId,
        CancellationToken cancellationToken)
    {
        var dependentProfile = await repository.GetReferenceByIdAsync(tenantId, dependentJobProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Dependent profile reference could not be resolved.");
        return dependentPosition.ToResponse(dependentProfile);
    }
}

internal sealed class UpdateJobProfileDependentPositionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateJobProfileDependentPositionCommand, JobProfileDependentPositionResponse>
{
    public async Task<Result<JobProfileDependentPositionResponse>> Handle(UpdateJobProfileDependentPositionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(authorizationResult.Error);
        }

        if (command.JobProfileId == command.DependentJobProfileId)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.DependencyCycle);
        }

        var profile = await repository.GetWithDependentPositionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var dependentPosition = profile.DependentPositions.FirstOrDefault(item => item.PublicId == command.DependentPositionId);
        if (dependentPosition is null)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.DependentPositionNotFound);
        }

        if (dependentPosition.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var dependentInternalId = await repository.ResolveProfileIdAsync(tenantContext.TenantId.Value, command.DependentJobProfileId, cancellationToken);
        if (!dependentInternalId.HasValue)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.DependentJobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var dependentInternalIds = profile.DependentPositions
            .Where(item => item.PublicId != command.DependentPositionId)
            .Select(item => item.DependentJobProfileId)
            .ToList();
        dependentInternalIds.Add(dependentInternalId.Value);

        var graph = await repository.GetDependencyGraphAsync(tenantContext.TenantId.Value, cancellationToken);
        if (JobProfileDependencyAnalyzer.WouldCreateDependentCycle(profile.Id, dependentInternalIds, graph))
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.DependencyCycle);
        }

        var before = await repository.GetDependentPositionResponseAsync(profile.PublicId, dependentPosition.PublicId, cancellationToken)
            ?? await BuildResponseForDependentInternalAsync(tenantContext.TenantId.Value, dependentPosition, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            dependentPosition.Update(
                dependentInternalId.Value,
                command.Quantity,
                command.Notes);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetDependentPositionResponseAsync(profile.PublicId, dependentPosition.PublicId, cancellationToken)
                ?? await BuildResponseForDependentPublicAsync(tenantContext.TenantId.Value, dependentPosition, command.DependentJobProfileId, cancellationToken);

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
            return Result<JobProfileDependentPositionResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileDependentPositionResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<JobProfileDependentPositionResponse> BuildResponseForDependentInternalAsync(
        Guid tenantId,
        JobProfileDependentPosition dependentPosition,
        CancellationToken cancellationToken)
    {
        var dependentProfile = dependentPosition.DependentJobProfile is not null
            ? new JobProfileReferenceResponse(
                dependentPosition.DependentJobProfile.PublicId,
                dependentPosition.DependentJobProfile.Code,
                dependentPosition.DependentJobProfile.Title)
            : await repository.GetReferenceByInternalIdAsync(tenantId, dependentPosition.DependentJobProfileId, cancellationToken)
                ?? throw new InvalidOperationException("Dependent profile reference could not be resolved.");
        return dependentPosition.ToResponse(dependentProfile);
    }

    private async Task<JobProfileDependentPositionResponse> BuildResponseForDependentPublicAsync(
        Guid tenantId,
        JobProfileDependentPosition dependentPosition,
        Guid dependentJobProfileId,
        CancellationToken cancellationToken)
    {
        var dependentProfile = await repository.GetReferenceByIdAsync(tenantId, dependentJobProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Dependent profile reference could not be resolved.");
        return dependentPosition.ToResponse(dependentProfile);
    }
}

internal sealed class PatchJobProfileDependentPositionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchJobProfileDependentPositionCommand, JobProfileDependentPositionResponse>
{
    public async Task<Result<JobProfileDependentPositionResponse>> Handle(PatchJobProfileDependentPositionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithDependentPositionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var dependentPosition = profile.DependentPositions.FirstOrDefault(item => item.PublicId == command.DependentPositionId);
        if (dependentPosition is null)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.DependentPositionNotFound);
        }

        var before = await repository.GetDependentPositionResponseAsync(profile.PublicId, dependentPosition.PublicId, cancellationToken)
            ?? await BuildCurrentResponseAsync(tenantContext.TenantId.Value, dependentPosition, cancellationToken);
        var patchState = JobProfileDependentPositionPatchState.From(before);
        var patchApplication = JobProfileDependentPositionPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(patchApplication.Error);
        }

        var validation = JobProfileDependentPositionPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(validation.Error);
        }

        if (patchState.ConcurrencyToken != dependentPosition.ConcurrencyToken)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        if (!patchState.HasMutation)
        {
            return Result<JobProfileDependentPositionResponse>.Success(before);
        }

        if (command.JobProfileId == patchState.DependentJobProfilePublicId)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.DependencyCycle);
        }

        var dependentInternalId = await repository.ResolveProfileIdAsync(tenantContext.TenantId.Value, patchState.DependentJobProfilePublicId, cancellationToken);
        if (!dependentInternalId.HasValue)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(patchState.DependentJobProfilePublicId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var dependentInternalIds = profile.DependentPositions
            .Where(item => item.PublicId != command.DependentPositionId)
            .Select(item => item.DependentJobProfileId)
            .ToList();
        dependentInternalIds.Add(dependentInternalId.Value);

        var graph = await repository.GetDependencyGraphAsync(tenantContext.TenantId.Value, cancellationToken);
        if (JobProfileDependencyAnalyzer.WouldCreateDependentCycle(profile.Id, dependentInternalIds, graph))
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.DependencyCycle);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            dependentPosition.Update(
                dependentInternalId.Value,
                patchState.Quantity,
                patchState.Notes);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetDependentPositionResponseAsync(profile.PublicId, dependentPosition.PublicId, cancellationToken)
                ?? await BuildResponseForPublicAsync(tenantContext.TenantId.Value, dependentPosition, patchState.DependentJobProfilePublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Patched dependent position in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileDependentPositionResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileDependentPositionResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<JobProfileDependentPositionResponse> BuildCurrentResponseAsync(
        Guid tenantId,
        JobProfileDependentPosition dependentPosition,
        CancellationToken cancellationToken)
    {
        var dependentProfile = dependentPosition.DependentJobProfile is not null
            ? new JobProfileReferenceResponse(
                dependentPosition.DependentJobProfile.PublicId,
                dependentPosition.DependentJobProfile.Code,
                dependentPosition.DependentJobProfile.Title)
            : await repository.GetReferenceByInternalIdAsync(tenantId, dependentPosition.DependentJobProfileId, cancellationToken)
                ?? throw new InvalidOperationException("Dependent profile reference could not be resolved.");
        return dependentPosition.ToResponse(dependentProfile);
    }

    private async Task<JobProfileDependentPositionResponse> BuildResponseForPublicAsync(
        Guid tenantId,
        JobProfileDependentPosition dependentPosition,
        Guid dependentJobProfileId,
        CancellationToken cancellationToken)
    {
        var dependentProfile = await repository.GetReferenceByIdAsync(tenantId, dependentJobProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Dependent profile reference could not be resolved.");
        return dependentPosition.ToResponse(dependentProfile);
    }
}

internal sealed class RemoveJobProfileDependentPositionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileDependentPositionCommand, JobProfileDependentPositionResponse>
{
    public async Task<Result<JobProfileDependentPositionResponse>> Handle(RemoveJobProfileDependentPositionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithDependentPositionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var dependentPosition = profile.DependentPositions.FirstOrDefault(item => item.PublicId == command.DependentPositionId);
        if (dependentPosition is null)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.DependentPositionNotFound);
        }

        if (dependentPosition.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileDependentPositionResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetDependentPositionResponseAsync(profile.PublicId, dependentPosition.PublicId, cancellationToken)
            ?? await BuildResponseAsync(tenantContext.TenantId.Value, dependentPosition, cancellationToken);

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
            return Result<JobProfileDependentPositionResponse>.Success(before);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileDependentPositionResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<JobProfileDependentPositionResponse> BuildResponseAsync(
        Guid tenantId,
        JobProfileDependentPosition dependentPosition,
        CancellationToken cancellationToken)
    {
        var dependentProfile = dependentPosition.DependentJobProfile is not null
            ? new JobProfileReferenceResponse(
                dependentPosition.DependentJobProfile.PublicId,
                dependentPosition.DependentJobProfile.Code,
                dependentPosition.DependentJobProfile.Title)
            : await repository.GetReferenceByInternalIdAsync(tenantId, dependentPosition.DependentJobProfileId, cancellationToken)
                ?? throw new InvalidOperationException("Dependent profile reference could not be resolved.");
        return dependentPosition.ToResponse(dependentProfile);
    }
}

internal sealed class JobProfileDependentPositionPatchState
{
    public Guid DependentJobProfilePublicId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public bool ConcurrencyTokenTouched { get; set; }
    public bool HasMutation { get; set; }

    public static JobProfileDependentPositionPatchState From(JobProfileDependentPositionResponse response) =>
        new()
        {
            DependentJobProfilePublicId = response.DependentJobProfileId,
            Quantity = response.Quantity,
            Notes = response.Notes,
            ConcurrencyToken = response.ConcurrencyToken
        };
}

internal static class JobProfileDependentPositionPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfileDependentPositionPatchOperation> operations, JobProfileDependentPositionPatchState state)
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
                return ValidationFailure(operation.Path, "Only root dependent position properties can be patched.");
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

    public static Result Validate(JobProfileDependentPositionPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!state.ConcurrencyTokenTouched)
        {
            errors["concurrencyToken"] = ["ConcurrencyToken is required."];
        }
        else if (state.ConcurrencyToken == Guid.Empty)
        {
            errors["concurrencyToken"] = ["ConcurrencyToken must be a valid UUID."];
        }

        if (state.DependentJobProfilePublicId == Guid.Empty)
        {
            errors["dependentJobProfileId"] = ["DependentJobProfileId is required."];
        }

        if (state.Quantity < 0)
        {
            errors["quantity"] = ["Quantity must be greater than or equal to 0."];
        }

        if (state.Notes is not null && state.Notes.Length > 1000)
        {
            errors["notes"] = ["Notes must be 1000 characters or fewer."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        JobProfileDependentPositionPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsAnySegment(property, "dependentJobProfileId", "dependentJobProfilePublicId"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "DependentJobProfileId cannot be removed.");
            }

            state.DependentJobProfilePublicId = ReadRequiredGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "quantity"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Quantity cannot be removed.");
            }

            state.Quantity = ReadInt(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "notes"))
        {
            state.Notes = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "concurrencyToken"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "ConcurrencyToken cannot be removed.");
            }

            state.ConcurrencyToken = ReadRequiredGuid(value, path);
            state.ConcurrencyTokenTouched = true;
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

    private static Guid ReadRequiredGuid(JsonElement? value, string path)
    {
        var raw = ReadNullableString(value, path);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JobProfilePatchValueException(path, "Value must be a valid UUID.");
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
