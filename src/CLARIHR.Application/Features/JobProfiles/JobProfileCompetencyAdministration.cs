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
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>;

public sealed record UpdateJobProfileCompetencyCommand(
    Guid JobProfileId,
    Guid CompetencyId,
    Guid? CatalogItemId,
    string? Name,
    string? ExpectedLevel,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>;

public sealed record RemoveJobProfileCompetencyCommand(
    Guid JobProfileId,
    Guid CompetencyId,
    Guid ConcurrencyToken) : ICommand<JobProfileParentConcurrencyResult>;

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
    : ICommandHandler<AddJobProfileCompetencyCommand, JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>> Handle(AddJobProfileCompetencyCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItem?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(new Error("JobProfileCompetency.NameRequired", "A name is required for the competency.", ErrorType.Validation));
        }

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

            var response = competency.ToLegacyResponse(command.CatalogItemId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added competency to job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>(response, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
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
    : ICommandHandler<UpdateJobProfileCompetencyCommand, JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>> Handle(UpdateJobProfileCompetencyCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var catalogItem = command.CatalogItemId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemId.Value, cancellationToken)
            : null;
        var catalogItemInternalId = catalogItem?.Id;

        if (command.CatalogItemId.HasValue && catalogItem is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var name = string.IsNullOrWhiteSpace(command.Name)
            ? catalogItem?.Name ?? string.Empty
            : command.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(new Error("JobProfileCompetency.NameRequired", "A name is required for the competency.", ErrorType.Validation));
        }

        var competency = profile.GetCompetency(command.CompetencyId);
        var before = competency.ToLegacyResponse(command.CatalogItemId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            competency.Update(
                catalogItemInternalId,
                null,
                name,
                command.ExpectedLevel,
                command.Notes,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = competency.ToLegacyResponse(command.CatalogItemId);

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
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>(after, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
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
    : ICommandHandler<RemoveJobProfileCompetencyCommand, JobProfileParentConcurrencyResult>
{
    public async Task<Result<JobProfileParentConcurrencyResult>> Handle(RemoveJobProfileCompetencyCommand command, CancellationToken cancellationToken)
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

        var competency = profile.GetCompetency(command.CompetencyId);
        var before = competency.ToLegacyResponse();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.RemoveCompetency(competency);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed competency from job profile {profile.Code}.",
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
