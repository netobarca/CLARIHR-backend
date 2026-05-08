using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record AddJobProfileCompetencyCommand(
    Guid JobProfileId,
    Guid? CatalogItemId,
    string? Name,
    string? ExpectedLevel,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileResponse>;

public sealed record UpdateJobProfileCompetencyCommand(
    Guid JobProfileId,
    Guid CompetencyId,
    Guid? CatalogItemId,
    string? Name,
    string? ExpectedLevel,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileResponse>;

public sealed record RemoveJobProfileCompetencyCommand(
    Guid JobProfileId,
    Guid CompetencyId,
    Guid ConcurrencyToken) : ICommand<JobProfileResponse>;

internal sealed class AddJobProfileCompetencyCommandValidator : AbstractValidator<AddJobProfileCompetencyCommand>
{
    public AddJobProfileCompetencyCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemId.HasValue);
        RuleFor(command => command.Name).MaximumLength(200);
        RuleFor(command => command.ExpectedLevel).MaximumLength(100);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class UpdateJobProfileCompetencyCommandValidator : AbstractValidator<UpdateJobProfileCompetencyCommand>
{
    public UpdateJobProfileCompetencyCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CompetencyId).NotEmpty();
        RuleFor(command => command.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemId.HasValue);
        RuleFor(command => command.Name).MaximumLength(200);
        RuleFor(command => command.ExpectedLevel).MaximumLength(100);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class RemoveJobProfileCompetencyCommandValidator : AbstractValidator<RemoveJobProfileCompetencyCommand>
{
    public RemoveJobProfileCompetencyCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CompetencyId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddJobProfileCompetencyCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<AddJobProfileCompetencyCommand, JobProfileResponse>
{
    public async Task<Result<JobProfileResponse>> Handle(AddJobProfileCompetencyCommand command, CancellationToken cancellationToken)
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

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileResponse>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItem?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileResponse>.Failure(new Error("JobProfileCompetency.NameRequired", "A name is required for the competency.", ErrorType.Validation));
        }

        var before = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile response could not be resolved.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var competency = JobProfileCompetency.Create(
                catalogItemInternalId,
                null,
                name,
                command.ExpectedLevel,
                command.Notes,
                command.SortOrder);

            profile.AddCompetency(competency);

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
                    $"Added competency to job profile {profile.Code}.",
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

internal sealed class UpdateJobProfileCompetencyCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<UpdateJobProfileCompetencyCommand, JobProfileResponse>
{
    public async Task<Result<JobProfileResponse>> Handle(UpdateJobProfileCompetencyCommand command, CancellationToken cancellationToken)
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

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileResponse>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItem?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileResponse>.Failure(new Error("JobProfileCompetency.NameRequired", "A name is required for the competency.", ErrorType.Validation));
        }

        var before = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile response could not be resolved.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var competency = profile.GetCompetency(command.CompetencyId);
            
            competency.Update(
                catalogItemInternalId,
                null,
                name,
                command.ExpectedLevel,
                command.Notes,
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
                    $"Updated competency in job profile {profile.Code}.",
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

internal sealed class RemoveJobProfileCompetencyCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileCompetencyCommand, JobProfileResponse>
{
    public async Task<Result<JobProfileResponse>> Handle(RemoveJobProfileCompetencyCommand command, CancellationToken cancellationToken)
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
            var competency = profile.GetCompetency(command.CompetencyId);
            profile.RemoveCompetency(competency);

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
                    $"Removed competency from job profile {profile.Code}.",
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
