using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Application.Abstractions.OrgUnits;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using CLARIHR.Domain.OrgUnits;
using FluentValidation;

namespace CLARIHR.Application.Features.OrgUnits;

public sealed record OrgUnitCatalogReferenceResponse(
    Guid Id,
    string Code,
    string Name);

public sealed record OrgUnitResponse(
    Guid Id,
    string Code,
    string Name,
    OrgUnitCatalogReferenceResponse OrgUnitType,
    OrgUnitCatalogReferenceResponse? FunctionalArea,
    OrgUnitCatalogReferenceResponse? Parent,
    int? SortOrder,
    string? Description,
    string? CostCenterCode,
    Guid? ManagerEmployeeId,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

// List projection of the paged search: same shape as OrgUnitResponse minus Description, which is
// detail-only payload (mirror of the CostCenters ListItem/Response split).
public sealed record OrgUnitListItemResponse(
    Guid Id,
    string Code,
    string Name,
    OrgUnitCatalogReferenceResponse OrgUnitType,
    OrgUnitCatalogReferenceResponse? FunctionalArea,
    OrgUnitCatalogReferenceResponse? Parent,
    int? SortOrder,
    string? CostCenterCode,
    Guid? ManagerEmployeeId,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record OrgUnitExportRow(
    Guid Id,
    string Code,
    string Name,
    Guid OrgUnitTypeId,
    string OrgUnitTypeCode,
    string OrgUnitTypeName,
    Guid? FunctionalAreaId,
    string? FunctionalAreaCode,
    string? FunctionalAreaName,
    string? ParentCode,
    string? ParentName,
    int? SortOrder,
    string? Description,
    string? CostCenterCode,
    Guid? ManagerEmployeeId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record OrgUnitTreeNodeResponse(
    Guid Id,
    string Code,
    string Name,
    OrgUnitCatalogReferenceResponse OrgUnitType,
    OrgUnitCatalogReferenceResponse? FunctionalArea,
    Guid? ParentId,
    int? SortOrder,
    bool IsActive,
    IReadOnlyCollection<OrgUnitTreeNodeResponse> Children);

public sealed record OrgUnitGraphNodeResponse(
    Guid Id,
    string Label,
    Guid OrgUnitTypeId,
    string OrgUnitTypeCode,
    string OrgUnitTypeName,
    bool IsActive);

public sealed record OrgUnitGraphEdgeResponse(Guid FromId, Guid ToId);

public sealed record OrgUnitGraphResponse(
    IReadOnlyCollection<OrgUnitGraphNodeResponse> Nodes,
    IReadOnlyCollection<OrgUnitGraphEdgeResponse> Edges);

public sealed record OrgUnitHierarchyNodeData(
    long InternalId,
    Guid Id,
    string Code,
    string Name,
    long OrgUnitTypeCatalogItemId,
    Guid OrgUnitTypeId,
    string OrgUnitTypeCode,
    string OrgUnitTypeName,
    long? FunctionalAreaCatalogItemId,
    Guid? FunctionalAreaId,
    string? FunctionalAreaCode,
    string? FunctionalAreaName,
    long? ParentInternalId,
    Guid? ParentId,
    int? SortOrder,
    string? Description,
    string? CostCenterCode,
    Guid? ManagerEmployeeId,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record SearchOrgUnitsQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    Guid? OrgUnitTypeId,
    Guid? FunctionalAreaId,
    Guid? ParentId,
    int PageNumber = 1,
    int PageSize = OrgUnitValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<OrgUnitListItemResponse>>;

public sealed record GetOrgUnitByIdQuery(Guid OrgUnitId) : IQuery<OrgUnitResponse>;

public sealed record GetOrgUnitExportRowsQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    Guid? OrgUnitTypeId,
    Guid? FunctionalAreaId,
    Guid? ParentId,
    int? MaxRows = null)
    : IQuery<IReadOnlyCollection<OrgUnitExportRow>>;

public sealed record CreateOrgUnitCommand(
    Guid CompanyId,
    string Code,
    string Name,
    Guid OrgUnitTypeId,
    Guid? FunctionalAreaId,
    Guid? ParentId,
    int? SortOrder,
    string? Description,
    string? CostCenterCode,
    Guid? ManagerEmployeeId) : ICommand<OrgUnitResponse>;

public sealed record UpdateOrgUnitCommand(
    Guid OrgUnitId,
    string Code,
    string Name,
    Guid OrgUnitTypeId,
    Guid? FunctionalAreaId,
    int? SortOrder,
    string? Description,
    string? CostCenterCode,
    Guid? ManagerEmployeeId,
    Guid ConcurrencyToken) : ICommand<OrgUnitResponse>;

public sealed record MoveOrgUnitCommand(
    Guid OrgUnitId,
    Guid? NewParentId,
    int? SortOrder,
    Guid ConcurrencyToken) : ICommand<OrgUnitResponse>;

public sealed record ActivateOrgUnitCommand(Guid OrgUnitId, Guid ConcurrencyToken) : ICommand<OrgUnitResponse>;

public sealed record InactivateOrgUnitCommand(Guid OrgUnitId, Guid ConcurrencyToken) : ICommand<OrgUnitResponse>;

public sealed record OrgUnitPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchOrgUnitCommand(
    Guid OrgUnitId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<OrgUnitPatchOperation> Operations) : ICommand<OrgUnitResponse>;

public sealed record GetOrgUnitTreeQuery(Guid CompanyId, Guid? RootId, int? Depth)
    : IQuery<IReadOnlyCollection<OrgUnitTreeNodeResponse>>;

public sealed record GetOrgUnitGraphQuery(Guid CompanyId, Guid? RootId, int? Depth)
    : IQuery<OrgUnitGraphResponse>;

internal sealed class SearchOrgUnitsQueryValidator : AbstractValidator<SearchOrgUnitsQuery>
{
    public SearchOrgUnitsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(OrgUnitValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {OrgUnitValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.OrgUnitTypeId)
            .NotEqual(Guid.Empty)
            .When(static query => query.OrgUnitTypeId.HasValue);
        RuleFor(query => query.FunctionalAreaId)
            .NotEqual(Guid.Empty)
            .When(static query => query.FunctionalAreaId.HasValue);
        RuleFor(query => query.ParentId).NotEqual(Guid.Empty).When(static query => query.ParentId.HasValue);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, OrgUnitValidationRules.MaxPageSize);
    }
}

internal sealed class GetOrgUnitByIdQueryValidator : AbstractValidator<GetOrgUnitByIdQuery>
{
    public GetOrgUnitByIdQueryValidator()
    {
        RuleFor(query => query.OrgUnitId).NotEmpty();
    }
}

internal sealed class CreateOrgUnitCommandValidator : AbstractValidator<CreateOrgUnitCommand>
{
    public CreateOrgUnitCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(OrgUnitValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.OrgUnitTypeId).NotEmpty();
        RuleFor(command => command.FunctionalAreaId)
            .NotEqual(Guid.Empty)
            .When(static command => command.FunctionalAreaId.HasValue);
        RuleFor(command => command.SortOrder)
            .GreaterThanOrEqualTo(0)
            .When(static command => command.SortOrder.HasValue);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.CostCenterCode).MaximumLength(100);
        RuleFor(command => command.ParentId)
            .NotEqual(Guid.Empty)
            .When(static command => command.ParentId.HasValue);
        RuleFor(command => command.ManagerEmployeeId)
            .NotEqual(Guid.Empty)
            .When(static command => command.ManagerEmployeeId.HasValue);
    }
}

internal sealed class UpdateOrgUnitCommandValidator : AbstractValidator<UpdateOrgUnitCommand>
{
    public UpdateOrgUnitCommandValidator()
    {
        RuleFor(command => command.OrgUnitId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(OrgUnitValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.OrgUnitTypeId).NotEmpty();
        RuleFor(command => command.FunctionalAreaId)
            .NotEqual(Guid.Empty)
            .When(static command => command.FunctionalAreaId.HasValue);
        RuleFor(command => command.SortOrder)
            .GreaterThanOrEqualTo(0)
            .When(static command => command.SortOrder.HasValue);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.CostCenterCode).MaximumLength(100);
        RuleFor(command => command.ManagerEmployeeId)
            .NotEqual(Guid.Empty)
            .When(static command => command.ManagerEmployeeId.HasValue);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class MoveOrgUnitCommandValidator : AbstractValidator<MoveOrgUnitCommand>
{
    public MoveOrgUnitCommandValidator()
    {
        RuleFor(command => command.OrgUnitId).NotEmpty();
        RuleFor(command => command.NewParentId)
            .NotEqual(Guid.Empty)
            .When(static command => command.NewParentId.HasValue);
        RuleFor(command => command.SortOrder)
            .GreaterThanOrEqualTo(0)
            .When(static command => command.SortOrder.HasValue);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateOrgUnitCommandValidator : AbstractValidator<ActivateOrgUnitCommand>
{
    public ActivateOrgUnitCommandValidator()
    {
        RuleFor(command => command.OrgUnitId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateOrgUnitCommandValidator : AbstractValidator<InactivateOrgUnitCommand>
{
    public InactivateOrgUnitCommandValidator()
    {
        RuleFor(command => command.OrgUnitId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchOrgUnitCommandValidator : AbstractValidator<PatchOrgUnitCommand>
{
    public PatchOrgUnitCommandValidator()
    {
        RuleFor(command => command.OrgUnitId).NotEmpty();
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

internal sealed class GetOrgUnitTreeQueryValidator : AbstractValidator<GetOrgUnitTreeQuery>
{
    public GetOrgUnitTreeQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.RootId)
            .NotEqual(Guid.Empty)
            .When(static query => query.RootId.HasValue);
        RuleFor(query => query.Depth)
            .InclusiveBetween(1, OrgUnitValidationRules.MaxDepth)
            .When(static query => query.Depth.HasValue);
    }
}

internal sealed class GetOrgUnitGraphQueryValidator : AbstractValidator<GetOrgUnitGraphQuery>
{
    public GetOrgUnitGraphQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.RootId)
            .NotEqual(Guid.Empty)
            .When(static query => query.RootId.HasValue);
        RuleFor(query => query.Depth)
            .InclusiveBetween(1, OrgUnitValidationRules.MaxDepth)
            .When(static query => query.Depth.HasValue);
    }
}

internal sealed class GetOrgUnitExportRowsQueryValidator : AbstractValidator<GetOrgUnitExportRowsQuery>
{
    public GetOrgUnitExportRowsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(OrgUnitValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {OrgUnitValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.OrgUnitTypeId)
            .NotEqual(Guid.Empty)
            .When(static query => query.OrgUnitTypeId.HasValue);
        RuleFor(query => query.FunctionalAreaId)
            .NotEqual(Guid.Empty)
            .When(static query => query.FunctionalAreaId.HasValue);
        RuleFor(query => query.ParentId)
            .NotEqual(Guid.Empty)
            .When(static query => query.ParentId.HasValue);
    }
}

internal sealed class SearchOrgUnitsQueryHandler(
    IOrgUnitAuthorizationService authorizationService,
    IOrgUnitRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchOrgUnitsQuery, PagedResponse<OrgUnitListItemResponse>>
{
    public async Task<Result<PagedResponse<OrgUnitListItemResponse>>> Handle(
        SearchOrgUnitsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<OrgUnitListItemResponse>>.Failure(authorizationResult.Error);
        }

        var result = await repository.SearchAsync(
            query.CompanyId,
            query.IsActive,
            query.OrgUnitTypeId,
            query.FunctionalAreaId,
            query.ParentId,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<OrgUnitListItemResponse>>.Success(result);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        // OU-007 (by design): the list reports hasActiveChildren=false for every item to avoid an N+1
        // child probe per row — so `canInactivate` here is an optimistic hint only. The authoritative
        // check runs server-side in the Inactivate handler (HasActiveChildrenAsync → 409 if it has active
        // children). GetById, which loads a single resource, resolves the real flag.
        var items = result.Items
            .Select(item => OrgUnitPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage, hasActiveChildren: false))
            .ToArray();

        result = result with { Items = items };
        return Result<PagedResponse<OrgUnitListItemResponse>>.Success(result);
    }
}

internal sealed class GetOrgUnitByIdQueryHandler(
    IOrgUnitAuthorizationService authorizationService,
    IOrgUnitRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetOrgUnitByIdQuery, OrgUnitResponse>
{
    public async Task<Result<OrgUnitResponse>> Handle(
        GetOrgUnitByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OrgUnitResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OrgUnitResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.OrgUnitId, cancellationToken);
        if (response is not null)
        {
            // OU-007: single child-flag query by public id (was GetByIdAsync + HasActiveChildrenAsync = 2 reads).
            var hasActiveChildren = await repository.HasActiveChildrenByPublicIdAsync(query.OrgUnitId, cancellationToken);
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;

            response = OrgUnitPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage, hasActiveChildren);
            return Result<OrgUnitResponse>.Success(response);
        }

        return Result<OrgUnitResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.OrgUnitId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : OrgUnitErrors.OrgUnitNotFound);
    }
}

internal sealed class GetOrgUnitTreeQueryHandler(
    IOrgUnitAuthorizationService authorizationService,
    IOrgUnitRepository repository)
    : IQueryHandler<GetOrgUnitTreeQuery, IReadOnlyCollection<OrgUnitTreeNodeResponse>>
{
    public async Task<Result<IReadOnlyCollection<OrgUnitTreeNodeResponse>>> Handle(
        GetOrgUnitTreeQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<OrgUnitTreeNodeResponse>>.Failure(authorizationResult.Error);
        }

        var hierarchy = await repository.GetHierarchyAsync(query.CompanyId, cancellationToken);
        if (query.RootId.HasValue && hierarchy.All(node => node.Id != query.RootId.Value))
        {
            return Result<IReadOnlyCollection<OrgUnitTreeNodeResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(query.RootId.Value, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : OrgUnitErrors.OrgUnitNotFound);
        }

        var tree = OrgUnitHierarchyBuilder.BuildTree(hierarchy, query.RootId, query.Depth);
        return Result<IReadOnlyCollection<OrgUnitTreeNodeResponse>>.Success(tree);
    }
}

internal sealed class GetOrgUnitGraphQueryHandler(
    IOrgUnitAuthorizationService authorizationService,
    IOrgUnitRepository repository)
    : IQueryHandler<GetOrgUnitGraphQuery, OrgUnitGraphResponse>
{
    public async Task<Result<OrgUnitGraphResponse>> Handle(
        GetOrgUnitGraphQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OrgUnitGraphResponse>.Failure(authorizationResult.Error);
        }

        var hierarchy = await repository.GetHierarchyAsync(query.CompanyId, cancellationToken);
        if (query.RootId.HasValue && hierarchy.All(node => node.Id != query.RootId.Value))
        {
            return Result<OrgUnitGraphResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(query.RootId.Value, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : OrgUnitErrors.OrgUnitNotFound);
        }

        var graph = OrgUnitHierarchyBuilder.BuildGraph(hierarchy, query.RootId, query.Depth);
        return Result<OrgUnitGraphResponse>.Success(graph);
    }
}

internal sealed class GetOrgUnitExportRowsQueryHandler(
    IOrgUnitAuthorizationService authorizationService,
    IOrgUnitRepository repository)
    : IQueryHandler<GetOrgUnitExportRowsQuery, IReadOnlyCollection<OrgUnitExportRow>>
{
    public async Task<Result<IReadOnlyCollection<OrgUnitExportRow>>> Handle(
        GetOrgUnitExportRowsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<OrgUnitExportRow>>.Failure(authorizationResult.Error);
        }

        var hierarchy = await repository.GetHierarchyAsync(query.CompanyId, cancellationToken);
        if (query.ParentId.HasValue && hierarchy.All(node => node.Id != query.ParentId.Value))
        {
            return Result<IReadOnlyCollection<OrgUnitExportRow>>.Failure(
                await repository.ExistsOutsideTenantAsync(query.ParentId.Value, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : OrgUnitErrors.OrgUnitNotFound);
        }

        var rows = await repository.GetExportRowsAsync(
            query.CompanyId,
            query.IsActive,
            query.Search,
            query.OrgUnitTypeId,
            query.FunctionalAreaId,
            query.ParentId,
            query.MaxRows,
            cancellationToken);

        return Result<IReadOnlyCollection<OrgUnitExportRow>>.Success(rows);
    }
}

internal sealed class CreateOrgUnitCommandHandler(
    IOrgUnitAuthorizationService authorizationService,
    IOrgUnitRepository repository,
    IOrgStructureCatalogRepository orgStructureCatalogRepository,
    ICostCenterRepository costCenterRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateOrgUnitCommand, OrgUnitResponse>
{
    public async Task<Result<OrgUnitResponse>> Handle(
        CreateOrgUnitCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OrgUnitResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(command.CompanyId, command.Code.Trim().ToUpperInvariant(), excludingOrgUnitId: null, cancellationToken))
        {
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.CodeConflict);
        }

        var orgUnitType = await orgStructureCatalogRepository.GetActiveOrgUnitTypeLookupAsync(
            command.CompanyId,
            command.OrgUnitTypeId,
            cancellationToken);
        if (orgUnitType is null)
        {
            return Result<OrgUnitResponse>.Failure(
                await orgStructureCatalogRepository.ExistsOrgUnitTypeOutsideTenantAsync(command.OrgUnitTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Create)
                    : OrgStructureCatalogErrors.OrgUnitTypeNotFound);
        }

        CatalogReferenceLookup? functionalArea = null;
        if (command.FunctionalAreaId.HasValue)
        {
            functionalArea = await orgStructureCatalogRepository.GetActiveFunctionalAreaLookupAsync(
                command.CompanyId,
                command.FunctionalAreaId.Value,
                cancellationToken);
            if (functionalArea is null)
            {
                return Result<OrgUnitResponse>.Failure(
                    await orgStructureCatalogRepository.ExistsFunctionalAreaOutsideTenantAsync(command.FunctionalAreaId.Value, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Create)
                        : OrgStructureCatalogErrors.FunctionalAreaNotFound);
            }
        }

        if (!string.IsNullOrWhiteSpace(command.CostCenterCode) &&
            !await costCenterRepository.ExistsActiveByCodeAsync(
                command.CompanyId,
                command.CostCenterCode.Trim().ToUpperInvariant(),
                cancellationToken))
        {
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.CostCenterInvalid);
        }

        OrgUnit? parent = null;
        if (command.ParentId.HasValue)
        {
            parent = await repository.GetByIdAsync(command.ParentId.Value, cancellationToken);
            if (parent is null)
            {
                return Result<OrgUnitResponse>.Failure(
                    await repository.ExistsOutsideTenantAsync(command.ParentId.Value, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Create)
                        : OrgUnitErrors.ParentNotFound);
            }
        }

        if (parent is not null)
        {
            var hierarchy = await repository.GetHierarchyAsync(command.CompanyId, cancellationToken);
            var byInternalId = hierarchy.ToDictionary(static node => node.InternalId);
            var depth = OrgUnitHierarchyBuilder.CalculateDepth(parent.Id, byInternalId);
            if (depth > OrgUnitValidationRules.MaxDepth)
            {
                return Result<OrgUnitResponse>.Failure(OrgUnitErrors.DepthLimitExceeded);
            }
        }

        var orgUnit = OrgUnit.Create(
            command.Code,
            command.Name,
            orgUnitType.InternalId,
            functionalArea?.InternalId,
            parent?.Id,
            command.SortOrder,
            command.Description,
            command.CostCenterCode,
            command.ManagerEmployeeId);
        orgUnit.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(orgUnit);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(orgUnit.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Org unit response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OrgUnitCreated,
                    AuditEntityTypes.OrgUnit,
                    orgUnit.PublicId,
                    orgUnit.Code,
                    AuditActions.Create,
                    $"Created organization unit {orgUnit.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OrgUnitResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (OrgUnitConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateOrgUnitCommandHandler(
    IOrgUnitAuthorizationService authorizationService,
    IOrgUnitRepository repository,
    IOrgStructureCatalogRepository orgStructureCatalogRepository,
    ICostCenterRepository costCenterRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateOrgUnitCommand, OrgUnitResponse>
{
    public async Task<Result<OrgUnitResponse>> Handle(
        UpdateOrgUnitCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OrgUnitResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OrgUnitResponse>.Failure(authorizationResult.Error);
        }

        var orgUnit = await repository.GetByIdAsync(command.OrgUnitId, cancellationToken);
        if (orgUnit is null)
        {
            return Result<OrgUnitResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.OrgUnitId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgUnitErrors.OrgUnitNotFound);
        }

        if (orgUnit.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(orgUnit.TenantId, command.Code.Trim().ToUpperInvariant(), orgUnit.Id, cancellationToken))
        {
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.CodeConflict);
        }

        var orgUnitType = await orgStructureCatalogRepository.GetActiveOrgUnitTypeLookupAsync(
            orgUnit.TenantId,
            command.OrgUnitTypeId,
            cancellationToken);
        if (orgUnitType is null)
        {
            return Result<OrgUnitResponse>.Failure(
                await orgStructureCatalogRepository.ExistsOrgUnitTypeOutsideTenantAsync(command.OrgUnitTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgStructureCatalogErrors.OrgUnitTypeNotFound);
        }

        CatalogReferenceLookup? functionalArea = null;
        if (command.FunctionalAreaId.HasValue)
        {
            functionalArea = await orgStructureCatalogRepository.GetActiveFunctionalAreaLookupAsync(
                orgUnit.TenantId,
                command.FunctionalAreaId.Value,
                cancellationToken);
            if (functionalArea is null)
            {
                return Result<OrgUnitResponse>.Failure(
                    await orgStructureCatalogRepository.ExistsFunctionalAreaOutsideTenantAsync(command.FunctionalAreaId.Value, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : OrgStructureCatalogErrors.FunctionalAreaNotFound);
            }
        }

        if (!string.IsNullOrWhiteSpace(command.CostCenterCode) &&
            !await costCenterRepository.ExistsActiveByCodeAsync(
                orgUnit.TenantId,
                command.CostCenterCode.Trim().ToUpperInvariant(),
                cancellationToken))
        {
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.CostCenterInvalid);
        }

        var before = await repository.GetResponseByIdAsync(orgUnit.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Org unit response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            orgUnit.Update(
                command.Code,
                command.Name,
                orgUnitType.InternalId,
                functionalArea?.InternalId,
                command.SortOrder,
                command.Description,
                command.CostCenterCode,
                command.ManagerEmployeeId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(orgUnit.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Org unit response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OrgUnitUpdated,
                    AuditEntityTypes.OrgUnit,
                    orgUnit.PublicId,
                    orgUnit.Code,
                    AuditActions.Update,
                    $"Updated organization unit {orgUnit.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OrgUnitResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (OrgUnitConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class MoveOrgUnitCommandHandler(
    IOrgUnitAuthorizationService authorizationService,
    IOrgUnitRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<MoveOrgUnitCommand, OrgUnitResponse>
{
    public async Task<Result<OrgUnitResponse>> Handle(
        MoveOrgUnitCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OrgUnitResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OrgUnitResponse>.Failure(authorizationResult.Error);
        }

        var orgUnit = await repository.GetByIdAsync(command.OrgUnitId, cancellationToken);
        if (orgUnit is null)
        {
            return Result<OrgUnitResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.OrgUnitId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgUnitErrors.OrgUnitNotFound);
        }

        if (orgUnit.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.ConcurrencyConflict);
        }

        var hierarchy = await repository.GetHierarchyAsync(orgUnit.TenantId, cancellationToken);
        var byInternalId = hierarchy.ToDictionary(static node => node.InternalId);

        OrgUnit? parent = null;
        if (command.NewParentId.HasValue)
        {
            if (command.NewParentId.Value == orgUnit.PublicId)
            {
                return Result<OrgUnitResponse>.Failure(OrgUnitErrors.CycleDetected);
            }

            parent = await repository.GetByIdAsync(command.NewParentId.Value, cancellationToken);
            if (parent is null)
            {
                return Result<OrgUnitResponse>.Failure(
                    await repository.ExistsOutsideTenantAsync(command.NewParentId.Value, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : OrgUnitErrors.ParentNotFound);
            }

            if (OrgUnitHierarchyBuilder.WouldCreateCycle(orgUnit.Id, parent.Id, byInternalId))
            {
                return Result<OrgUnitResponse>.Failure(OrgUnitErrors.CycleDetected);
            }

            var newDepth = OrgUnitHierarchyBuilder.CalculateDepth(parent.Id, byInternalId);
            if (newDepth > OrgUnitValidationRules.MaxDepth)
            {
                return Result<OrgUnitResponse>.Failure(OrgUnitErrors.DepthLimitExceeded);
            }
        }

        var before = await repository.GetResponseByIdAsync(orgUnit.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Org unit response could not be resolved before move.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            orgUnit.Move(parent?.Id, command.SortOrder ?? orgUnit.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(orgUnit.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Org unit response could not be resolved after move.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OrgUnitMoved,
                    AuditEntityTypes.OrgUnit,
                    orgUnit.PublicId,
                    orgUnit.Code,
                    AuditActions.Update,
                    $"Moved organization unit {orgUnit.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OrgUnitResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateOrgUnitCommandHandler(
    IOrgUnitAuthorizationService authorizationService,
    IOrgUnitRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateOrgUnitCommand, OrgUnitResponse>
{
    public async Task<Result<OrgUnitResponse>> Handle(
        ActivateOrgUnitCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OrgUnitResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OrgUnitResponse>.Failure(authorizationResult.Error);
        }

        var orgUnit = await repository.GetByIdAsync(command.OrgUnitId, cancellationToken);
        if (orgUnit is null)
        {
            return Result<OrgUnitResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.OrgUnitId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgUnitErrors.OrgUnitNotFound);
        }

        if (orgUnit.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(orgUnit.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Org unit response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            orgUnit.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(orgUnit.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Org unit response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OrgUnitActivated,
                    AuditEntityTypes.OrgUnit,
                    orgUnit.PublicId,
                    orgUnit.Code,
                    AuditActions.Reactivate,
                    $"Activated organization unit {orgUnit.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OrgUnitResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateOrgUnitCommandHandler(
    IOrgUnitAuthorizationService authorizationService,
    IOrgUnitRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateOrgUnitCommand, OrgUnitResponse>
{
    public async Task<Result<OrgUnitResponse>> Handle(
        InactivateOrgUnitCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OrgUnitResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OrgUnitResponse>.Failure(authorizationResult.Error);
        }

        var orgUnit = await repository.GetByIdAsync(command.OrgUnitId, cancellationToken);
        if (orgUnit is null)
        {
            return Result<OrgUnitResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.OrgUnitId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgUnitErrors.OrgUnitNotFound);
        }

        if (orgUnit.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.ConcurrencyConflict);
        }

        if (await repository.HasActiveChildrenAsync(orgUnit.Id, cancellationToken))
        {
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.HasActiveChildren);
        }

        var before = await repository.GetResponseByIdAsync(orgUnit.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Org unit response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            orgUnit.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(orgUnit.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Org unit response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OrgUnitInactivated,
                    AuditEntityTypes.OrgUnit,
                    orgUnit.PublicId,
                    orgUnit.Code,
                    AuditActions.Deactivate,
                    $"Inactivated organization unit {orgUnit.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OrgUnitResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchOrgUnitCommandHandler(
    IOrgUnitAuthorizationService authorizationService,
    IOrgUnitRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchOrgUnitCommand, OrgUnitResponse>
{
    public async Task<Result<OrgUnitResponse>> Handle(
        PatchOrgUnitCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OrgUnitResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OrgUnitResponse>.Failure(authorizationResult.Error);
        }

        var orgUnit = await repository.GetByIdAsync(command.OrgUnitId, cancellationToken);
        if (orgUnit is null)
        {
            return Result<OrgUnitResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.OrgUnitId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgUnitErrors.OrgUnitNotFound);
        }

        if (orgUnit.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OrgUnitResponse>.Failure(OrgUnitErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(orgUnit.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Org unit response could not be resolved before patch.");

        var state = OrgUnitPatchState.From(before);

        var applied = OrgUnitPatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<OrgUnitResponse>.Failure(applied.Error);
        }

        var validation = OrgUnitPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<OrgUnitResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<OrgUnitResponse>.Success(before);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Descriptive-only patch: code (uniqueness), the type/functional-area/manager FKs and the
            // cost-center code (resolved/validated) are kept at their current — already valid —
            // values, and the parent moves via /move. So the patch re-runs no code-conflict, no FK
            // resolution and no cost-center validation. Only name/sortOrder/description change.
            orgUnit.Update(
                orgUnit.Code,
                state.Name,
                orgUnit.OrgUnitTypeCatalogItemId,
                orgUnit.FunctionalAreaCatalogItemId,
                state.SortOrder,
                state.Description,
                orgUnit.CostCenterCode,
                orgUnit.ManagerEmployeeId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(orgUnit.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Org unit response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OrgUnitUpdated,
                    AuditEntityTypes.OrgUnit,
                    orgUnit.PublicId,
                    orgUnit.Code,
                    AuditActions.Update,
                    $"Patched organization unit {orgUnit.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OrgUnitResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class OrgUnitPatchState
{
    public string Name { get; set; } = string.Empty;
    public int? SortOrder { get; set; }
    public string? Description { get; set; }
    public bool HasMutation { get; set; }

    public static OrgUnitPatchState From(OrgUnitResponse response) =>
        new()
        {
            Name = response.Name,
            SortOrder = response.SortOrder,
            Description = response.Description
        };
}

internal sealed class OrgUnitPatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class OrgUnitPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<OrgUnitPatchOperation> operations, OrgUnitPatchState state)
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
                return ValidationFailure(operation.Path, "Only root org unit properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (OrgUnitPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(OrgUnitPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (state.Name.Length > 150)
        {
            errors["name"] = ["Name must be 150 characters or fewer."];
        }

        if (state.SortOrder is < 0)
        {
            errors["sortOrder"] = ["Sort order must be greater than or equal to zero."];
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
        OrgUnitPatchState state,
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

        if (IsSegment(property, "parentId") || IsSegment(property, "parentPublicId") || IsSegment(property, "parent"))
        {
            return ValidationFailure(path, "The parent is not patchable; use the /move endpoint.");
        }

        if (IsSegment(property, "code"))
        {
            return ValidationFailure(path, "The code is not patchable here; use PUT (it is uniqueness-checked).");
        }

        if (IsSegment(property, "orgUnitTypePublicId") || IsSegment(property, "orgUnitType") ||
            IsSegment(property, "functionalAreaPublicId") || IsSegment(property, "functionalArea") ||
            IsSegment(property, "managerEmployeePublicId") || IsSegment(property, "managerEmployeeId") ||
            IsSegment(property, "costCenterCode"))
        {
            return ValidationFailure(path, "The type, functional area, manager and cost center are not patchable here; use PUT (they are resolved/validated).");
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

        if (IsSegment(property, "sortOrder"))
        {
            state.SortOrder = isRemove ? null : ReadNullableInt(value, path);
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
            throw new OrgUnitPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new OrgUnitPatchValueException(path, "Value must be a string.");
    }

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new OrgUnitPatchValueException(path, "Value must be a string or null.");
    }

    private static int? ReadNullableInt(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var parsed)
            ? parsed
            : throw new OrgUnitPatchValueException(path, "Value must be an integer or null.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

internal static class OrgUnitPolicyAdapter
{
    public static OrgUnitResponse ApplyAllowedActions(
        OrgUnitResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage,
        bool hasActiveChildren) =>
        response with
        {
            AllowedActions = Evaluate(
                resourceActionPolicyService,
                response.OrgUnitType.Code,
                response.IsActive,
                canManage,
                hasActiveChildren)
        };

    public static OrgUnitListItemResponse ApplyAllowedActions(
        OrgUnitListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage,
        bool hasActiveChildren) =>
        response with
        {
            AllowedActions = Evaluate(
                resourceActionPolicyService,
                response.OrgUnitType.Code,
                response.IsActive,
                canManage,
                hasActiveChildren)
        };

    private static AllowedActionsResponse Evaluate(
        IResourceActionPolicyService resourceActionPolicyService,
        string orgUnitTypeCode,
        bool isActive,
        bool canManage,
        bool hasActiveChildren) =>
        resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                OrgUnitPermissionCodes.ResourceKey,
                orgUnitTypeCode,
                isActive,
                HasDependencies: hasActiveChildren,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));
}

internal static class OrgUnitHierarchyBuilder
{
    public static IReadOnlyCollection<OrgUnitTreeNodeResponse> BuildTree(
        IReadOnlyList<OrgUnitHierarchyNodeData> nodes,
        Guid? rootId,
        int? depth)
    {
        var selected = SelectNodes(nodes, rootId, depth);
        var byParent = selected
            .GroupBy(static node => node.ParentId ?? Guid.Empty)
            .ToDictionary(static group => group.Key, static group => Order(group).ToArray());

        if (rootId.HasValue)
        {
            var root = selected.Single(node => node.Id == rootId.Value);
            return [BuildNode(root, byParent, depth, level: 1)];
        }

        return BuildChildren(parentId: null, byParent, depth, level: 1);
    }

    public static OrgUnitGraphResponse BuildGraph(
        IReadOnlyList<OrgUnitHierarchyNodeData> nodes,
        Guid? rootId,
        int? depth)
    {
        var selected = SelectNodes(nodes, rootId, depth);
        var selectedIds = selected.Select(static node => node.Id).ToHashSet();

        var graphNodes = Order(selected)
            .Select(node => new OrgUnitGraphNodeResponse(
                node.Id,
                node.Name,
                node.OrgUnitTypeId,
                node.OrgUnitTypeCode,
                node.OrgUnitTypeName,
                node.IsActive))
            .ToArray();

        var edges = selected
            .Where(node => node.ParentId.HasValue && selectedIds.Contains(node.ParentId.Value))
            .OrderBy(node => node.ParentId)
            .ThenBy(node => node.SortOrder ?? int.MaxValue)
            .ThenBy(node => node.Name)
            .Select(node => new OrgUnitGraphEdgeResponse(node.ParentId!.Value, node.Id))
            .ToArray();

        return new OrgUnitGraphResponse(graphNodes, edges);
    }

    public static bool WouldCreateCycle(
        long sourceInternalId,
        long? candidateParentInternalId,
        IReadOnlyDictionary<long, OrgUnitHierarchyNodeData> byInternalId)
    {
        var cursor = candidateParentInternalId;
        while (cursor.HasValue)
        {
            if (cursor.Value == sourceInternalId)
            {
                return true;
            }

            if (!byInternalId.TryGetValue(cursor.Value, out var parent))
            {
                break;
            }

            cursor = parent.ParentInternalId;
        }

        return false;
    }

    public static int CalculateDepth(long? parentInternalId, IReadOnlyDictionary<long, OrgUnitHierarchyNodeData> byInternalId)
    {
        var depth = 1;
        var visited = new HashSet<long>();
        var cursor = parentInternalId;

        while (cursor.HasValue)
        {
            if (!visited.Add(cursor.Value))
            {
                return OrgUnitValidationRules.MaxDepth + 1;
            }

            depth++;
            if (depth > OrgUnitValidationRules.MaxDepth)
            {
                return depth;
            }

            if (!byInternalId.TryGetValue(cursor.Value, out var parent))
            {
                break;
            }

            cursor = parent.ParentInternalId;
        }

        return depth;
    }

    private static IReadOnlyList<OrgUnitHierarchyNodeData> SelectNodes(
        IReadOnlyList<OrgUnitHierarchyNodeData> nodes,
        Guid? rootId,
        int? depth)
    {
        const long RootKey = long.MinValue;

        if (!depth.HasValue && !rootId.HasValue)
        {
            return nodes;
        }

        var byParentInternal = nodes
            .GroupBy(static node => node.ParentInternalId ?? RootKey)
            .ToDictionary(static group => group.Key, static group => Order(group).ToArray());

        var selected = new List<OrgUnitHierarchyNodeData>();
        var queue = new Queue<(OrgUnitHierarchyNodeData Node, int Level)>();

        if (rootId.HasValue)
        {
            var root = nodes.Single(node => node.Id == rootId.Value);
            queue.Enqueue((root, 1));
        }
        else
        {
            if (!byParentInternal.TryGetValue(RootKey, out var roots))
            {
                return [];
            }

            foreach (var root in roots)
            {
                queue.Enqueue((root, 1));
            }
        }

        while (queue.Count > 0)
        {
            var (node, level) = queue.Dequeue();
            selected.Add(node);

            if (depth.HasValue && level >= depth.Value)
            {
                continue;
            }

            if (!byParentInternal.TryGetValue(node.InternalId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                queue.Enqueue((child, level + 1));
            }
        }

        return selected;
    }

    private static IReadOnlyCollection<OrgUnitTreeNodeResponse> BuildChildren(
        Guid? parentId,
        IReadOnlyDictionary<Guid, OrgUnitHierarchyNodeData[]> byParent,
        int? depth,
        int level)
    {
        var key = parentId ?? Guid.Empty;
        if (!byParent.TryGetValue(key, out var children))
        {
            return [];
        }

        return children
            .Select(child => BuildNode(child, byParent, depth, level))
            .ToArray();
    }

    private static OrgUnitTreeNodeResponse BuildNode(
        OrgUnitHierarchyNodeData node,
        IReadOnlyDictionary<Guid, OrgUnitHierarchyNodeData[]> byParent,
        int? depth,
        int level)
    {
        var children = depth.HasValue && level >= depth.Value
            ? []
            : BuildChildren(node.Id, byParent, depth, level + 1);

        return new OrgUnitTreeNodeResponse(
            node.Id,
            node.Code,
            node.Name,
            new OrgUnitCatalogReferenceResponse(node.OrgUnitTypeId, node.OrgUnitTypeCode, node.OrgUnitTypeName),
            node.FunctionalAreaId.HasValue && !string.IsNullOrWhiteSpace(node.FunctionalAreaCode) && !string.IsNullOrWhiteSpace(node.FunctionalAreaName)
                ? new OrgUnitCatalogReferenceResponse(node.FunctionalAreaId.Value, node.FunctionalAreaCode!, node.FunctionalAreaName!)
                : null,
            node.ParentId,
            node.SortOrder,
            node.IsActive,
            children);
    }

    private static IOrderedEnumerable<OrgUnitHierarchyNodeData> Order(IEnumerable<OrgUnitHierarchyNodeData> nodes) =>
        nodes
            .OrderBy(static node => node.SortOrder ?? int.MaxValue)
            .ThenBy(static node => node.Name)
            .ThenBy(static node => node.Code);
}

internal static class OrgUnitConstraintViolations
{
    // OU-004: the (TenantId, NormalizedCode) unique index is the real guard against duplicate codes; the
    // up-front CodeExistsAsync probe only closes the common (sequential) case. On a concurrent
    // create/update of the same code, the second writer trips this index — map it to the same clean 409
    // as the probe instead of letting the 23505 escape as an HTTP 500 (mirrors CostCenters R2).
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, OrgUnitValidationRules.CodeUniqueConstraintName, StringComparison.Ordinal);
}
