using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
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
    AllowedActionsResponse? AllowedActions = null);

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

public sealed record GetLocationGroupTreeQuery(Guid CompanyId) : IQuery<IReadOnlyCollection<LocationGroupTreeNodeResponse>>;

public sealed record SearchLocationGroupsQuery(
    Guid CompanyId,
    int? LevelOrder,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = LocationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<LocationGroupResponse>>;

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
        RuleFor(query => query.Search).MaximumLength(150);
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
    ILocationDependencyPolicy dependencyPolicy,
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
            var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
            var enrichedItems = new List<LocationGroupResponse>(groups.Items.Count);

            foreach (var group in groups.Items)
            {
                var hasDependencies = group.IsActive &&
                    (await dependencyPolicy.CanInactivateLocationGroupAsync(group.Id, cancellationToken)).IsFailure;

                enrichedItems.Add(
                    LocationGroupPolicyAdapter.ApplyAllowedActions(
                        group,
                        resourceActionPolicyService,
                        canManage,
                        hasDependencies));
            }

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
        bool canManage,
        bool hasDependencies) =>
        response with
        {
            AllowedActions = resourceActionPolicyService.Evaluate(
                new ResourceActionContext(
                    ResourceKey: "LocationGroups",
                    State: response.IsActive ? "Active" : "Inactive",
                    IsActive: response.IsActive,
                    HasDependencies: hasDependencies,
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
                    "LOCATION_GROUP_CREATED",
                    "LocationGroup",
                    group.PublicId,
                    group.Code,
                    AuditActions.Create,
                    $"Created location group {group.Code}.",
                    After: response),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationGroupResponse>.Success(response);
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

        var before = await LocationGroupLookup.GetResponseAsync(groupRepository, group, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            group.Update(command.Code, command.Name, command.Description);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await LocationGroupLookup.GetResponseAsync(groupRepository, group, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    "LOCATION_GROUP_UPDATED",
                    "LocationGroup",
                    group.PublicId,
                    group.Code,
                    AuditActions.Update,
                    $"Updated location group {group.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

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

        var before = await LocationGroupLookup.GetResponseAsync(groupRepository, group, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            group.Move(parent?.Id);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await LocationGroupLookup.GetResponseAsync(groupRepository, group, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    "LOCATION_GROUP_MOVED",
                    "LocationGroup",
                    group.PublicId,
                    group.Code,
                    AuditActions.Update,
                    $"Moved location group {group.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

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

        var before = await LocationGroupLookup.GetResponseAsync(groupRepository, group, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            group.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await LocationGroupLookup.GetResponseAsync(groupRepository, group, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    "LOCATION_GROUP_ACTIVATED",
                    "LocationGroup",
                    group.PublicId,
                    group.Code,
                    AuditActions.Reactivate,
                    $"Activated location group {group.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

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

        var before = await LocationGroupLookup.GetResponseAsync(groupRepository, group, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            group.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await LocationGroupLookup.GetResponseAsync(groupRepository, group, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    "LOCATION_GROUP_INACTIVATED",
                    "LocationGroup",
                    group.PublicId,
                    group.Code,
                    AuditActions.Deactivate,
                    $"Inactivated location group {group.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

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

internal static class LocationGroupLookup
{
    public static async Task<LocationGroupResponse> GetResponseAsync(
        ILocationGroupRepository repository,
        LocationGroup group,
        CancellationToken cancellationToken)
    {
        var page = await repository.SearchAsync(group.TenantId, group.LevelOrder, isActive: null, group.Code, 1, 100, cancellationToken);
        return page.Items.Single(item => item.Id == group.PublicId);
    }
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
