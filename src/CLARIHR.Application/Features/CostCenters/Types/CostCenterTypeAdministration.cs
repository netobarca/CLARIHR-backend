using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.CostCenters;
using FluentValidation;

namespace CLARIHR.Application.Features.CostCenters.Types;

public sealed record CostCenterTypeResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

// List projection of the paged search: same shape as CostCenterTypeResponse minus Description,
// which is detail-only payload (mirror of the CostCenters ListItem/Response split).
public sealed record CostCenterTypeListItemResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record GetCostCenterTypesQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = CostCenterValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<CostCenterTypeListItemResponse>>;

public sealed record GetCostCenterTypeByIdQuery(Guid CostCenterTypeId) : IQuery<CostCenterTypeResponse>;

public sealed record CreateCostCenterTypeCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string? Description) : ICommand<CostCenterTypeResponse>;

public sealed record UpdateCostCenterTypeCommand(
    Guid CostCenterTypeId,
    string Code,
    string Name,
    string? Description,
    Guid ConcurrencyToken) : ICommand<CostCenterTypeResponse>;

public sealed record ActivateCostCenterTypeCommand(Guid CostCenterTypeId, Guid ConcurrencyToken) : ICommand<CostCenterTypeResponse>;

public sealed record InactivateCostCenterTypeCommand(Guid CostCenterTypeId, Guid ConcurrencyToken) : ICommand<CostCenterTypeResponse>;

public sealed record CostCenterTypePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchCostCenterTypeCommand(
    Guid CostCenterTypeId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<CostCenterTypePatchOperation> Operations) : ICommand<CostCenterTypeResponse>;

internal sealed class GetCostCenterTypesQueryValidator : AbstractValidator<GetCostCenterTypesQuery>
{
    public GetCostCenterTypesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(CostCenterValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {CostCenterValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, CostCenterValidationRules.MaxPageSize);
    }
}

internal sealed class GetCostCenterTypeByIdQueryValidator : AbstractValidator<GetCostCenterTypeByIdQuery>
{
    public GetCostCenterTypeByIdQueryValidator()
    {
        RuleFor(query => query.CostCenterTypeId).NotEmpty();
    }
}

internal sealed class CreateCostCenterTypeCommandValidator : AbstractValidator<CreateCostCenterTypeCommand>
{
    public CreateCostCenterTypeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(CostCenterValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

internal sealed class UpdateCostCenterTypeCommandValidator : AbstractValidator<UpdateCostCenterTypeCommand>
{
    public UpdateCostCenterTypeCommandValidator()
    {
        RuleFor(command => command.CostCenterTypeId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(CostCenterValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateCostCenterTypeCommandValidator : AbstractValidator<ActivateCostCenterTypeCommand>
{
    public ActivateCostCenterTypeCommandValidator()
    {
        RuleFor(command => command.CostCenterTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateCostCenterTypeCommandValidator : AbstractValidator<InactivateCostCenterTypeCommand>
{
    public InactivateCostCenterTypeCommandValidator()
    {
        RuleFor(command => command.CostCenterTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchCostCenterTypeCommandValidator : AbstractValidator<PatchCostCenterTypeCommand>
{
    public PatchCostCenterTypeCommandValidator()
    {
        RuleFor(command => command.CostCenterTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class GetCostCenterTypesQueryHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterTypeRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetCostCenterTypesQuery, PagedResponse<CostCenterTypeListItemResponse>>
{
    public async Task<Result<PagedResponse<CostCenterTypeListItemResponse>>> Handle(
        GetCostCenterTypesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<CostCenterTypeListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (query.IncludeAllowedActions)
        {
            // §12.7 (ADR-0001): list AllowedActions derive ONLY from the caller's permission (canManage),
            // never from per-item dependency state — resolving dependencies per row is the forbidden N+1.
            // The real inactivation block is enforced server-side in InactivateCostCenterTypeCommandHandler.
            var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
            var enrichedItems = response.Items
                .Select(costCenterType => CostCenterTypePolicyAdapter.ApplyAllowedActions(costCenterType, resourceActionPolicyService, canManage))
                .ToArray();

            response = new PagedResponse<CostCenterTypeListItemResponse>(
                enrichedItems,
                response.PageNumber,
                response.PageSize,
                response.TotalCount);
        }

        return Result<PagedResponse<CostCenterTypeListItemResponse>>.Success(response);
    }
}

internal static class CostCenterTypePolicyAdapter
{
    public static CostCenterTypeResponse ApplyAllowedActions(
        CostCenterTypeResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with
        {
            AllowedActions = Evaluate(resourceActionPolicyService, response.IsActive, canManage)
        };

    public static CostCenterTypeListItemResponse ApplyAllowedActions(
        CostCenterTypeListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with
        {
            AllowedActions = Evaluate(resourceActionPolicyService, response.IsActive, canManage)
        };

    private static AllowedActionsResponse Evaluate(
        IResourceActionPolicyService resourceActionPolicyService,
        bool isActive,
        bool canManage) =>
        resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                ResourceKey: "CostCenterTypes",
                State: isActive ? "Active" : "Inactive",
                IsActive: isActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));
}

internal sealed class GetCostCenterTypeByIdQueryHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterTypeRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetCostCenterTypeByIdQuery, CostCenterTypeResponse>
{
    public async Task<Result<CostCenterTypeResponse>> Handle(
        GetCostCenterTypeByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CostCenterTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterTypeResponse>.Failure(authorizationResult.Error);
        }

        var costCenterType = await repository.GetByIdAsync(query.CostCenterTypeId, cancellationToken);
        if (costCenterType is not null)
        {
            return Result<CostCenterTypeResponse>.Success(CostCenterTypeMapper.Map(costCenterType));
        }

        return Result<CostCenterTypeResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.CostCenterTypeId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CostCenterErrors.CostCenterTypeNotFound);
    }
}

internal sealed class CreateCostCenterTypeCommandHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterTypeRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCostCenterTypeCommand, CostCenterTypeResponse>
{
    public async Task<Result<CostCenterTypeResponse>> Handle(
        CreateCostCenterTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterTypeResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(command.CompanyId, command.Code.Trim().ToUpperInvariant(), excludingCostCenterTypeId: null, cancellationToken))
        {
            return Result<CostCenterTypeResponse>.Failure(CostCenterErrors.CostCenterTypeCodeConflict);
        }

        var costCenterType = CostCenterType.Create(command.Code, command.Name, command.Description);
        costCenterType.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(costCenterType);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = CostCenterTypeMapper.Map(costCenterType);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CostCenterTypeCreated,
                    AuditEntityTypes.CostCenterType,
                    costCenterType.PublicId,
                    costCenterType.Code,
                    AuditActions.Create,
                    $"Created cost center type {costCenterType.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CostCenterTypeResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException exception)
            when (CostCenterTypeConstraintViolations.IsCodeConflict(exception.ConstraintName))
        {
            // Two concurrent creates with the same code both pass CodeExistsAsync; the second trips
            // the (TenantId, NormalizedCode) unique index → the same clean 409 as the probe (CostCenters R2).
            await transaction.RollbackAsync(cancellationToken);
            return Result<CostCenterTypeResponse>.Failure(CostCenterErrors.CostCenterTypeCodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCostCenterTypeCommandHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCostCenterTypeCommand, CostCenterTypeResponse>
{
    public async Task<Result<CostCenterTypeResponse>> Handle(
        UpdateCostCenterTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CostCenterTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterTypeResponse>.Failure(authorizationResult.Error);
        }

        var costCenterType = await repository.GetByIdAsync(command.CostCenterTypeId, cancellationToken);
        if (costCenterType is null)
        {
            return Result<CostCenterTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CostCenterTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CostCenterErrors.CostCenterTypeNotFound);
        }

        if (costCenterType.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CostCenterTypeResponse>.Failure(CostCenterErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(costCenterType.TenantId, command.Code.Trim().ToUpperInvariant(), costCenterType.Id, cancellationToken))
        {
            return Result<CostCenterTypeResponse>.Failure(CostCenterErrors.CostCenterTypeCodeConflict);
        }

        var before = CostCenterTypeMapper.Map(costCenterType);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            costCenterType.Update(command.Code, command.Name, command.Description);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = CostCenterTypeMapper.Map(costCenterType);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CostCenterTypeUpdated,
                    AuditEntityTypes.CostCenterType,
                    costCenterType.PublicId,
                    costCenterType.Code,
                    AuditActions.Update,
                    $"Updated cost center type {costCenterType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CostCenterTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException exception)
            when (CostCenterTypeConstraintViolations.IsCodeConflict(exception.ConstraintName))
        {
            // A concurrent rename to the same code trips the (TenantId, NormalizedCode) unique index
            // after CodeExistsAsync passed → the same clean 409 as the probe (CostCenters R2).
            await transaction.RollbackAsync(cancellationToken);
            return Result<CostCenterTypeResponse>.Failure(CostCenterErrors.CostCenterTypeCodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateCostCenterTypeCommandHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateCostCenterTypeCommand, CostCenterTypeResponse>
{
    public async Task<Result<CostCenterTypeResponse>> Handle(
        ActivateCostCenterTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CostCenterTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterTypeResponse>.Failure(authorizationResult.Error);
        }

        var costCenterType = await repository.GetByIdAsync(command.CostCenterTypeId, cancellationToken);
        if (costCenterType is null)
        {
            return Result<CostCenterTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CostCenterTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CostCenterErrors.CostCenterTypeNotFound);
        }

        if (costCenterType.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CostCenterTypeResponse>.Failure(CostCenterErrors.ConcurrencyConflict);
        }

        var before = CostCenterTypeMapper.Map(costCenterType);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            costCenterType.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = CostCenterTypeMapper.Map(costCenterType);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CostCenterTypeActivated,
                    AuditEntityTypes.CostCenterType,
                    costCenterType.PublicId,
                    costCenterType.Code,
                    AuditActions.Reactivate,
                    $"Activated cost center type {costCenterType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CostCenterTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateCostCenterTypeCommandHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateCostCenterTypeCommand, CostCenterTypeResponse>
{
    public async Task<Result<CostCenterTypeResponse>> Handle(
        InactivateCostCenterTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CostCenterTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterTypeResponse>.Failure(authorizationResult.Error);
        }

        var costCenterType = await repository.GetByIdAsync(command.CostCenterTypeId, cancellationToken);
        if (costCenterType is null)
        {
            return Result<CostCenterTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CostCenterTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CostCenterErrors.CostCenterTypeNotFound);
        }

        if (costCenterType.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CostCenterTypeResponse>.Failure(CostCenterErrors.ConcurrencyConflict);
        }

        // Referential guard (mirror of the WorkCenterType in-use rule, probed inline via the
        // repository like the CostCenters usage checks): an ACTIVE cost center still referencing
        // the type blocks the soft-delete with a clean 409.
        if (await repository.HasActiveCostCentersAsync(costCenterType.Id, cancellationToken))
        {
            return Result<CostCenterTypeResponse>.Failure(CostCenterErrors.CostCenterTypeInUse);
        }

        var before = CostCenterTypeMapper.Map(costCenterType);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            costCenterType.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = CostCenterTypeMapper.Map(costCenterType);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CostCenterTypeInactivated,
                    AuditEntityTypes.CostCenterType,
                    costCenterType.PublicId,
                    costCenterType.Code,
                    AuditActions.Deactivate,
                    $"Inactivated cost center type {costCenterType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CostCenterTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchCostCenterTypeCommandHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchCostCenterTypeCommand, CostCenterTypeResponse>
{
    public async Task<Result<CostCenterTypeResponse>> Handle(
        PatchCostCenterTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CostCenterTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterTypeResponse>.Failure(authorizationResult.Error);
        }

        var costCenterType = await repository.GetByIdAsync(command.CostCenterTypeId, cancellationToken);
        if (costCenterType is null)
        {
            return Result<CostCenterTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CostCenterTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CostCenterErrors.CostCenterTypeNotFound);
        }

        if (costCenterType.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CostCenterTypeResponse>.Failure(CostCenterErrors.ConcurrencyConflict);
        }

        var before = CostCenterTypeMapper.Map(costCenterType);
        var state = CostCenterTypePatchState.From(before);

        var applied = CostCenterTypePatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<CostCenterTypeResponse>.Failure(applied.Error);
        }

        var validation = CostCenterTypePatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<CostCenterTypeResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<CostCenterTypeResponse>.Success(before);
        }

        if (await repository.CodeExistsAsync(costCenterType.TenantId, state.Code.Trim().ToUpperInvariant(), costCenterType.Id, cancellationToken))
        {
            return Result<CostCenterTypeResponse>.Failure(CostCenterErrors.CostCenterTypeCodeConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            costCenterType.Update(state.Code, state.Name, state.Description);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = CostCenterTypeMapper.Map(costCenterType);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CostCenterTypeUpdated,
                    AuditEntityTypes.CostCenterType,
                    costCenterType.PublicId,
                    costCenterType.Code,
                    AuditActions.Update,
                    $"Patched cost center type {costCenterType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CostCenterTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException exception)
            when (CostCenterTypeConstraintViolations.IsCodeConflict(exception.ConstraintName))
        {
            // A concurrent patch to the same code trips the (TenantId, NormalizedCode) unique index
            // after CodeExistsAsync passed → the same clean 409 as the probe (CostCenters R2).
            await transaction.RollbackAsync(cancellationToken);
            return Result<CostCenterTypeResponse>.Failure(CostCenterErrors.CostCenterTypeCodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class CostCenterTypeConstraintViolations
{
    // The (TenantId, NormalizedCode) unique index is the real guard against duplicate type codes;
    // the up-front CodeExistsAsync probe only closes the sequential case (mirrors CostCenters R2).
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, CostCenterValidationRules.CostCenterTypeCodeUniqueConstraintName, StringComparison.Ordinal);
}

internal sealed class CostCenterTypePatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool HasMutation { get; set; }

    public static CostCenterTypePatchState From(CostCenterTypeResponse response) =>
        new()
        {
            Code = response.Code,
            Name = response.Name,
            Description = response.Description
        };
}

internal sealed class CostCenterTypePatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class CostCenterTypePatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<CostCenterTypePatchOperation> operations, CostCenterTypePatchState state)
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
                return ValidationFailure(operation.Path, "Only root cost center type properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (CostCenterTypePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(CostCenterTypePatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.Code))
        {
            errors["code"] = ["Code is required."];
        }
        else if (state.Code.Length > 50)
        {
            errors["code"] = ["Code must be 50 characters or fewer."];
        }
        else if (!CostCenterValidationRules.IsValidCode(state.Code))
        {
            errors["code"] = ["Code format is invalid."];
        }

        if (string.IsNullOrWhiteSpace(state.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (state.Name.Length > 150)
        {
            errors["name"] = ["Name must be 150 characters or fewer."];
        }

        if (state.Description is { Length: > 500 })
        {
            errors["description"] = ["Description must be 500 characters or fewer."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        CostCenterTypePatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "concurrencyToken"))
        {
            return ValidationFailure(path, "The concurrency token cannot be patched; send the current token in the If-Match header.");
        }

        if (IsSegment(property, "isActive"))
        {
            return ValidationFailure(path, "Activation is not patchable; use the /activate and /inactivate endpoints.");
        }

        if (IsSegment(property, "code"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Code cannot be removed.");
            }

            state.Code = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "name"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Name cannot be removed.");
            }

            state.Name = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "description"))
        {
            state.Description = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
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

    private static string ReadRequiredString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new CostCenterTypePatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new CostCenterTypePatchValueException(path, "Value must be a string.");
    }

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new CostCenterTypePatchValueException(path, "Value must be a string or null.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

internal static class CostCenterTypeMapper
{
    public static CostCenterTypeResponse Map(CostCenterType costCenterType) =>
        new(
            costCenterType.PublicId,
            costCenterType.Code,
            costCenterType.Name,
            costCenterType.Description,
            costCenterType.IsActive,
            costCenterType.ConcurrencyToken,
            costCenterType.CreatedUtc,
            costCenterType.ModifiedUtc);
}
