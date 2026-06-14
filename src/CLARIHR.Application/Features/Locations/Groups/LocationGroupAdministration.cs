using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Domain.Locations;
using FluentValidation;

namespace CLARIHR.Application.Features.Locations.Groups;

public sealed record LocationGroupResponse(
    Guid Id,
    int LevelOrder,
    string Code,
    string Name,
    Guid? ParentId,
    string? Description,
    bool IsActive,
    bool IsDefault,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record LocationGroupTreeNodeResponse(
    Guid Id,
    int LevelOrder,
    string Code,
    string Name,
    Guid? ParentId,
    string? Description,
    bool IsActive,
    bool IsDefault,
    Guid ConcurrencyToken,
    IReadOnlyCollection<LocationGroupTreeNodeResponse> Children);

public sealed record LocationGroupTreeNodeData(
    Guid Id,
    int LevelOrder,
    string Code,
    string Name,
    Guid? ParentId,
    string? Description,
    bool IsActive,
    bool IsDefault,
    Guid ConcurrencyToken);

public sealed record LocationGroupPathNodeResponse(
    Guid Id,
    int LevelOrder,
    string Code,
    string Name);

public sealed record LocationGroupUsageResponse(
    Guid Id,
    string Code,
    string Name,
    int ActiveChildGroupCount,
    int InactiveChildGroupCount,
    int ActiveWorkCenterCount,
    int InactiveWorkCenterCount,
    bool IsDefault,
    bool CanInactivate);

public sealed record GetLocationGroupTreeQuery(Guid CompanyId) : IQuery<IReadOnlyCollection<LocationGroupTreeNodeResponse>>;

public sealed record SearchLocationGroupsQuery(
    Guid CompanyId,
    int? LevelOrder,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = LocationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<LocationGroupResponse>>;

public sealed record GetLocationGroupByIdQuery(Guid GroupId) : IQuery<LocationGroupResponse>;

public sealed record GetLocationGroupChildrenQuery(Guid GroupId, bool? IsActive) : IQuery<IReadOnlyCollection<LocationGroupResponse>>;

public sealed record GetLocationGroupPathQuery(Guid GroupId) : IQuery<IReadOnlyCollection<LocationGroupPathNodeResponse>>;

public sealed record GetLocationGroupUsageQuery(Guid GroupId) : IQuery<LocationGroupUsageResponse>;

public sealed record CreateLocationGroupCommand(
    Guid CompanyId,
    int LevelOrder,
    string Code,
    string Name,
    Guid? ParentId,
    string? Description) : ICommand<LocationGroupResponse>;

public sealed record UpdateLocationGroupCommand(
    Guid GroupId,
    string Code,
    string Name,
    string? Description,
    Guid ConcurrencyToken) : ICommand<LocationGroupResponse>;

public sealed record MoveLocationGroupCommand(
    Guid GroupId,
    Guid? ParentId,
    Guid ConcurrencyToken) : ICommand<LocationGroupResponse>;

public sealed record ActivateLocationGroupCommand(Guid GroupId, Guid ConcurrencyToken) : ICommand<LocationGroupResponse>;

public sealed record InactivateLocationGroupCommand(Guid GroupId, Guid ConcurrencyToken) : ICommand<LocationGroupResponse>;

public sealed record LocationGroupPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchLocationGroupCommand(
    Guid GroupId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<LocationGroupPatchOperation> Operations) : ICommand<LocationGroupResponse>;

internal sealed class GetLocationGroupTreeQueryValidator : AbstractValidator<GetLocationGroupTreeQuery>
{
    public GetLocationGroupTreeQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class SearchLocationGroupsQueryValidator : AbstractValidator<SearchLocationGroupsQuery>
{
    public SearchLocationGroupsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.LevelOrder).GreaterThan(0).When(static query => query.LevelOrder.HasValue);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, LocationValidationRules.MaxPageSize);
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(LocationValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {LocationValidationRules.MinSearchLength} characters when provided.");
    }
}

internal sealed class GetLocationGroupByIdQueryValidator : AbstractValidator<GetLocationGroupByIdQuery>
{
    public GetLocationGroupByIdQueryValidator()
    {
        RuleFor(query => query.GroupId).NotEmpty();
    }
}

internal sealed class CreateLocationGroupCommandValidator : AbstractValidator<CreateLocationGroupCommand>
{
    public CreateLocationGroupCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.LevelOrder).GreaterThan(0);
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(LocationValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.ParentId)
            .NotEmpty()
            .When(static command => command.LevelOrder > 1);
    }
}

internal sealed class UpdateLocationGroupCommandValidator : AbstractValidator<UpdateLocationGroupCommand>
{
    public UpdateLocationGroupCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(LocationValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class MoveLocationGroupCommandValidator : AbstractValidator<MoveLocationGroupCommand>
{
    public MoveLocationGroupCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateLocationGroupCommandValidator : AbstractValidator<ActivateLocationGroupCommand>
{
    public ActivateLocationGroupCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateLocationGroupCommandValidator : AbstractValidator<InactivateLocationGroupCommand>
{
    public InactivateLocationGroupCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchLocationGroupCommandValidator : AbstractValidator<PatchLocationGroupCommand>
{
    public PatchLocationGroupCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
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

internal sealed class GetLocationGroupTreeQueryHandler(
    ILocationAuthorizationService authorizationService,
    ILocationGroupRepository repository)
    : IQueryHandler<GetLocationGroupTreeQuery, IReadOnlyCollection<LocationGroupTreeNodeResponse>>
{
    public async Task<Result<IReadOnlyCollection<LocationGroupTreeNodeResponse>>> Handle(
        GetLocationGroupTreeQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<LocationGroupTreeNodeResponse>>.Failure(authorizationResult.Error);
        }

        var treeData = await repository.GetTreeAsync(query.CompanyId, cancellationToken);
        var tree = LocationGroupTreeBuilder.Build(treeData);
        return Result<IReadOnlyCollection<LocationGroupTreeNodeResponse>>.Success(tree);
    }
}

internal sealed class SearchLocationGroupsQueryHandler(
    ILocationAuthorizationService authorizationService,
    ILocationGroupRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchLocationGroupsQuery, PagedResponse<LocationGroupResponse>>
{
    public async Task<Result<PagedResponse<LocationGroupResponse>>> Handle(
        SearchLocationGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<LocationGroupResponse>>.Failure(authorizationResult.Error);
        }

        var groups = await repository.SearchAsync(
            query.CompanyId,
            query.LevelOrder,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (query.IncludeAllowedActions)
        {
            // §12.7 (ADR-0001): list AllowedActions derive ONLY from the caller's permission (canManage),
            // never from per-item dependency state — resolving dependencies per row is the forbidden N+1.
            // The real inactivation block (active children / work centers / protected default group) is
            // enforced server-side in InactivateLocationGroupCommandHandler, independent of this list flag.
            var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
            var enrichedItems = groups.Items
                .Select(group => LocationGroupPolicyAdapter.ApplyAllowedActions(group, resourceActionPolicyService, canManage))
                .ToArray();

            groups = new PagedResponse<LocationGroupResponse>(
                enrichedItems,
                groups.PageNumber,
                groups.PageSize,
                groups.TotalCount);
        }

        return Result<PagedResponse<LocationGroupResponse>>.Success(groups);
    }
}

internal static class LocationGroupPolicyAdapter
{
    public static LocationGroupResponse ApplyAllowedActions(
        LocationGroupResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with
        {
            AllowedActions = resourceActionPolicyService.Evaluate(
                new ResourceActionContext(
                    ResourceKey: "LocationGroups",
                    State: response.IsActive ? "Active" : "Inactive",
                    IsActive: response.IsActive,
                    SupportsEdit: true,
                    EditAllowed: canManage,
                    SupportsActivate: true,
                    ActivateAllowed: canManage,
                    SupportsInactivate: true,
                    InactivateAllowed: canManage))
        };
}

internal sealed class CreateLocationGroupCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationHierarchyRepository hierarchyRepository,
    ILocationGroupRepository groupRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateLocationGroupCommand, LocationGroupResponse>
{
    public async Task<Result<LocationGroupResponse>> Handle(
        CreateLocationGroupCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationGroupResponse>.Failure(authorizationResult.Error);
        }

        var levels = await hierarchyRepository.GetLevelsAsync(command.CompanyId, cancellationToken);
        var level = levels.SingleOrDefault(level => level.LevelOrder == command.LevelOrder && level.IsActive);
        if (level is null)
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.LevelNotFound);
        }

        if (await groupRepository.CodeExistsAsync(command.CompanyId, command.Code.Trim().ToUpperInvariant(), excludingGroupId: null, cancellationToken))
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.GroupCodeConflict);
        }

        LocationGroup? parent = null;
        if (command.LevelOrder == 1)
        {
            if (command.ParentId.HasValue)
            {
                return Result<LocationGroupResponse>.Failure(LocationErrors.GroupInvalidParent);
            }
        }
        else
        {
            if (!command.ParentId.HasValue)
            {
                return Result<LocationGroupResponse>.Failure(LocationErrors.GroupParentRequired);
            }

            parent = await groupRepository.GetByIdAsync(command.ParentId.Value, cancellationToken);
            if (parent is null)
            {
                return Result<LocationGroupResponse>.Failure(
                    await groupRepository.ExistsOutsideTenantAsync(command.ParentId.Value, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : LocationErrors.GroupInvalidParent);
            }

            if (!parent.IsActive)
            {
                return Result<LocationGroupResponse>.Failure(LocationErrors.ParentGroupInactive);
            }

            if (parent.LevelOrder != command.LevelOrder - 1)
            {
                return Result<LocationGroupResponse>.Failure(LocationErrors.GroupInvalidParent);
            }
        }

        var group = LocationGroup.Create(
            command.LevelOrder,
            command.Code,
            command.Name,
            parent?.Id,
            command.Description);
        group.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            groupRepository.Add(group);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = new LocationGroupResponse(
                group.PublicId,
                group.LevelOrder,
                group.Code,
                group.Name,
                parent?.PublicId,
                group.Description,
                group.IsActive,
                group.IsDefault,
                group.ConcurrencyToken,
                group.CreatedUtc,
                group.ModifiedUtc);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LocationGroupCreated,
                    AuditEntityTypes.LocationGroup,
                    group.PublicId,
                    group.Code,
                    AuditActions.Create,
                    $"Created location group {group.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationGroupResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException exception)
            when (LocationConstraintViolations.IsGroupCodeConflict(exception.ConstraintName))
        {
            // WCT-A (family alignment): two concurrent creates with the same code both pass CodeExistsAsync;
            // the second trips uq_location_groups__tenant_code → the same clean 409 as the probe (CostCenters R2).
            await transaction.RollbackAsync(cancellationToken);
            return Result<LocationGroupResponse>.Failure(LocationErrors.GroupCodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateLocationGroupCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationGroupRepository groupRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateLocationGroupCommand, LocationGroupResponse>
{
    public async Task<Result<LocationGroupResponse>> Handle(
        UpdateLocationGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LocationGroupResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationGroupResponse>.Failure(authorizationResult.Error);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result<LocationGroupResponse>.Failure(
                await groupRepository.ExistsOutsideTenantAsync(command.GroupId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.GroupNotFound);
        }

        if (group.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        if (group.IsDefault &&
            (!group.Code.Equals(command.Code.Trim(), StringComparison.OrdinalIgnoreCase) ||
             !group.Name.Equals(command.Name.Trim(), StringComparison.Ordinal)))
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.DefaultGroupProtected);
        }

        if (await groupRepository.CodeExistsAsync(group.TenantId, command.Code.Trim().ToUpperInvariant(), group.Id, cancellationToken))
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.GroupCodeConflict);
        }

        var before = await groupRepository.GetResponseByIdAsync(group.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Location group response could not be resolved.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            group.Update(command.Code, command.Name, command.Description);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await groupRepository.GetResponseByIdAsync(group.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Location group response could not be resolved.");
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LocationGroupUpdated,
                    AuditEntityTypes.LocationGroup,
                    group.PublicId,
                    group.Code,
                    AuditActions.Update,
                    $"Updated location group {group.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationGroupResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException exception)
            when (LocationConstraintViolations.IsGroupCodeConflict(exception.ConstraintName))
        {
            // WCT-A (family alignment): a concurrent rename to the same code trips
            // uq_location_groups__tenant_code after CodeExistsAsync passed → 409 (CostCenters R2).
            await transaction.RollbackAsync(cancellationToken);
            return Result<LocationGroupResponse>.Failure(LocationErrors.GroupCodeConflict);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<LocationGroupResponse>.Failure(LocationErrors.DefaultGroupProtected);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class MoveLocationGroupCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationGroupRepository groupRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<MoveLocationGroupCommand, LocationGroupResponse>
{
    public async Task<Result<LocationGroupResponse>> Handle(
        MoveLocationGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LocationGroupResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationGroupResponse>.Failure(authorizationResult.Error);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result<LocationGroupResponse>.Failure(
                await groupRepository.ExistsOutsideTenantAsync(command.GroupId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.GroupNotFound);
        }

        if (group.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        LocationGroup? parent = null;
        if (group.LevelOrder == 1)
        {
            if (command.ParentId.HasValue)
            {
                return Result<LocationGroupResponse>.Failure(LocationErrors.GroupInvalidParent);
            }
        }
        else
        {
            if (!command.ParentId.HasValue)
            {
                return Result<LocationGroupResponse>.Failure(LocationErrors.GroupParentRequired);
            }

            parent = await groupRepository.GetByIdAsync(command.ParentId.Value, cancellationToken);
            if (parent is null)
            {
                return Result<LocationGroupResponse>.Failure(
                    await groupRepository.ExistsOutsideTenantAsync(command.ParentId.Value, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : LocationErrors.GroupInvalidParent);
            }

            if (!parent.IsActive)
            {
                return Result<LocationGroupResponse>.Failure(LocationErrors.ParentGroupInactive);
            }

            if (parent.LevelOrder != group.LevelOrder - 1)
            {
                return Result<LocationGroupResponse>.Failure(LocationErrors.GroupInvalidParent);
            }

            if (await groupRepository.IsDescendantAsync(group.Id, parent.Id, cancellationToken))
            {
                return Result<LocationGroupResponse>.Failure(LocationErrors.GroupCycleDetected);
            }
        }

        var before = await groupRepository.GetResponseByIdAsync(group.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Location group response could not be resolved.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            group.Move(parent?.Id);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await groupRepository.GetResponseByIdAsync(group.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Location group response could not be resolved.");
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LocationGroupMoved,
                    AuditEntityTypes.LocationGroup,
                    group.PublicId,
                    group.Code,
                    AuditActions.Update,
                    $"Moved location group {group.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationGroupResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateLocationGroupCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationGroupRepository groupRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateLocationGroupCommand, LocationGroupResponse>
{
    public async Task<Result<LocationGroupResponse>> Handle(
        ActivateLocationGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LocationGroupResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationGroupResponse>.Failure(authorizationResult.Error);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result<LocationGroupResponse>.Failure(
                await groupRepository.ExistsOutsideTenantAsync(command.GroupId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.GroupNotFound);
        }

        if (group.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        var before = await groupRepository.GetResponseByIdAsync(group.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Location group response could not be resolved.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            group.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await groupRepository.GetResponseByIdAsync(group.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Location group response could not be resolved.");
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LocationGroupActivated,
                    AuditEntityTypes.LocationGroup,
                    group.PublicId,
                    group.Code,
                    AuditActions.Reactivate,
                    $"Activated location group {group.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationGroupResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateLocationGroupCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationGroupRepository groupRepository,
    ILocationDependencyPolicy dependencyPolicy,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateLocationGroupCommand, LocationGroupResponse>
{
    public async Task<Result<LocationGroupResponse>> Handle(
        InactivateLocationGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LocationGroupResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationGroupResponse>.Failure(authorizationResult.Error);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result<LocationGroupResponse>.Failure(
                await groupRepository.ExistsOutsideTenantAsync(command.GroupId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.GroupNotFound);
        }

        if (group.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        if (group.IsDefault)
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.DefaultGroupProtected);
        }

        var dependencyResult = await dependencyPolicy.CanInactivateLocationGroupAsync(group.PublicId, cancellationToken);
        if (dependencyResult.IsFailure)
        {
            return Result<LocationGroupResponse>.Failure(dependencyResult.Error);
        }

        var before = await groupRepository.GetResponseByIdAsync(group.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Location group response could not be resolved.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            group.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await groupRepository.GetResponseByIdAsync(group.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Location group response could not be resolved.");
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LocationGroupInactivated,
                    AuditEntityTypes.LocationGroup,
                    group.PublicId,
                    group.Code,
                    AuditActions.Deactivate,
                    $"Inactivated location group {group.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationGroupResponse>.Success(after);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<LocationGroupResponse>.Failure(LocationErrors.DefaultGroupProtected);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetLocationGroupByIdQueryHandler(
    ILocationAuthorizationService authorizationService,
    ILocationGroupRepository groupRepository,
    ITenantContext tenantContext)
    : IQueryHandler<GetLocationGroupByIdQuery, LocationGroupResponse>
{
    public async Task<Result<LocationGroupResponse>> Handle(
        GetLocationGroupByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LocationGroupResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationGroupResponse>.Failure(authorizationResult.Error);
        }

        var group = await groupRepository.GetByIdAsync(query.GroupId, cancellationToken);
        if (group is not null)
        {
            return Result<LocationGroupResponse>.Success(
                await groupRepository.GetResponseByIdAsync(group.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Location group response could not be resolved."));
        }

        return Result<LocationGroupResponse>.Failure(
            await groupRepository.ExistsOutsideTenantAsync(query.GroupId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : LocationErrors.GroupNotFound);
    }
}

internal sealed class GetLocationGroupChildrenQueryHandler(
    ILocationAuthorizationService authorizationService,
    ILocationGroupRepository groupRepository,
    ITenantContext tenantContext)
    : IQueryHandler<GetLocationGroupChildrenQuery, IReadOnlyCollection<LocationGroupResponse>>
{
    public async Task<Result<IReadOnlyCollection<LocationGroupResponse>>> Handle(
        GetLocationGroupChildrenQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IReadOnlyCollection<LocationGroupResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<LocationGroupResponse>>.Failure(authorizationResult.Error);
        }

        var group = await groupRepository.GetByIdAsync(query.GroupId, cancellationToken);
        if (group is null)
        {
            return Result<IReadOnlyCollection<LocationGroupResponse>>.Failure(
                await groupRepository.ExistsOutsideTenantAsync(query.GroupId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : LocationErrors.GroupNotFound);
        }

        var children = await groupRepository.GetChildrenAsync(query.GroupId, query.IsActive, cancellationToken);
        return Result<IReadOnlyCollection<LocationGroupResponse>>.Success(children);
    }
}

internal sealed class GetLocationGroupPathQueryHandler(
    ILocationAuthorizationService authorizationService,
    ILocationGroupRepository groupRepository,
    ITenantContext tenantContext)
    : IQueryHandler<GetLocationGroupPathQuery, IReadOnlyCollection<LocationGroupPathNodeResponse>>
{
    public async Task<Result<IReadOnlyCollection<LocationGroupPathNodeResponse>>> Handle(
        GetLocationGroupPathQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IReadOnlyCollection<LocationGroupPathNodeResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<LocationGroupPathNodeResponse>>.Failure(authorizationResult.Error);
        }

        var group = await groupRepository.GetByIdAsync(query.GroupId, cancellationToken);
        if (group is null)
        {
            return Result<IReadOnlyCollection<LocationGroupPathNodeResponse>>.Failure(
                await groupRepository.ExistsOutsideTenantAsync(query.GroupId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : LocationErrors.GroupNotFound);
        }

        var path = await groupRepository.GetAncestorPathAsync(query.GroupId, cancellationToken);
        return Result<IReadOnlyCollection<LocationGroupPathNodeResponse>>.Success(path);
    }
}

internal sealed class GetLocationGroupUsageQueryHandler(
    ILocationAuthorizationService authorizationService,
    ILocationGroupRepository groupRepository,
    ITenantContext tenantContext)
    : IQueryHandler<GetLocationGroupUsageQuery, LocationGroupUsageResponse>
{
    public async Task<Result<LocationGroupUsageResponse>> Handle(
        GetLocationGroupUsageQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LocationGroupUsageResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationGroupUsageResponse>.Failure(authorizationResult.Error);
        }

        var usage = await groupRepository.GetUsageByIdAsync(query.GroupId, cancellationToken);
        if (usage is not null)
        {
            return Result<LocationGroupUsageResponse>.Success(usage);
        }

        return Result<LocationGroupUsageResponse>.Failure(
            await groupRepository.ExistsOutsideTenantAsync(query.GroupId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : LocationErrors.GroupNotFound);
    }
}

internal sealed class PatchLocationGroupCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationGroupRepository groupRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchLocationGroupCommand, LocationGroupResponse>
{
    public async Task<Result<LocationGroupResponse>> Handle(
        PatchLocationGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LocationGroupResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationGroupResponse>.Failure(authorizationResult.Error);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result<LocationGroupResponse>.Failure(
                await groupRepository.ExistsOutsideTenantAsync(command.GroupId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.GroupNotFound);
        }

        if (group.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        var before = await groupRepository.GetResponseByIdAsync(group.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Location group response could not be resolved.");
        var state = LocationGroupPatchState.From(before);

        var applied = LocationGroupPatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<LocationGroupResponse>.Failure(applied.Error);
        }

        var validation = LocationGroupPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<LocationGroupResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<LocationGroupResponse>.Success(before);
        }

        // Mirror Update: the default group's identity (code/name) is protected; its description
        // may still be patched. The entity also enforces this (throws), caught below as a backstop.
        if (group.IsDefault &&
            (!group.Code.Equals(state.Code.Trim(), StringComparison.OrdinalIgnoreCase) ||
             !group.Name.Equals(state.Name.Trim(), StringComparison.Ordinal)))
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.DefaultGroupProtected);
        }

        if (await groupRepository.CodeExistsAsync(group.TenantId, state.Code.Trim().ToUpperInvariant(), group.Id, cancellationToken))
        {
            return Result<LocationGroupResponse>.Failure(LocationErrors.GroupCodeConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            group.Update(state.Code, state.Name, state.Description);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await groupRepository.GetResponseByIdAsync(group.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Location group response could not be resolved.");
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LocationGroupUpdated,
                    AuditEntityTypes.LocationGroup,
                    group.PublicId,
                    group.Code,
                    AuditActions.Update,
                    $"Patched location group {group.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationGroupResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException exception)
            when (LocationConstraintViolations.IsGroupCodeConflict(exception.ConstraintName))
        {
            // WCT-A (family alignment): a concurrent patch to the same code trips
            // uq_location_groups__tenant_code after CodeExistsAsync passed → 409 (CostCenters R2).
            await transaction.RollbackAsync(cancellationToken);
            return Result<LocationGroupResponse>.Failure(LocationErrors.GroupCodeConflict);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<LocationGroupResponse>.Failure(LocationErrors.DefaultGroupProtected);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class LocationGroupPatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool HasMutation { get; set; }

    public static LocationGroupPatchState From(LocationGroupResponse response) =>
        new()
        {
            Code = response.Code,
            Name = response.Name,
            Description = response.Description
        };
}

internal sealed class LocationGroupPatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class LocationGroupPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<LocationGroupPatchOperation> operations, LocationGroupPatchState state)
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
                return ValidationFailure(operation.Path, "Only root location group properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (LocationGroupPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(LocationGroupPatchState state)
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
        else if (!LocationValidationRules.IsValidCode(state.Code))
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
        LocationGroupPatchState state,
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

        if (IsSegment(property, "levelOrder"))
        {
            return ValidationFailure(path, "The level order is immutable and cannot be patched.");
        }

        if (IsSegment(property, "parentId") || IsSegment(property, "parentPublicId"))
        {
            return ValidationFailure(path, "The parent is not patchable; use the /move endpoint.");
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
            throw new LocationGroupPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new LocationGroupPatchValueException(path, "Value must be a string.");
    }

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new LocationGroupPatchValueException(path, "Value must be a string or null.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

internal static class LocationGroupTreeBuilder
{
    public static IReadOnlyCollection<LocationGroupTreeNodeResponse> Build(IReadOnlyList<LocationGroupTreeNodeData> items)
    {
        var byParent = items
            .OrderBy(item => item.LevelOrder)
            .ThenBy(item => item.Name)
            .GroupBy(static item => item.ParentId ?? Guid.Empty)
            .ToDictionary(static group => group.Key, static group => group.ToArray());

        return BuildChildren(parentId: null, byParent);
    }

    private static IReadOnlyCollection<LocationGroupTreeNodeResponse> BuildChildren(
        Guid? parentId,
        IReadOnlyDictionary<Guid, LocationGroupTreeNodeData[]> byParent)
    {
        var key = parentId ?? Guid.Empty;
        if (!byParent.TryGetValue(key, out var children))
        {
            return [];
        }

        return children
            .Select(child => new LocationGroupTreeNodeResponse(
                child.Id,
                child.LevelOrder,
                child.Code,
                child.Name,
                child.ParentId,
                child.Description,
                child.IsActive,
                child.IsDefault,
                child.ConcurrencyToken,
                BuildChildren(child.Id, byParent)))
            .ToArray();
    }
}
