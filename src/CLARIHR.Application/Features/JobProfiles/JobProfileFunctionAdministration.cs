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

public sealed record AddJobProfileFunctionCommand(
    Guid JobProfileId,
    JobFunctionType FunctionType,
    Guid? FrequencyCatalogItemId,
    string Description,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileFunctionResponse>>;

public sealed record UpdateJobProfileFunctionCommand(
    Guid JobProfileId,
    Guid FunctionId,
    JobFunctionType FunctionType,
    Guid? FrequencyCatalogItemId,
    string Description,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileSubResourceResult<JobProfileFunctionResponse>>;

public sealed record RemoveJobProfileFunctionCommand(
    Guid JobProfileId,
    Guid FunctionId,
    Guid ConcurrencyToken) : ICommand<JobProfileParentConcurrencyResult>;

internal sealed class AddJobProfileFunctionCommandValidator : AbstractValidator<AddJobProfileFunctionCommand>
{
    public AddJobProfileFunctionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.FrequencyCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.FrequencyCatalogItemId.HasValue);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class UpdateJobProfileFunctionCommandValidator : AbstractValidator<UpdateJobProfileFunctionCommand>
{
    public UpdateJobProfileFunctionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.FunctionId).NotEmpty();
        RuleFor(command => command.FrequencyCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.FrequencyCatalogItemId.HasValue);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class RemoveJobProfileFunctionCommandValidator : AbstractValidator<RemoveJobProfileFunctionCommand>
{
    public RemoveJobProfileFunctionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.FunctionId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddJobProfileFunctionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository)
    : ICommandHandler<AddJobProfileFunctionCommand, JobProfileSubResourceResult<JobProfileFunctionResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>> Handle(AddJobProfileFunctionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var frequencyInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            tenantContext.TenantId.Value,
            command.FrequencyCatalogItemId,
            PositionDescriptionCatalogType.Frequency,
            PositionDescriptionCatalogErrors.FrequencyNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);

        if (frequencyInternalIdResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(frequencyInternalIdResult.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var function = JobProfileFunction.Create(
                command.FunctionType,
                frequencyInternalIdResult.Value,
                command.Description,
                command.SortOrder);

            profile.AddFunction(function);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            var response = function.ToResponse(command.FrequencyCatalogItemId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added function to job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileFunctionResponse>(response, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobProfileFunctionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository)
    : ICommandHandler<UpdateJobProfileFunctionCommand, JobProfileSubResourceResult<JobProfileFunctionResponse>>
{
    public async Task<Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>> Handle(UpdateJobProfileFunctionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var frequencyInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            tenantContext.TenantId.Value,
            command.FrequencyCatalogItemId,
            PositionDescriptionCatalogType.Frequency,
            PositionDescriptionCatalogErrors.FrequencyNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);

        if (frequencyInternalIdResult.IsFailure)
        {
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(frequencyInternalIdResult.Error);
        }
        var function = profile.GetFunction(command.FunctionId);
        var before = function.ToResponse(command.FrequencyCatalogItemId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            function.Update(
                command.FunctionType,
                frequencyInternalIdResult.Value,
                command.Description,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            var after = function.ToResponse(command.FrequencyCatalogItemId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated function in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Success(
                new JobProfileSubResourceResult<JobProfileFunctionResponse>(after, profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileSubResourceResult<JobProfileFunctionResponse>>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobProfileFunctionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileFunctionCommand, JobProfileParentConcurrencyResult>
{
    public async Task<Result<JobProfileParentConcurrencyResult>> Handle(RemoveJobProfileFunctionCommand command, CancellationToken cancellationToken)
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
        var function = profile.GetFunction(command.FunctionId);
        var before = function.ToResponse(null);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.RemoveFunction(function);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed function from job profile {profile.Code}.",
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
