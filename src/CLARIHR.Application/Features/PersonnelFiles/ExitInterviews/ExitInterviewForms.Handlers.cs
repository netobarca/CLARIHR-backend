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

internal sealed class CreateExitInterviewFormCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateExitInterviewFormCommand, ExitInterviewFormResponse>
{
    public async Task<Result<ExitInterviewFormResponse>> Handle(CreateExitInterviewFormCommand command, CancellationToken cancellationToken)
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

        var normalizedName = command.Name.Trim().ToUpperInvariant();
        if (await repository.FormNameExistsAsync(tenantId, normalizedName, excludingFormPublicId: null, cancellationToken))
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormNameDuplicate);
        }

        var form = ExitInterviewForm.Create(command.Name, command.Description, command.IsAnonymous);
        form.SetTenantId(tenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddForm(form);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetFormResponseAsync(tenantId, form.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Exit-interview form response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ExitInterviewFormCreated,
                    AuditEntityTypes.ExitInterviewForm,
                    form.PublicId,
                    form.Name,
                    AuditActions.Create,
                    $"Created exit-interview form {form.Name}.",
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

internal sealed class SaveExitInterviewFormDefinitionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SaveExitInterviewFormDefinitionCommand, ExitInterviewFormResponse>
{
    public async Task<Result<ExitInterviewFormResponse>> Handle(SaveExitInterviewFormDefinitionCommand command, CancellationToken cancellationToken)
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

        var normalizedName = command.Name.Trim().ToUpperInvariant();
        if (await repository.FormNameExistsAsync(tenantId, normalizedName, excludingFormPublicId: form.PublicId, cancellationToken))
        {
            return Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormNameDuplicate);
        }

        // Validate the submitted definition (coherence) before mutating anything.
        var capabilities = await repository.GetControlTypeCapabilitiesAsync(tenantId, cancellationToken);
        var validation = ValidateDefinition(command, capabilities);
        if (validation.IsFailure)
        {
            return Result<ExitInterviewFormResponse>.Failure(validation.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            form.UpdateDefinition(command.Name, command.Description, command.IsAnonymous);
            await repository.RemoveDefinitionChildrenAsync(tenantId, form.Id, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            // Groups first (to obtain their generated ids for field → group linkage).
            var groupEntities = command.Groups
                .Select(input =>
                {
                    var group = ExitInterviewFormGroup.Create(input.Title, input.Description, input.DisplayOrder);
                    group.BindToForm(form.Id);
                    group.SetTenantId(tenantId);
                    repository.AddGroup(group);
                    return (input.GroupKey, Entity: group);
                })
                .ToList();
            if (groupEntities.Count > 0)
            {
                _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var groupKeyToId = groupEntities.ToDictionary(
                pair => pair.GroupKey.Trim().ToUpperInvariant(),
                pair => pair.Entity.Id,
                StringComparer.Ordinal);

            // Fields next.
            var fieldEntities = command.Fields
                .Select(input =>
                {
                    long? groupId = string.IsNullOrWhiteSpace(input.GroupKey)
                        ? null
                        : groupKeyToId[input.GroupKey.Trim().ToUpperInvariant()];
                    var field = ExitInterviewFormField.Create(
                        groupId,
                        input.ControlTypeCode,
                        input.FieldKey,
                        input.Title,
                        input.Description,
                        input.Weight,
                        input.IsRequired,
                        input.DisplayOrder,
                        input.MinValue,
                        input.MaxValue,
                        input.MaxLength,
                        input.ScaleMax);
                    field.BindToForm(form.Id);
                    field.SetTenantId(tenantId);
                    repository.AddField(field);
                    return (Input: input, Entity: field);
                })
                .ToList();
            if (fieldEntities.Count > 0)
            {
                _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Options last (need field ids).
            foreach (var (input, fieldEntity) in fieldEntities)
            {
                foreach (var optionInput in input.Options)
                {
                    var option = ExitInterviewFormFieldOption.Create(
                        optionInput.OptionCode,
                        optionInput.Label,
                        optionInput.Score,
                        optionInput.DisplayOrder);
                    option.BindToField(fieldEntity.Id);
                    option.SetTenantId(tenantId);
                    repository.AddOption(option);
                }
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetFormResponseAsync(tenantId, form.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Exit-interview form response could not be resolved after save.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ExitInterviewFormUpdated,
                    AuditEntityTypes.ExitInterviewForm,
                    form.PublicId,
                    form.Name,
                    AuditActions.Update,
                    $"Saved exit-interview form definition {form.Name}.",
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

    private static Result ValidateDefinition(
        SaveExitInterviewFormDefinitionCommand command,
        IReadOnlyDictionary<string, ControlTypeCapability> capabilities)
    {
        var groupKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in command.Groups)
        {
            if (!groupKeys.Add(group.GroupKey.Trim().ToUpperInvariant()))
            {
                return Result.Failure(ErrorCatalog.Validation(
                    new Dictionary<string, string[]> { ["groups"] = ["Duplicate group key in the form definition."] }));
            }
        }

        var fieldKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in command.Fields)
        {
            var normalizedFieldKey = field.FieldKey.Trim().ToUpperInvariant();
            if (!fieldKeys.Add(normalizedFieldKey))
            {
                return Result.Failure(ExitInterviewErrors.FieldKeyDuplicate);
            }

            if (!string.IsNullOrWhiteSpace(field.GroupKey)
                && !groupKeys.Contains(field.GroupKey.Trim().ToUpperInvariant()))
            {
                return Result.Failure(ErrorCatalog.Validation(
                    new Dictionary<string, string[]> { ["fields"] = [$"Field '{field.FieldKey}' references an unknown group key."] }));
            }

            if (!capabilities.TryGetValue(field.ControlTypeCode.Trim().ToUpperInvariant(), out var capability))
            {
                return Result.Failure(ExitInterviewErrors.ControlTypeInvalid);
            }

            var configResult = ExitInterviewRules.CheckFieldConfig(capability.SupportsRange, field.MinValue, field.MaxValue);
            if (configResult.IsFailure)
            {
                return configResult;
            }

            if (field.Options.Count > 0)
            {
                var optionsAllowed = ExitInterviewRules.CheckOptionsAllowed(capability.SupportsOptions);
                if (optionsAllowed.IsFailure)
                {
                    return optionsAllowed;
                }

                var optionCodes = new HashSet<string>(StringComparer.Ordinal);
                foreach (var option in field.Options)
                {
                    if (!optionCodes.Add(option.OptionCode.Trim().ToUpperInvariant()))
                    {
                        return Result.Failure(ExitInterviewErrors.OptionCodeDuplicate);
                    }
                }
            }
        }

        return Result.Success();
    }
}

internal sealed class DeleteExitInterviewFormCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteExitInterviewFormCommand, ExitInterviewFormResponse>
{
    public async Task<Result<ExitInterviewFormResponse>> Handle(DeleteExitInterviewFormCommand command, CancellationToken cancellationToken)
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

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            form.DeactivateForReason();
            form.SetActive(false);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetFormResponseAsync(tenantId, form.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Exit-interview form response could not be resolved after delete.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ExitInterviewFormDeleted,
                    AuditEntityTypes.ExitInterviewForm,
                    form.PublicId,
                    form.Name,
                    AuditActions.Delete,
                    $"Deactivated exit-interview form {form.Name}.",
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

internal sealed class GetExitInterviewFormByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetExitInterviewFormByIdQuery, ExitInterviewFormResponse>
{
    public async Task<Result<ExitInterviewFormResponse>> Handle(GetExitInterviewFormByIdQuery query, CancellationToken cancellationToken)
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

        var response = await repository.GetFormResponseAsync(tenantId, query.FormId, cancellationToken);
        return response is null
            ? Result<ExitInterviewFormResponse>.Failure(ExitInterviewErrors.FormNotFound)
            : Result<ExitInterviewFormResponse>.Success(response);
    }
}

internal sealed class ListExitInterviewFormsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<ListExitInterviewFormsQuery, IReadOnlyCollection<ExitInterviewFormListItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<ExitInterviewFormListItemResponse>>> Handle(ListExitInterviewFormsQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IReadOnlyCollection<ExitInterviewFormListItemResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var authorizationResult = await authorizationService.EnsureCanManageExitInterviewFormsAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<ExitInterviewFormListItemResponse>>.Failure(authorizationResult.Error);
        }

        var forms = await repository.ListFormsAsync(tenantId, query.Status, query.ReasonCode, query.Search, cancellationToken);
        return Result<IReadOnlyCollection<ExitInterviewFormListItemResponse>>.Success(forms);
    }
}
