using CLARIHR.Application.Abstractions.Auditing;
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

/// <summary>
/// H-01 — the job profile STATE TRANSITIONS: publish, reopen (with a mandatory reason) and archive.
/// <para>
/// They live apart from <c>JobProfileAdministration</c> because they answer to a different grant —
/// <c>JobProfiles.Publish</c>, which <c>JobProfiles.Admin</c> deliberately does NOT imply — so whoever
/// drafts a descriptor is not automatically the one who approves it. Publishing FREEZES the descriptor
/// (the profile core and its 9 collections); reopening is the only way back to an editable draft, and it
/// leaves existing position slots and their occupants untouched: the state governs new downstream writes,
/// never records that already exist.
/// </para>
/// </summary>
public sealed record PublishJobProfileCommand(
    Guid JobProfileId,
    Guid ConcurrencyToken) : ICommand<JobProfileCoreResponse>;

public sealed record ReopenJobProfileCommand(
    Guid JobProfileId,
    string Reason,
    Guid ConcurrencyToken) : ICommand<JobProfileCoreResponse>;

public sealed record ArchiveJobProfileCommand(
    Guid JobProfileId,
    Guid ConcurrencyToken) : ICommand<JobProfileCoreResponse>;

internal sealed class PublishJobProfileCommandValidator : AbstractValidator<PublishJobProfileCommand>
{
    public PublishJobProfileCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ReopenJobProfileCommandValidator : AbstractValidator<ReopenJobProfileCommand>
{
    public ReopenJobProfileCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();

        // The reason is what makes reopening auditable — it is the change-control record of why an
        // approved descriptor was unfrozen, so it is not optional.
        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(JobProfileResolutionRules.MaxReasonLength);
    }
}

internal sealed class ArchiveJobProfileCommandValidator : AbstractValidator<ArchiveJobProfileCommand>
{
    public ArchiveJobProfileCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal static class JobProfileResolutionRules
{
    public const int MaxReasonLength = 500;
}

/// <summary>
/// Shared pipeline of the three transitions: permission → load → concurrency token → domain transition →
/// save → audit. The order is fixed and mirrors <c>PayrollRunReview</c>: the state check lives in the
/// aggregate, so a transition never has to re-derive it here.
/// </summary>
internal static class JobProfileTransitionExecutor
{
    public static async Task<Result<JobProfileCoreResponse>> ExecuteAsync(
        Guid jobProfileId,
        Guid concurrencyToken,
        Action<JobProfile> transition,
        Func<JobProfile, (string EventType, string Action, string Description)> auditFactory,
        IJobProfileAuthorizationService authorizationService,
        IJobProfileRepository repository,
        IAuditService auditService,
        ITenantContext tenantContext,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileCoreResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanPublishProfilesAsync(
            tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(authorizationResult.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var profile = await repository.GetByIdAsync(jobProfileId, cancellationToken);
            if (profile is null)
            {
                var error = await repository.ExistsOutsideTenantAsync(jobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound;

                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(error);
            }

            if (profile.ConcurrencyToken != concurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
            }

            var before = await repository.GetCoreResponseByIdAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile response could not be resolved before the transition.");

            var (eventType, action, description) = auditFactory(profile);

            try
            {
                transition(profile);
            }
            catch (JobProfileStateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.StateRuleViolation);
            }
            catch (InvalidOperationException)
            {
                // Publish() enforces its four content preconditions with plain InvalidOperationException.
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.PublishRequirementsMissing);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetCoreResponseByIdAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile response could not be resolved after the transition.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    eventType,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    action,
                    description,
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCoreResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PublishJobProfileCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PublishJobProfileCommand, JobProfileCoreResponse>
{
    public Task<Result<JobProfileCoreResponse>> Handle(PublishJobProfileCommand command, CancellationToken cancellationToken) =>
        JobProfileTransitionExecutor.ExecuteAsync(
            command.JobProfileId,
            command.ConcurrencyToken,
            static profile => profile.Publish(),
            static profile => (
                AuditEventTypes.JobProfilePublished,
                AuditActions.Update,
                $"Published job profile {profile.Code}."),
            authorizationService, repository, auditService, tenantContext, unitOfWork, cancellationToken);
}

internal sealed class ReopenJobProfileCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReopenJobProfileCommand, JobProfileCoreResponse>
{
    public Task<Result<JobProfileCoreResponse>> Handle(ReopenJobProfileCommand command, CancellationToken cancellationToken)
    {
        var reason = command.Reason.Trim();

        return JobProfileTransitionExecutor.ExecuteAsync(
            command.JobProfileId,
            command.ConcurrencyToken,
            static profile => profile.Reopen(),
            profile => (
                AuditEventTypes.JobProfileReopened,
                AuditActions.Update,
                $"Reopened job profile {profile.Code} for editing. Reason: {reason}"),
            authorizationService, repository, auditService, tenantContext, unitOfWork, cancellationToken);
    }
}

internal sealed class ArchiveJobProfileCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveJobProfileCommand, JobProfileCoreResponse>
{
    public Task<Result<JobProfileCoreResponse>> Handle(ArchiveJobProfileCommand command, CancellationToken cancellationToken) =>
        JobProfileTransitionExecutor.ExecuteAsync(
            command.JobProfileId,
            command.ConcurrencyToken,
            static profile => profile.Archive(),
            static profile => (
                AuditEventTypes.JobProfileArchived,
                AuditActions.Archive,
                $"Archived job profile {profile.Code}."),
            authorizationService, repository, auditService, tenantContext, unitOfWork, cancellationToken);
}
