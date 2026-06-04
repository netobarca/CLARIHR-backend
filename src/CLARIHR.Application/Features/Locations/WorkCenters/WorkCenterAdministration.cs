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

namespace CLARIHR.Application.Features.Locations.WorkCenters;

public sealed record WorkCenterResponse(
    Guid Id,
    string Code,
    string Name,
    Guid WorkCenterTypeId,
    string WorkCenterTypeCode,
    string WorkCenterTypeName,
    Guid LocationGroupId,
    string LocationGroupCode,
    string LocationGroupName,
    int LocationGroupLevelOrder,
    string? Address,
    decimal? GeoLat,
    decimal? GeoLong,
    string? Phone,
    string? Email,
    string? Notes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record SearchWorkCentersQuery(
    Guid CompanyId,
    Guid? GroupId,
    Guid? TypeId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = LocationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<WorkCenterResponse>>;

public sealed record GetWorkCenterByIdQuery(Guid WorkCenterId) : IQuery<WorkCenterResponse>;

public sealed record CreateWorkCenterCommand(
    Guid CompanyId,
    string Code,
    string Name,
    Guid WorkCenterTypeId,
    Guid LocationGroupId,
    string? Address,
    decimal? GeoLat,
    decimal? GeoLong,
    string? Phone,
    string? Email,
    string? Notes) : ICommand<WorkCenterResponse>;

public sealed record UpdateWorkCenterCommand(
    Guid WorkCenterId,
    string Code,
    string Name,
    Guid WorkCenterTypeId,
    Guid LocationGroupId,
    string? Address,
    decimal? GeoLat,
    decimal? GeoLong,
    string? Phone,
    string? Email,
    string? Notes,
    Guid ConcurrencyToken) : ICommand<WorkCenterResponse>;

public sealed record ReassignWorkCenterGroupCommand(
    Guid WorkCenterId,
    Guid LocationGroupId,
    Guid ConcurrencyToken) : ICommand<WorkCenterResponse>;

public sealed record ActivateWorkCenterCommand(Guid WorkCenterId, Guid ConcurrencyToken) : ICommand<WorkCenterResponse>;

public sealed record InactivateWorkCenterCommand(Guid WorkCenterId, Guid ConcurrencyToken) : ICommand<WorkCenterResponse>;

public sealed record WorkCenterPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchWorkCenterCommand(
    Guid WorkCenterId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<WorkCenterPatchOperation> Operations) : ICommand<WorkCenterResponse>;

internal sealed class SearchWorkCentersQueryValidator : AbstractValidator<SearchWorkCentersQuery>
{
    public SearchWorkCentersQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.GroupId).NotEqual(Guid.Empty).When(static query => query.GroupId.HasValue);
        RuleFor(query => query.TypeId).NotEqual(Guid.Empty).When(static query => query.TypeId.HasValue);
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, LocationValidationRules.MaxPageSize);
    }
}

internal sealed class GetWorkCenterByIdQueryValidator : AbstractValidator<GetWorkCenterByIdQuery>
{
    public GetWorkCenterByIdQueryValidator()
    {
        RuleFor(query => query.WorkCenterId).NotEmpty();
    }
}

internal sealed class CreateWorkCenterCommandValidator : AbstractValidator<CreateWorkCenterCommand>
{
    public CreateWorkCenterCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(LocationValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.WorkCenterTypeId).NotEmpty();
        RuleFor(command => command.LocationGroupId).NotEmpty();
        RuleFor(command => command.Address).MaximumLength(300);
        RuleFor(command => command.Phone).MaximumLength(50);
        RuleFor(command => command.Email).EmailAddress().When(static command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.GeoLat).InclusiveBetween(-90m, 90m).When(static command => command.GeoLat.HasValue);
        RuleFor(command => command.GeoLong).InclusiveBetween(-180m, 180m).When(static command => command.GeoLong.HasValue);
    }
}

internal sealed class UpdateWorkCenterCommandValidator : AbstractValidator<UpdateWorkCenterCommand>
{
    public UpdateWorkCenterCommandValidator()
    {
        RuleFor(command => command.WorkCenterId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(LocationValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.WorkCenterTypeId).NotEmpty();
        RuleFor(command => command.LocationGroupId).NotEmpty();
        RuleFor(command => command.Address).MaximumLength(300);
        RuleFor(command => command.Phone).MaximumLength(50);
        RuleFor(command => command.Email).EmailAddress().When(static command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.GeoLat).InclusiveBetween(-90m, 90m).When(static command => command.GeoLat.HasValue);
        RuleFor(command => command.GeoLong).InclusiveBetween(-180m, 180m).When(static command => command.GeoLong.HasValue);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ReassignWorkCenterGroupCommandValidator : AbstractValidator<ReassignWorkCenterGroupCommand>
{
    public ReassignWorkCenterGroupCommandValidator()
    {
        RuleFor(command => command.WorkCenterId).NotEmpty();
        RuleFor(command => command.LocationGroupId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateWorkCenterCommandValidator : AbstractValidator<ActivateWorkCenterCommand>
{
    public ActivateWorkCenterCommandValidator()
    {
        RuleFor(command => command.WorkCenterId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateWorkCenterCommandValidator : AbstractValidator<InactivateWorkCenterCommand>
{
    public InactivateWorkCenterCommandValidator()
    {
        RuleFor(command => command.WorkCenterId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchWorkCenterCommandValidator : AbstractValidator<PatchWorkCenterCommand>
{
    public PatchWorkCenterCommandValidator()
    {
        RuleFor(command => command.WorkCenterId).NotEmpty();
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

internal sealed class SearchWorkCentersQueryHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterRepository repository,
    ILocationDependencyPolicy dependencyPolicy,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchWorkCentersQuery, PagedResponse<WorkCenterResponse>>
{
    public async Task<Result<PagedResponse<WorkCenterResponse>>> Handle(
        SearchWorkCentersQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<WorkCenterResponse>>.Failure(authorizationResult.Error);
        }

        var result = await repository.SearchAsync(
            query.CompanyId,
            query.GroupId,
            query.TypeId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (query.IncludeAllowedActions)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
            var enrichedItems = new List<WorkCenterResponse>(result.Items.Count);

            foreach (var workCenter in result.Items)
            {
                var hasDependencies = workCenter.IsActive &&
                    (await dependencyPolicy.CanInactivateWorkCenterAsync(workCenter.Id, cancellationToken)).IsFailure;

                enrichedItems.Add(
                    WorkCenterPolicyAdapter.ApplyAllowedActions(
                        workCenter,
                        resourceActionPolicyService,
                        canManage,
                        hasDependencies));
            }

            result = new PagedResponse<WorkCenterResponse>(
                enrichedItems,
                result.PageNumber,
                result.PageSize,
                result.TotalCount);
        }

        return Result<PagedResponse<WorkCenterResponse>>.Success(result);
    }
}

internal static class WorkCenterPolicyAdapter
{
    public static WorkCenterResponse ApplyAllowedActions(
        WorkCenterResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage,
        bool hasDependencies) =>
        response with
        {
            AllowedActions = resourceActionPolicyService.Evaluate(
                new ResourceActionContext(
                    ResourceKey: "WorkCenters",
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

internal sealed class GetWorkCenterByIdQueryHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetWorkCenterByIdQuery, WorkCenterResponse>
{
    public async Task<Result<WorkCenterResponse>> Handle(
        GetWorkCenterByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkCenterResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(authorizationResult.Error);
        }

        var workCenter = await repository.GetResponseByIdAsync(query.WorkCenterId, cancellationToken);
        if (workCenter is not null)
        {
            return Result<WorkCenterResponse>.Success(workCenter);
        }

        return Result<WorkCenterResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.WorkCenterId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : LocationErrors.WorkCenterNotFound);
    }
}

internal sealed class CreateWorkCenterCommandHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterRepository repository,
    IWorkCenterTypeRepository workCenterTypeRepository,
    ILocationGroupRepository groupRepository,
    ILocationHierarchyRepository hierarchyRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateWorkCenterCommand, WorkCenterResponse>
{
    public async Task<Result<WorkCenterResponse>> Handle(
        CreateWorkCenterCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(command.CompanyId, command.Code.Trim().ToUpperInvariant(), excludingWorkCenterId: null, cancellationToken))
        {
            return Result<WorkCenterResponse>.Failure(LocationErrors.WorkCenterCodeConflict);
        }

        var typeResult = await WorkCenterRules.ResolveTypeAsync(
            workCenterTypeRepository,
            authorizationService,
            command.WorkCenterTypeId,
            RbacPermissionAction.Create,
            cancellationToken);
        if (typeResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(typeResult.Error);
        }

        var groupResult = await WorkCenterRules.ResolveGroupAsync(
            groupRepository,
            authorizationService,
            command.LocationGroupId,
            RbacPermissionAction.Create,
            cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(groupResult.Error);
        }

        var validationResult = await WorkCenterRules.ValidateAssignmentAsync(
            hierarchyRepository,
            command.CompanyId,
            typeResult.Value,
            groupResult.Value,
            command.Address,
            command.GeoLat,
            command.GeoLong,
            cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(validationResult.Error);
        }

        var workCenter = WorkCenter.Create(
            command.Code,
            command.Name,
            typeResult.Value.Id,
            groupResult.Value.Id,
            command.Address,
            command.GeoLat,
            command.GeoLong,
            command.Phone,
            command.Email,
            command.Notes);
        workCenter.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(workCenter);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(workCenter.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Work center response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    "WORK_CENTER_CREATED",
                    "WorkCenter",
                    workCenter.PublicId,
                    workCenter.Code,
                    AuditActions.Create,
                    $"Created work center {workCenter.Code}.",
                    After: response),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateWorkCenterCommandHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterRepository repository,
    IWorkCenterTypeRepository workCenterTypeRepository,
    ILocationGroupRepository groupRepository,
    ILocationHierarchyRepository hierarchyRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateWorkCenterCommand, WorkCenterResponse>
{
    public async Task<Result<WorkCenterResponse>> Handle(
        UpdateWorkCenterCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkCenterResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(authorizationResult.Error);
        }

        var workCenter = await repository.GetByIdAsync(command.WorkCenterId, cancellationToken);
        if (workCenter is null)
        {
            return Result<WorkCenterResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.WorkCenterId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.WorkCenterNotFound);
        }

        if (workCenter.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<WorkCenterResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(workCenter.TenantId, command.Code.Trim().ToUpperInvariant(), workCenter.Id, cancellationToken))
        {
            return Result<WorkCenterResponse>.Failure(LocationErrors.WorkCenterCodeConflict);
        }

        var typeResult = await WorkCenterRules.ResolveTypeAsync(
            workCenterTypeRepository,
            authorizationService,
            command.WorkCenterTypeId,
            RbacPermissionAction.Update,
            cancellationToken);
        if (typeResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(typeResult.Error);
        }

        var groupResult = await WorkCenterRules.ResolveGroupAsync(
            groupRepository,
            authorizationService,
            command.LocationGroupId,
            RbacPermissionAction.Update,
            cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(groupResult.Error);
        }

        var validationResult = await WorkCenterRules.ValidateAssignmentAsync(
            hierarchyRepository,
            workCenter.TenantId,
            typeResult.Value,
            groupResult.Value,
            command.Address,
            command.GeoLat,
            command.GeoLong,
            cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(validationResult.Error);
        }

        var before = await repository.GetResponseByIdAsync(workCenter.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workCenter.Update(
                command.Code,
                command.Name,
                typeResult.Value.Id,
                groupResult.Value.Id,
                command.Address,
                command.GeoLat,
                command.GeoLong,
                command.Phone,
                command.Email,
                command.Notes);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(workCenter.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Work center response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    "WORK_CENTER_UPDATED",
                    "WorkCenter",
                    workCenter.PublicId,
                    workCenter.Code,
                    AuditActions.Update,
                    $"Updated work center {workCenter.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ReassignWorkCenterGroupCommandHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterRepository repository,
    ILocationGroupRepository groupRepository,
    ILocationHierarchyRepository hierarchyRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReassignWorkCenterGroupCommand, WorkCenterResponse>
{
    public async Task<Result<WorkCenterResponse>> Handle(
        ReassignWorkCenterGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkCenterResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(authorizationResult.Error);
        }

        var workCenter = await repository.GetByIdAsync(command.WorkCenterId, cancellationToken);
        if (workCenter is null)
        {
            return Result<WorkCenterResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.WorkCenterId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.WorkCenterNotFound);
        }

        if (workCenter.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<WorkCenterResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        var groupResult = await WorkCenterRules.ResolveGroupAsync(
            groupRepository,
            authorizationService,
            command.LocationGroupId,
            RbacPermissionAction.Update,
            cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(groupResult.Error);
        }

        var validationResult = await WorkCenterRules.ValidateGroupAllowsWorkCentersAsync(
            hierarchyRepository,
            workCenter.TenantId,
            groupResult.Value,
            cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(validationResult.Error);
        }

        var before = await repository.GetResponseByIdAsync(workCenter.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workCenter.ReassignGroup(groupResult.Value.Id);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(workCenter.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Work center response could not be resolved after reassignment.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    "WORK_CENTER_REASSIGNED",
                    "WorkCenter",
                    workCenter.PublicId,
                    workCenter.Code,
                    AuditActions.Update,
                    $"Reassigned work center {workCenter.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateWorkCenterCommandHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateWorkCenterCommand, WorkCenterResponse>
{
    public async Task<Result<WorkCenterResponse>> Handle(
        ActivateWorkCenterCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkCenterResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(authorizationResult.Error);
        }

        var workCenter = await repository.GetByIdAsync(command.WorkCenterId, cancellationToken);
        if (workCenter is null)
        {
            return Result<WorkCenterResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.WorkCenterId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.WorkCenterNotFound);
        }

        if (workCenter.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<WorkCenterResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(workCenter.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workCenter.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(workCenter.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Work center response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    "WORK_CENTER_ACTIVATED",
                    "WorkCenter",
                    workCenter.PublicId,
                    workCenter.Code,
                    AuditActions.Reactivate,
                    $"Activated work center {workCenter.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateWorkCenterCommandHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterRepository repository,
    ILocationDependencyPolicy dependencyPolicy,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateWorkCenterCommand, WorkCenterResponse>
{
    public async Task<Result<WorkCenterResponse>> Handle(
        InactivateWorkCenterCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkCenterResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(authorizationResult.Error);
        }

        var workCenter = await repository.GetByIdAsync(command.WorkCenterId, cancellationToken);
        if (workCenter is null)
        {
            return Result<WorkCenterResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.WorkCenterId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.WorkCenterNotFound);
        }

        if (workCenter.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<WorkCenterResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        var dependencyResult = await dependencyPolicy.CanInactivateWorkCenterAsync(workCenter.PublicId, cancellationToken);
        if (dependencyResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(dependencyResult.Error);
        }

        var before = await repository.GetResponseByIdAsync(workCenter.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workCenter.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(workCenter.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Work center response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    "WORK_CENTER_INACTIVATED",
                    "WorkCenter",
                    workCenter.PublicId,
                    workCenter.Code,
                    AuditActions.Deactivate,
                    $"Inactivated work center {workCenter.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchWorkCenterCommandHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterRepository repository,
    IWorkCenterTypeRepository workCenterTypeRepository,
    ILocationGroupRepository groupRepository,
    ILocationHierarchyRepository hierarchyRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchWorkCenterCommand, WorkCenterResponse>
{
    public async Task<Result<WorkCenterResponse>> Handle(
        PatchWorkCenterCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkCenterResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(authorizationResult.Error);
        }

        var workCenter = await repository.GetByIdAsync(command.WorkCenterId, cancellationToken);
        if (workCenter is null)
        {
            return Result<WorkCenterResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.WorkCenterId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.WorkCenterNotFound);
        }

        if (workCenter.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<WorkCenterResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(workCenter.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Work center response could not be resolved before patch.");

        var state = WorkCenterPatchState.From(before);

        var applied = WorkCenterPatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(applied.Error);
        }

        var validation = WorkCenterPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<WorkCenterResponse>.Success(before);
        }

        if (await repository.CodeExistsAsync(workCenter.TenantId, state.Code.Trim().ToUpperInvariant(), workCenter.Id, cancellationToken))
        {
            return Result<WorkCenterResponse>.Failure(LocationErrors.WorkCenterCodeConflict);
        }

        // Type/group are NOT patchable here (use PUT / reassign-group). Re-resolve the CURRENT ones
        // so the type-dependent assignment rules (RequiresAddress / RequiresGeo / group-level) are
        // re-validated against the patched address/geo — identical semantics to Update.
        var typeResult = await WorkCenterRules.ResolveTypeAsync(
            workCenterTypeRepository,
            authorizationService,
            before.WorkCenterTypeId,
            RbacPermissionAction.Update,
            cancellationToken);
        if (typeResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(typeResult.Error);
        }

        var groupResult = await WorkCenterRules.ResolveGroupAsync(
            groupRepository,
            authorizationService,
            before.LocationGroupId,
            RbacPermissionAction.Update,
            cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(groupResult.Error);
        }

        var validationResult = await WorkCenterRules.ValidateAssignmentAsync(
            hierarchyRepository,
            workCenter.TenantId,
            typeResult.Value,
            groupResult.Value,
            state.Address,
            state.GeoLat,
            state.GeoLong,
            cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result<WorkCenterResponse>.Failure(validationResult.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workCenter.Update(
                state.Code,
                state.Name,
                typeResult.Value.Id,
                groupResult.Value.Id,
                state.Address,
                state.GeoLat,
                state.GeoLong,
                state.Phone,
                state.Email,
                state.Notes);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(workCenter.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Work center response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    "WORK_CENTER_UPDATED",
                    "WorkCenter",
                    workCenter.PublicId,
                    workCenter.Code,
                    AuditActions.Update,
                    $"Patched work center {workCenter.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class WorkCenterPatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public decimal? GeoLat { get; set; }
    public decimal? GeoLong { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool HasMutation { get; set; }

    public static WorkCenterPatchState From(WorkCenterResponse response) =>
        new()
        {
            Code = response.Code,
            Name = response.Name,
            Address = response.Address,
            GeoLat = response.GeoLat,
            GeoLong = response.GeoLong,
            Phone = response.Phone,
            Email = response.Email,
            Notes = response.Notes
        };
}

internal sealed class WorkCenterPatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class WorkCenterPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<WorkCenterPatchOperation> operations, WorkCenterPatchState state)
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
                return ValidationFailure(operation.Path, "Only root work center properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (WorkCenterPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(WorkCenterPatchState state)
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

        if (state.Address is { Length: > 300 })
        {
            errors["address"] = ["Address must be 300 characters or fewer."];
        }

        if (state.Phone is { Length: > 50 })
        {
            errors["phone"] = ["Phone must be 50 characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(state.Email) && !IsValidEmail(state.Email))
        {
            errors["email"] = ["Email format is invalid."];
        }

        if (state.Notes is { Length: > 1000 })
        {
            errors["notes"] = ["Notes must be 1000 characters or fewer."];
        }

        if (state.GeoLat is < -90m or > 90m)
        {
            errors["geoLat"] = ["Latitude must be between -90 and 90."];
        }

        if (state.GeoLong is < -180m or > 180m)
        {
            errors["geoLong"] = ["Longitude must be between -180 and 180."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        WorkCenterPatchState state,
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

        if (IsSegment(property, "workCenterTypeId") || IsSegment(property, "workCenterTypePublicId") ||
            IsSegment(property, "locationGroupId") || IsSegment(property, "locationGroupPublicId"))
        {
            return ValidationFailure(path, "The work center type and location group are not patchable; use PUT or the /reassign-group endpoint.");
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

        if (IsSegment(property, "address"))
        {
            state.Address = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "geoLat"))
        {
            state.GeoLat = isRemove ? null : ReadNullableDecimal(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "geoLong"))
        {
            state.GeoLong = isRemove ? null : ReadNullableDecimal(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "phone"))
        {
            state.Phone = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "email"))
        {
            state.Email = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "notes"))
        {
            state.Notes = isRemove ? null : ReadNullableString(value, path);
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

    private static bool IsValidEmail(string email)
    {
        var index = email.IndexOf('@', StringComparison.Ordinal);
        return index > 0 &&
            index < email.Length - 1 &&
            email.IndexOf('@', index + 1) < 0;
    }

    private static string ReadRequiredString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new WorkCenterPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new WorkCenterPatchValueException(path, "Value must be a string.");
    }

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new WorkCenterPatchValueException(path, "Value must be a string or null.");
    }

    private static decimal? ReadNullableDecimal(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDecimal(out var parsed)
            ? parsed
            : throw new WorkCenterPatchValueException(path, "Value must be a number or null.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

internal static class WorkCenterRules
{
    public static async Task<Result<WorkCenterType>> ResolveTypeAsync(
        IWorkCenterTypeRepository repository,
        ILocationAuthorizationService authorizationService,
        Guid typeId,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        var type = await repository.GetByIdAsync(typeId, cancellationToken);
        if (type is not null)
        {
            return Result<WorkCenterType>.Success(type);
        }

        return Result<WorkCenterType>.Failure(
            await repository.ExistsOutsideTenantAsync(typeId, cancellationToken)
                ? authorizationService.TenantMismatch(action)
                : LocationErrors.WorkCenterTypeNotFound);
    }

    public static async Task<Result<LocationGroup>> ResolveGroupAsync(
        ILocationGroupRepository repository,
        ILocationAuthorizationService authorizationService,
        Guid groupId,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        var group = await repository.GetByIdAsync(groupId, cancellationToken);
        if (group is not null)
        {
            return Result<LocationGroup>.Success(group);
        }

        return Result<LocationGroup>.Failure(
            await repository.ExistsOutsideTenantAsync(groupId, cancellationToken)
                ? authorizationService.TenantMismatch(action)
                : LocationErrors.GroupNotFound);
    }

    public static async Task<Result> ValidateAssignmentAsync(
        ILocationHierarchyRepository hierarchyRepository,
        Guid tenantId,
        WorkCenterType workCenterType,
        LocationGroup locationGroup,
        string? address,
        decimal? geoLat,
        decimal? geoLong,
        CancellationToken cancellationToken)
    {
        if (!workCenterType.IsActive)
        {
            return Result.Failure(LocationErrors.WorkCenterTypeInactive);
        }

        if (!locationGroup.IsActive)
        {
            return Result.Failure(LocationErrors.LocationGroupInactive);
        }

        var groupValidation = await ValidateGroupAllowsWorkCentersAsync(
            hierarchyRepository,
            tenantId,
            locationGroup,
            cancellationToken);
        if (groupValidation.IsFailure)
        {
            return groupValidation;
        }

        if (workCenterType.RequiresAddress && string.IsNullOrWhiteSpace(address))
        {
            return Result.Failure(new Error(
                "WORK_CENTER_ADDRESS_REQUIRED",
                "Address is required for the selected work center type.",
                ErrorType.Validation));
        }

        if (workCenterType.RequiresGeo && (!geoLat.HasValue || !geoLong.HasValue))
        {
            return Result.Failure(new Error(
                "WORK_CENTER_GEO_REQUIRED",
                "Latitude and longitude are required for the selected work center type.",
                ErrorType.Validation));
        }

        if ((geoLat.HasValue && (geoLat.Value < -90m || geoLat.Value > 90m)) ||
            (geoLong.HasValue && (geoLong.Value < -180m || geoLong.Value > 180m)))
        {
            return Result.Failure(LocationErrors.InvalidCoordinates);
        }

        return Result.Success();
    }

    public static async Task<Result> ValidateGroupAllowsWorkCentersAsync(
        ILocationHierarchyRepository hierarchyRepository,
        Guid tenantId,
        LocationGroup locationGroup,
        CancellationToken cancellationToken)
    {
        var levels = await hierarchyRepository.GetLevelsAsync(tenantId, cancellationToken);
        var level = levels.SingleOrDefault(level => level.LevelOrder == locationGroup.LevelOrder && level.IsActive);
        if (level is null || !level.AllowsWorkCenters)
        {
            return Result.Failure(LocationErrors.GroupLevelNotAllowedForWorkCenter);
        }

        return Result.Success();
    }
}
