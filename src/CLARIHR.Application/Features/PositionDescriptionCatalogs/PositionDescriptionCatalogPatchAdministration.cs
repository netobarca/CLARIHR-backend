using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using FluentValidation;

namespace CLARIHR.Application.Features.PositionDescriptionCatalogs;

public sealed record PositionDescriptionCatalogPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPositionDescriptionCatalogItemCommand(
    Guid ItemId,
    PositionDescriptionCatalogType CatalogType,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PositionDescriptionCatalogPatchOperation> Operations)
    : ICommand<PositionDescriptionCatalogItemResponse>;

public sealed record PatchPositionCategoryClassificationCommand(
    Guid ClassificationId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PositionDescriptionCatalogPatchOperation> Operations)
    : ICommand<PositionCategoryClassificationResponse>;

public sealed record PatchPositionCategoryCommand(
    Guid CategoryId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PositionDescriptionCatalogPatchOperation> Operations)
    : ICommand<PositionCategoryResponse>;

internal sealed class PatchPositionDescriptionCatalogItemCommandValidator : AbstractValidator<PatchPositionDescriptionCatalogItemCommand>
{
    public PatchPositionDescriptionCatalogItemCommandValidator()
    {
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.CatalogType)
            .Must(PositionDescriptionCatalogErrors.IsSimpleCatalogType)
            .WithMessage("Unsupported catalog type.");
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class PatchPositionCategoryClassificationCommandValidator : AbstractValidator<PatchPositionCategoryClassificationCommand>
{
    public PatchPositionCategoryClassificationCommandValidator()
    {
        RuleFor(command => command.ClassificationId).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class PatchPositionCategoryCommandValidator : AbstractValidator<PatchPositionCategoryCommand>
{
    public PatchPositionCategoryCommandValidator()
    {
        RuleFor(command => command.CategoryId).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class PatchPositionDescriptionCatalogItemCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchPositionDescriptionCatalogItemCommand, PositionDescriptionCatalogItemResponse>
{
    public async Task<Result<PositionDescriptionCatalogItemResponse>> Handle(
        PatchPositionDescriptionCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetCatalogItemByIdAsync(command.ItemId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(
                await repository.ExistsCatalogItemOutsideTenantAsync(command.ItemId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.CatalogItemNotFound);
        }

        if (entity.CatalogType != command.CatalogType)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(PositionDescriptionCatalogErrors.CatalogItemNotFound);
        }

        var before = await repository.GetCatalogItemResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Catalog item response could not be resolved before patch.");
        var patchState = PositionDescriptionCatalogItemPatchState.From(before);
        var patchApplication = PositionDescriptionCatalogItemPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(patchApplication.Error);
        }

        var validation = PositionDescriptionCatalogItemPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(validation.Error);
        }

        if (command.ConcurrencyToken != entity.ConcurrencyToken)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        if (!patchState.HasMutation)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Success(before);
        }

        if (!patchState.HasScalarMutation && patchState.IsActiveTouched && patchState.IsActive == entity.IsActive)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Success(before);
        }

        var normalizedCode = patchState.Code.Trim().ToUpperInvariant();
        if (patchState.HasScalarMutation &&
            await repository.CatalogItemCodeExistsAsync(entity.TenantId, entity.CatalogType, normalizedCode, entity.Id, cancellationToken))
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(PositionDescriptionCatalogErrors.CatalogCodeConflict);
        }

        if (entity.IsActive && patchState.IsActiveTouched && !patchState.IsActive &&
            await IsCatalogItemInUseAsync(entity, repository, cancellationToken))
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(PositionDescriptionCatalogErrors.CatalogInUse);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (patchState.HasScalarMutation)
            {
                entity.Update(patchState.Code, patchState.Name, patchState.Description, patchState.SortOrder);
            }

            if (patchState.IsActiveTouched && patchState.IsActive != entity.IsActive)
            {
                if (patchState.IsActive)
                {
                    entity.Activate();
                }
                else
                {
                    entity.Inactivate();
                }
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            PositionDescriptionCatalogCacheInvalidation.InvalidateSimple(repository, entity.TenantId, entity.CatalogType);

            var after = await repository.GetCatalogItemResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    ResolveCatalogItemAuditEvent(before, after, patchState),
                    AuditEntityTypes.PositionDescriptionCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    ResolveAuditAction(before, after, patchState),
                    $"Patched position description catalog item {entity.Code} ({entity.CatalogType}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionDescriptionCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static Task<bool> IsCatalogItemInUseAsync(
        PositionDescriptionCatalogItem entity,
        IPositionDescriptionCatalogRepository repository,
        CancellationToken cancellationToken) =>
        entity.CatalogType switch
        {
            PositionDescriptionCatalogType.PositionFunctionType => repository.HasClassificationsUsingCatalogItemAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.PositionContractType => repository.HasClassificationsUsingCatalogItemAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.Frequency => repository.HasFunctionsUsingFrequencyAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.RequirementType => repository.HasRequirementsUsingRequirementTypeAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.WorkConditionType => repository.HasWorkConditionsUsingWorkConditionTypeAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.WorkCondition => repository.HasWorkConditionsUsingWorkConditionAsync(entity.Id, cancellationToken),
            // Competency domains are referenced by personnel-file curricular competencies *by code* (no inverse
            // FK from PositionDescriptionCatalogs → PersonnelFiles), so usage cannot be probed here; prefer
            // inactivation over deletion (historical rows keep their code). See plan §R-T3.
            PositionDescriptionCatalogType.CompetencyDomain => Task.FromResult(false),
            _ => repository.HasJobProfilesUsingCatalogItemAsync(entity.Id, cancellationToken)
        };

    private static string ResolveCatalogItemAuditEvent(
        PositionDescriptionCatalogItemResponse before,
        PositionDescriptionCatalogItemResponse after,
        PositionDescriptionCatalogItemPatchState patchState)
    {
        if (!patchState.HasScalarMutation && before.IsActive != after.IsActive)
        {
            return after.IsActive
                ? AuditEventTypes.PositionDescriptionCatalogItemActivated
                : AuditEventTypes.PositionDescriptionCatalogItemInactivated;
        }

        return AuditEventTypes.PositionDescriptionCatalogItemUpdated;
    }

    private static string ResolveAuditAction(
        PositionDescriptionCatalogItemResponse before,
        PositionDescriptionCatalogItemResponse after,
        PositionDescriptionCatalogItemPatchState patchState)
    {
        if (!patchState.HasScalarMutation && before.IsActive != after.IsActive)
        {
            return after.IsActive ? AuditActions.Reactivate : AuditActions.Deactivate;
        }

        return AuditActions.Update;
    }
}

internal sealed class PatchPositionCategoryClassificationCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchPositionCategoryClassificationCommand, PositionCategoryClassificationResponse>
{
    public async Task<Result<PositionCategoryClassificationResponse>> Handle(
        PatchPositionCategoryClassificationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetClassificationByIdAsync(command.ClassificationId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(
                await repository.ExistsClassificationOutsideTenantAsync(command.ClassificationId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.ClassificationNotFound);
        }

        var before = await repository.GetClassificationResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Classification response could not be resolved before patch.");
        var patchState = PositionCategoryClassificationPatchState.From(before);
        var patchApplication = PositionCategoryClassificationPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(patchApplication.Error);
        }

        var validation = PositionCategoryClassificationPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(validation.Error);
        }

        if (command.ConcurrencyToken != entity.ConcurrencyToken)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        if (!patchState.HasMutation)
        {
            return Result<PositionCategoryClassificationResponse>.Success(before);
        }

        if (!patchState.HasScalarMutation && patchState.IsActiveTouched && patchState.IsActive == entity.IsActive)
        {
            return Result<PositionCategoryClassificationResponse>.Success(before);
        }

        CatalogReferenceInternal? positionFunctionLookup = null;
        CatalogReferenceInternal? contractTypeLookup = null;
        CatalogReferenceInternal? orgUnitTypeLookup = null;
        if (patchState.HasScalarMutation)
        {
            positionFunctionLookup = await repository.GetActiveCatalogReferenceAsync(
                entity.TenantId,
                PositionDescriptionCatalogType.PositionFunctionType,
                patchState.PositionFunctionTypePublicId,
                cancellationToken);
            contractTypeLookup = await repository.GetActiveCatalogReferenceAsync(
                entity.TenantId,
                PositionDescriptionCatalogType.PositionContractType,
                patchState.PositionContractTypePublicId,
                cancellationToken);
            orgUnitTypeLookup = await repository.GetActiveOrgUnitTypeReferenceAsync(
                entity.TenantId,
                patchState.OrgUnitTypePublicId,
                cancellationToken);

            if (positionFunctionLookup is null || contractTypeLookup is null)
            {
                return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound);
            }

            if (orgUnitTypeLookup is null)
            {
                return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.OrgUnitTypeNotFound);
            }

            var normalizedCode = patchState.Code.Trim().ToUpperInvariant();
            if (await repository.ClassificationCodeExistsAsync(entity.TenantId, normalizedCode, entity.Id, cancellationToken))
            {
                return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ClassificationCodeConflict);
            }

            if (await repository.ClassificationAxesExistsAsync(
                    entity.TenantId,
                    positionFunctionLookup.InternalId,
                    contractTypeLookup.InternalId,
                    orgUnitTypeLookup.InternalId,
                    entity.Id,
                    cancellationToken))
            {
                return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ClassificationDuplicateAxes);
            }
        }

        if (entity.IsActive && patchState.IsActiveTouched && !patchState.IsActive &&
            await repository.HasCategoriesUsingClassificationAsync(entity.Id, cancellationToken))
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ClassificationInUse);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (patchState.HasScalarMutation)
            {
                entity.Update(
                    patchState.Code,
                    patchState.Name,
                    patchState.Description,
                    positionFunctionLookup!.InternalId,
                    contractTypeLookup!.InternalId,
                    orgUnitTypeLookup!.InternalId,
                    patchState.SortOrder);
            }

            if (patchState.IsActiveTouched && patchState.IsActive != entity.IsActive)
            {
                if (patchState.IsActive)
                {
                    entity.Activate();
                }
                else
                {
                    entity.Inactivate();
                }
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            PositionDescriptionCatalogCacheInvalidation.InvalidateClassificationAndDependents(repository, entity.TenantId);

            var after = await repository.GetClassificationResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Classification response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    ResolveClassificationAuditEvent(before, after, patchState),
                    AuditEntityTypes.PositionCategoryClassification,
                    entity.PublicId,
                    entity.Code,
                    ResolveAuditAction(before, after, patchState),
                    $"Patched position category classification {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionCategoryClassificationResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string ResolveClassificationAuditEvent(
        PositionCategoryClassificationResponse before,
        PositionCategoryClassificationResponse after,
        PositionCategoryClassificationPatchState patchState)
    {
        if (!patchState.HasScalarMutation && before.IsActive != after.IsActive)
        {
            return after.IsActive
                ? AuditEventTypes.PositionCategoryClassificationActivated
                : AuditEventTypes.PositionCategoryClassificationInactivated;
        }

        return AuditEventTypes.PositionCategoryClassificationUpdated;
    }

    private static string ResolveAuditAction(
        PositionCategoryClassificationResponse before,
        PositionCategoryClassificationResponse after,
        PositionCategoryClassificationPatchState patchState)
    {
        if (!patchState.HasScalarMutation && before.IsActive != after.IsActive)
        {
            return after.IsActive ? AuditActions.Reactivate : AuditActions.Deactivate;
        }

        return AuditActions.Update;
    }
}

internal sealed class PatchPositionCategoryCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchPositionCategoryCommand, PositionCategoryResponse>
{
    public async Task<Result<PositionCategoryResponse>> Handle(
        PatchPositionCategoryCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionCategoryResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetCategoryByIdAsync(command.CategoryId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionCategoryResponse>.Failure(
                await repository.ExistsCategoryOutsideTenantAsync(command.CategoryId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.CategoryNotFound);
        }

        var before = await repository.GetCategoryResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Category response could not be resolved before patch.");
        var patchState = PositionCategoryPatchState.From(before);
        var patchApplication = PositionCategoryPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<PositionCategoryResponse>.Failure(patchApplication.Error);
        }

        var validation = PositionCategoryPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<PositionCategoryResponse>.Failure(validation.Error);
        }

        if (command.ConcurrencyToken != entity.ConcurrencyToken)
        {
            return Result<PositionCategoryResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        if (!patchState.HasMutation)
        {
            return Result<PositionCategoryResponse>.Success(before);
        }

        if (!patchState.HasScalarMutation && patchState.IsActiveTouched && patchState.IsActive == entity.IsActive)
        {
            return Result<PositionCategoryResponse>.Success(before);
        }

        PositionCategoryClassification? classificationEntity = null;
        if (patchState.HasScalarMutation)
        {
            classificationEntity = await repository.GetClassificationByIdAsync(patchState.ClassificationPublicId, cancellationToken);
            if (classificationEntity is null)
            {
                return Result<PositionCategoryResponse>.Failure(
                    await repository.ExistsClassificationOutsideTenantAsync(patchState.ClassificationPublicId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PositionDescriptionCatalogErrors.ClassificationNotFound);
            }

            var normalizedCode = patchState.Code.Trim().ToUpperInvariant();
            if (await repository.CategoryCodeExistsAsync(entity.TenantId, normalizedCode, entity.Id, cancellationToken))
            {
                return Result<PositionCategoryResponse>.Failure(PositionDescriptionCatalogErrors.CategoryCodeConflict);
            }
        }

        if (entity.IsActive && patchState.IsActiveTouched && !patchState.IsActive &&
            await repository.HasJobProfilesUsingCategoryAsync(entity.Id, cancellationToken))
        {
            return Result<PositionCategoryResponse>.Failure(PositionDescriptionCatalogErrors.CategoryInUse);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (patchState.HasScalarMutation)
            {
                entity.Update(
                    patchState.Code,
                    patchState.Name,
                    patchState.Description,
                    classificationEntity!.Id,
                    patchState.SortOrder);
            }

            if (patchState.IsActiveTouched && patchState.IsActive != entity.IsActive)
            {
                if (patchState.IsActive)
                {
                    entity.Activate();
                }
                else
                {
                    entity.Inactivate();
                }
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            repository.InvalidateCategoryCache(entity.TenantId);

            var after = await repository.GetCategoryResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Category response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    ResolveCategoryAuditEvent(before, after, patchState),
                    AuditEntityTypes.PositionCategory,
                    entity.PublicId,
                    entity.Code,
                    ResolveAuditAction(before, after, patchState),
                    $"Patched position category {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionCategoryResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string ResolveCategoryAuditEvent(
        PositionCategoryResponse before,
        PositionCategoryResponse after,
        PositionCategoryPatchState patchState)
    {
        if (!patchState.HasScalarMutation && before.IsActive != after.IsActive)
        {
            return after.IsActive
                ? AuditEventTypes.PositionCategoryActivated
                : AuditEventTypes.PositionCategoryInactivated;
        }

        return AuditEventTypes.PositionCategoryUpdated;
    }

    private static string ResolveAuditAction(
        PositionCategoryResponse before,
        PositionCategoryResponse after,
        PositionCategoryPatchState patchState)
    {
        if (!patchState.HasScalarMutation && before.IsActive != after.IsActive)
        {
            return after.IsActive ? AuditActions.Reactivate : AuditActions.Deactivate;
        }

        return AuditActions.Update;
    }
}

internal sealed class PositionDescriptionCatalogItemPatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool HasMutation { get; set; }
    public bool HasScalarMutation { get; set; }
    public bool IsActiveTouched { get; set; }

    public static PositionDescriptionCatalogItemPatchState From(PositionDescriptionCatalogItemResponse response) =>
        new()
        {
            Code = response.Code,
            Name = response.Name,
            Description = response.Description,
            SortOrder = response.SortOrder,
            IsActive = response.IsActive
        };
}

internal sealed class PositionCategoryClassificationPatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid PositionFunctionTypePublicId { get; set; }
    public Guid PositionContractTypePublicId { get; set; }
    public Guid OrgUnitTypePublicId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool HasMutation { get; set; }
    public bool HasScalarMutation { get; set; }
    public bool IsActiveTouched { get; set; }

    public static PositionCategoryClassificationPatchState From(PositionCategoryClassificationResponse response) =>
        new()
        {
            Code = response.Code,
            Name = response.Name,
            Description = response.Description,
            PositionFunctionTypePublicId = response.PositionFunctionType.Id,
            PositionContractTypePublicId = response.PositionContractType.Id,
            OrgUnitTypePublicId = response.OrgUnitType.Id,
            SortOrder = response.SortOrder,
            IsActive = response.IsActive
        };
}

internal sealed class PositionCategoryPatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ClassificationPublicId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool HasMutation { get; set; }
    public bool HasScalarMutation { get; set; }
    public bool IsActiveTouched { get; set; }

    public static PositionCategoryPatchState From(PositionCategoryResponse response) =>
        new()
        {
            Code = response.Code,
            Name = response.Name,
            Description = response.Description,
            ClassificationPublicId = response.Classification.Id,
            SortOrder = response.SortOrder,
            IsActive = response.IsActive
        };
}

internal static class PositionDescriptionCatalogItemPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PositionDescriptionCatalogPatchOperation> operations, PositionDescriptionCatalogItemPatchState state) =>
        PositionDescriptionCatalogPatchApplier.Apply(operations, ApplyOperation, state);

    public static Result Validate(PositionDescriptionCatalogItemPatchState state)
    {
        var errors = PositionDescriptionCatalogPatchValidation.ValidateCommon(
            state.Code,
            state.Name,
            state.Description,
            state.SortOrder);

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(string op, string property, JsonElement? value, PositionDescriptionCatalogItemPatchState state, string path)
    {
        var isRemove = PositionDescriptionCatalogPatchApplier.IsRemove(op);

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "code"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "Code cannot be removed.");
            }

            state.Code = PositionDescriptionCatalogPatchApplier.ReadRequiredString(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "name"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "Name cannot be removed.");
            }

            state.Name = PositionDescriptionCatalogPatchApplier.ReadRequiredString(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "description"))
        {
            state.Description = isRemove ? null : PositionDescriptionCatalogPatchApplier.ReadNullableString(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "sortOrder"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "SortOrder cannot be removed.");
            }

            state.SortOrder = PositionDescriptionCatalogPatchApplier.ReadInt(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "isActive"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "IsActive cannot be removed.");
            }

            state.IsActive = PositionDescriptionCatalogPatchApplier.ReadBool(value, path);
            state.HasMutation = true;
            state.IsActiveTouched = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "concurrencyToken"))
        {
            return PositionDescriptionCatalogPatchApplier.ValidationFailure(
                path,
                "ConcurrencyToken cannot be set via the patch document; send the current token in the If-Match header.");
        }

        return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }
}

internal static class PositionCategoryClassificationPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PositionDescriptionCatalogPatchOperation> operations, PositionCategoryClassificationPatchState state) =>
        PositionDescriptionCatalogPatchApplier.Apply(operations, ApplyOperation, state);

    public static Result Validate(PositionCategoryClassificationPatchState state)
    {
        var errors = PositionDescriptionCatalogPatchValidation.ValidateCommon(
            state.Code,
            state.Name,
            state.Description,
            state.SortOrder);

        PositionDescriptionCatalogPatchValidation.AddRequiredGuid(errors, "positionFunctionTypePublicId", state.PositionFunctionTypePublicId);
        PositionDescriptionCatalogPatchValidation.AddRequiredGuid(errors, "positionContractTypePublicId", state.PositionContractTypePublicId);
        PositionDescriptionCatalogPatchValidation.AddRequiredGuid(errors, "orgUnitTypePublicId", state.OrgUnitTypePublicId);

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(string op, string property, JsonElement? value, PositionCategoryClassificationPatchState state, string path)
    {
        var isRemove = PositionDescriptionCatalogPatchApplier.IsRemove(op);

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "code"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "Code cannot be removed.");
            }

            state.Code = PositionDescriptionCatalogPatchApplier.ReadRequiredString(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "name"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "Name cannot be removed.");
            }

            state.Name = PositionDescriptionCatalogPatchApplier.ReadRequiredString(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "description"))
        {
            state.Description = isRemove ? null : PositionDescriptionCatalogPatchApplier.ReadNullableString(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "positionFunctionTypePublicId"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "PositionFunctionTypePublicId cannot be removed.");
            }

            state.PositionFunctionTypePublicId = PositionDescriptionCatalogPatchApplier.ReadRequiredGuid(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "positionContractTypePublicId"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "PositionContractTypePublicId cannot be removed.");
            }

            state.PositionContractTypePublicId = PositionDescriptionCatalogPatchApplier.ReadRequiredGuid(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "orgUnitTypePublicId"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "OrgUnitTypePublicId cannot be removed.");
            }

            state.OrgUnitTypePublicId = PositionDescriptionCatalogPatchApplier.ReadRequiredGuid(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "sortOrder"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "SortOrder cannot be removed.");
            }

            state.SortOrder = PositionDescriptionCatalogPatchApplier.ReadInt(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "isActive"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "IsActive cannot be removed.");
            }

            state.IsActive = PositionDescriptionCatalogPatchApplier.ReadBool(value, path);
            state.HasMutation = true;
            state.IsActiveTouched = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "concurrencyToken"))
        {
            return PositionDescriptionCatalogPatchApplier.ValidationFailure(
                path,
                "ConcurrencyToken cannot be set via the patch document; send the current token in the If-Match header.");
        }

        return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }
}

internal static class PositionCategoryPatchApplier
{
    public static Result Apply(IReadOnlyCollection<PositionDescriptionCatalogPatchOperation> operations, PositionCategoryPatchState state) =>
        PositionDescriptionCatalogPatchApplier.Apply(operations, ApplyOperation, state);

    public static Result Validate(PositionCategoryPatchState state)
    {
        var errors = PositionDescriptionCatalogPatchValidation.ValidateCommon(
            state.Code,
            state.Name,
            state.Description,
            state.SortOrder);

        PositionDescriptionCatalogPatchValidation.AddRequiredGuid(errors, "classificationPublicId", state.ClassificationPublicId);

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(string op, string property, JsonElement? value, PositionCategoryPatchState state, string path)
    {
        var isRemove = PositionDescriptionCatalogPatchApplier.IsRemove(op);

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "code"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "Code cannot be removed.");
            }

            state.Code = PositionDescriptionCatalogPatchApplier.ReadRequiredString(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "name"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "Name cannot be removed.");
            }

            state.Name = PositionDescriptionCatalogPatchApplier.ReadRequiredString(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "description"))
        {
            state.Description = isRemove ? null : PositionDescriptionCatalogPatchApplier.ReadNullableString(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "classificationPublicId"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "ClassificationPublicId cannot be removed.");
            }

            state.ClassificationPublicId = PositionDescriptionCatalogPatchApplier.ReadRequiredGuid(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "sortOrder"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "SortOrder cannot be removed.");
            }

            state.SortOrder = PositionDescriptionCatalogPatchApplier.ReadInt(value, path);
            state.HasMutation = true;
            state.HasScalarMutation = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "isActive"))
        {
            if (isRemove)
            {
                return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, "IsActive cannot be removed.");
            }

            state.IsActive = PositionDescriptionCatalogPatchApplier.ReadBool(value, path);
            state.HasMutation = true;
            state.IsActiveTouched = true;
            return Result.Success();
        }

        if (PositionDescriptionCatalogPatchApplier.IsSegment(property, "concurrencyToken"))
        {
            return PositionDescriptionCatalogPatchApplier.ValidationFailure(
                path,
                "ConcurrencyToken cannot be set via the patch document; send the current token in the If-Match header.");
        }

        return PositionDescriptionCatalogPatchApplier.ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }
}

internal static class PositionDescriptionCatalogPatchValidation
{
    public static Dictionary<string, string[]> ValidateCommon(
        string code,
        string name,
        string? description,
        int sortOrder)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["code"] = ["Code is required."];
        }
        else if (code.Length > 50)
        {
            errors["code"] = ["Code must be 50 characters or fewer."];
        }
        else if (!PositionDescriptionCatalogValidationRules.IsValidCode(code))
        {
            errors["code"] = ["Code format is invalid."];
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (name.Length > 150)
        {
            errors["name"] = ["Name must be 150 characters or fewer."];
        }

        if (description is { Length: > 500 })
        {
            errors["description"] = ["Description must be 500 characters or fewer."];
        }

        if (sortOrder < 0)
        {
            errors["sortOrder"] = ["SortOrder must be greater than or equal to 0."];
        }

        return errors;
    }

    public static void AddRequiredGuid(Dictionary<string, string[]> errors, string fieldName, Guid value)
    {
        if (value == Guid.Empty)
        {
            errors[fieldName] = [$"{fieldName} must be a valid UUID."];
        }
    }
}

internal static class PositionDescriptionCatalogPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply<TState>(
        IReadOnlyCollection<PositionDescriptionCatalogPatchOperation> operations,
        Func<string, string, JsonElement?, TState, string, Result> applyOperation,
        TState state)
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
                return ValidationFailure(operation.Path, "Only root properties can be patched.");
            }

            try
            {
                var result = applyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (PositionDescriptionCatalogPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static bool IsRemove(string op) =>
        string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

    public static bool IsSegment(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    public static string ReadRequiredString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new PositionDescriptionCatalogPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new PositionDescriptionCatalogPatchValueException(path, "Value must be a string.");
    }

    public static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new PositionDescriptionCatalogPatchValueException(path, "Value must be a string or null.");
    }

    public static int ReadInt(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new PositionDescriptionCatalogPatchValueException(path, "Value must be an integer.");
        }

        return value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var parsed)
            ? parsed
            : throw new PositionDescriptionCatalogPatchValueException(path, "Value must be an integer.");
    }

    public static bool ReadBool(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new PositionDescriptionCatalogPatchValueException(path, "Value must be a boolean.");
        }

        return value!.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.Value.GetString(), out var parsed) => parsed,
            _ => throw new PositionDescriptionCatalogPatchValueException(path, "Value must be a boolean.")
        };
    }

    public static Guid ReadRequiredGuid(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new PositionDescriptionCatalogPatchValueException(path, "Value must be a valid UUID.");
        }

        var raw = value!.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new PositionDescriptionCatalogPatchValueException(path, "Value must be a valid UUID.");
        }

        return Guid.TryParse(raw, out var parsed)
            ? parsed
            : throw new PositionDescriptionCatalogPatchValueException(path, "Value must be a valid UUID.");
    }

    public static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));

    private static string[] ParsePath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(UnescapeJsonPointerSegment)
            .ToArray();

    private static string UnescapeJsonPointerSegment(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

    private static bool IsNull(JsonElement? value) =>
        !value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
}

internal sealed class PositionDescriptionCatalogPatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}
