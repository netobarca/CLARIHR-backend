using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class PublishExitInterviewFormCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PublishExitInterviewFormCommand, ExitInterviewFormResponse>
{
    public async Task<Result<ExitInterviewFormResponse>> Handle(PublishExitInterviewFormCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<ExitInterviewFormResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var authorizationResult = await authorizationService.EnsureCanManageExitInterviewFormsAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ExitInterviewFormResponse>.Failure(authorizationResult.Error);
        }

        var form = await repository.GetFormEntityAsync(tenantId, command.FormId, cancellationToken);
        if (form is null)
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormNotFound);
        }

        if (form.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormConcurrencyConflict);
        }

        if (form.Status != ExitInterviewFormStatus.Draft)
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormNotDraft);
        }

        var capabilities = await repository.GetControlTypeCapabilitiesAsync(tenantId, cancellationToken);
        var candidates = await repository.GetPublishCandidateFieldsAsync(tenantId, form.Id, cancellationToken);
        var fieldDefinitions = new List<ExitInterviewRules.FieldDefinition>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (!capabilities.TryGetValue(candidate.ControlTypeCode.Trim().ToUpperInvariant(), out var capability))
            {
                return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.ControlTypeInvalid);
            }

            fieldDefinitions.Add(new ExitInterviewRules.FieldDefinition(
                capability.SupportsOptions,
                capability.SupportsRange,
                candidate.MinValue,
                candidate.MaxValue,
                candidate.ActiveOptionCount));
        }

        var publishCheck = ExitInterviewRules.ValidateDefinitionForPublish(fieldDefinitions);
        if (publishCheck.IsFailure)
        {
            return Result<ExitInterviewFormResponse>.Failure(publishCheck.Error);
        }

        return await ExitInterviewFormLifecycleSupport.MutateAndRespondAsync(
            repository,
            auditService,
            unitOfWork,
            tenantId,
            form,
            () => form.Publish(),
            AuditEventTypes.ExitInterviewFormPublished,
            $"Published exit-interview form {form.Name}.",
            cancellationToken);
    }
}

internal sealed class ReopenExitInterviewFormCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReopenExitInterviewFormCommand, ExitInterviewFormResponse>
{
    public async Task<Result<ExitInterviewFormResponse>> Handle(ReopenExitInterviewFormCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<ExitInterviewFormResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var authorizationResult = await authorizationService.EnsureCanManageExitInterviewFormsAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ExitInterviewFormResponse>.Failure(authorizationResult.Error);
        }

        var form = await repository.GetFormEntityAsync(tenantId, command.FormId, cancellationToken);
        if (form is null)
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormNotFound);
        }

        if (form.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormConcurrencyConflict);
        }

        if (form.Status != ExitInterviewFormStatus.Published)
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormNotPublished);
        }

        return await ExitInterviewFormLifecycleSupport.MutateAndRespondAsync(
            repository,
            auditService,
            unitOfWork,
            tenantId,
            form,
            () => form.ReopenForEditing(),
            AuditEventTypes.ExitInterviewFormVersionCreated,
            $"Reopened exit-interview form {form.Name} for editing (version {form.Version}).",
            cancellationToken);
    }
}

internal sealed class ArchiveExitInterviewFormCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveExitInterviewFormCommand, ExitInterviewFormResponse>
{
    public async Task<Result<ExitInterviewFormResponse>> Handle(ArchiveExitInterviewFormCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<ExitInterviewFormResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var authorizationResult = await authorizationService.EnsureCanManageExitInterviewFormsAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ExitInterviewFormResponse>.Failure(authorizationResult.Error);
        }

        var form = await repository.GetFormEntityAsync(tenantId, command.FormId, cancellationToken);
        if (form is null)
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormNotFound);
        }

        if (form.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormConcurrencyConflict);
        }

        return await ExitInterviewFormLifecycleSupport.MutateAndRespondAsync(
            repository,
            auditService,
            unitOfWork,
            tenantId,
            form,
            () => form.Archive(),
            AuditEventTypes.ExitInterviewFormArchived,
            $"Archived exit-interview form {form.Name}.",
            cancellationToken);
    }
}

internal sealed class AssignExitInterviewFormReasonCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    IPersonnelFileRepository personnelFileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AssignExitInterviewFormReasonCommand, ExitInterviewFormResponse>
{
    public async Task<Result<ExitInterviewFormResponse>> Handle(AssignExitInterviewFormReasonCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<ExitInterviewFormResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var authorizationResult = await authorizationService.EnsureCanManageExitInterviewFormsAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ExitInterviewFormResponse>.Failure(authorizationResult.Error);
        }

        var form = await repository.GetFormEntityAsync(tenantId, command.FormId, cancellationToken);
        if (form is null)
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormNotFound);
        }

        if (form.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormConcurrencyConflict);
        }

        if (form.Status != ExitInterviewFormStatus.Published)
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormNotPublished);
        }

        var companyCountry = await personnelFileRepository.GetCompanyCountryCodeAsync(tenantId, cancellationToken);
        var normalizedCountry = string.IsNullOrWhiteSpace(companyCountry) ? "SV" : companyCountry.Trim().ToUpperInvariant();
        var reasonActive = await personnelFileRepository.ReferenceCatalogCodeIsActiveAsync(
            normalizedCountry,
            PersonnelReferenceCatalogCategories.RetirementReason,
            command.ReasonCode,
            cancellationToken);
        if (!reasonActive)
        {
            return Result<ExitInterviewFormResponse>.Failure(ErrorCatalog.Validation(
                new Dictionary<string, string[]> { ["reasonCode"] = [$"Retirement reason '{command.ReasonCode.Trim().ToUpperInvariant()}' is not active."] }));
        }

        var existingActive = await repository.GetActiveFormForReasonAsync(tenantId, command.ReasonCode, form.PublicId, cancellationToken);

        return await ExitInterviewFormLifecycleSupport.MutateAndRespondAsync(
            repository,
            auditService,
            unitOfWork,
            tenantId,
            form,
            () =>
            {
                existingActive?.DeactivateForReason();
                form.AssignReason(command.ReasonCode);
            },
            AuditEventTypes.ExitInterviewFormReasonAssigned,
            $"Associated exit-interview form {form.Name} to retirement reason {command.ReasonCode.Trim().ToUpperInvariant()}.",
            cancellationToken);
    }
}

internal sealed class ResolveApplicableExitInterviewFormQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<ResolveApplicableExitInterviewFormQuery, ExitInterviewApplicableFormResponse>
{
    public async Task<Result<ExitInterviewApplicableFormResponse>> Handle(ResolveApplicableExitInterviewFormQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<ExitInterviewApplicableFormResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var authorizationResult = await authorizationService.EnsureCanManageExitInterviewFormsAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ExitInterviewApplicableFormResponse>.Failure(authorizationResult.Error);
        }

        var activeForm = await repository.GetActiveFormForReasonAsync(tenantId, query.ReasonCode, excludingFormPublicId: null, cancellationToken);
        if (activeForm is null)
        {
            return Result<ExitInterviewApplicableFormResponse>.Success(new ExitInterviewApplicableFormResponse(false, null));
        }

        var response = await repository.GetFormResponseAsync(tenantId, activeForm.PublicId, cancellationToken);
        return Result<ExitInterviewApplicableFormResponse>.Success(new ExitInterviewApplicableFormResponse(response is not null, response));
    }
}

/// <summary>Shared transaction + audit + response plumbing for exit-interview form lifecycle mutations.</summary>
internal static class ExitInterviewFormLifecycleSupport
{
    public static async Task<Result<ExitInterviewFormResponse>> MutateAndRespondAsync(
        IExitInterviewRepository repository,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        Guid tenantId,
        ExitInterviewForm form,
        Action mutate,
        string auditEventType,
        string auditSummary,
        CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            mutate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetFormResponseAsync(tenantId, form.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Exit-interview form response could not be resolved after a lifecycle change.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    auditEventType,
                    AuditEntityTypes.ExitInterviewForm,
                    form.PublicId,
                    form.Name,
                    AuditActions.Update,
                    auditSummary,
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<ExitInterviewFormResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
