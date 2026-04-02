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

namespace CLARIHR.Application.Features.Locations.WorkCenterTypes;

public sealed record WorkCenterTypeResponse(
    Guid Id,
    string Code,
    string Name,
    bool RequiresAddress,
    bool RequiresGeo,
    bool AllowsBiometric,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record GetWorkCenterTypesQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = LocationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<WorkCenterTypeResponse>>;

public sealed record CreateWorkCenterTypeCommand(
    Guid CompanyId,
    string Code,
    string Name,
    bool RequiresAddress,
    bool RequiresGeo,
    bool AllowsBiometric) : ICommand<WorkCenterTypeResponse>;

public sealed record UpdateWorkCenterTypeCommand(
    Guid WorkCenterTypeId,
    string Code,
    string Name,
    bool RequiresAddress,
    bool RequiresGeo,
    bool AllowsBiometric,
    Guid ConcurrencyToken) : ICommand<WorkCenterTypeResponse>;

public sealed record ActivateWorkCenterTypeCommand(Guid WorkCenterTypeId, Guid ConcurrencyToken) : ICommand<WorkCenterTypeResponse>;

public sealed record InactivateWorkCenterTypeCommand(Guid WorkCenterTypeId, Guid ConcurrencyToken) : ICommand<WorkCenterTypeResponse>;

internal sealed class GetWorkCenterTypesQueryValidator : AbstractValidator<GetWorkCenterTypesQuery>
{
    public GetWorkCenterTypesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, LocationValidationRules.MaxPageSize);
    }
}

internal sealed class CreateWorkCenterTypeCommandValidator : AbstractValidator<CreateWorkCenterTypeCommand>
{
    public CreateWorkCenterTypeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(LocationValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
    }
}

internal sealed class UpdateWorkCenterTypeCommandValidator : AbstractValidator<UpdateWorkCenterTypeCommand>
{
    public UpdateWorkCenterTypeCommandValidator()
    {
        RuleFor(command => command.WorkCenterTypeId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(LocationValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateWorkCenterTypeCommandValidator : AbstractValidator<ActivateWorkCenterTypeCommand>
{
    public ActivateWorkCenterTypeCommandValidator()
    {
        RuleFor(command => command.WorkCenterTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateWorkCenterTypeCommandValidator : AbstractValidator<InactivateWorkCenterTypeCommand>
{
    public InactivateWorkCenterTypeCommandValidator()
    {
        RuleFor(command => command.WorkCenterTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetWorkCenterTypesQueryHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterTypeRepository repository,
    ILocationDependencyPolicy dependencyPolicy,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetWorkCenterTypesQuery, PagedResponse<WorkCenterTypeResponse>>
{
    public async Task<Result<PagedResponse<WorkCenterTypeResponse>>> Handle(
        GetWorkCenterTypesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<WorkCenterTypeResponse>>.Failure(authorizationResult.Error);
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
            var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
            var enrichedItems = new List<WorkCenterTypeResponse>(response.Items.Count);

            foreach (var workCenterType in response.Items)
            {
                var hasDependencies = workCenterType.IsActive &&
                    (await dependencyPolicy.CanInactivateWorkCenterTypeAsync(workCenterType.Id, cancellationToken)).IsFailure;

                enrichedItems.Add(
                    WorkCenterTypePolicyAdapter.ApplyAllowedActions(
                        workCenterType,
                        resourceActionPolicyService,
                        canManage,
                        hasDependencies));
            }

            response = new PagedResponse<WorkCenterTypeResponse>(
                enrichedItems,
                response.PageNumber,
                response.PageSize,
                response.TotalCount);
        }

        return Result<PagedResponse<WorkCenterTypeResponse>>.Success(response);
    }
}

internal static class WorkCenterTypePolicyAdapter
{
    public static WorkCenterTypeResponse ApplyAllowedActions(
        WorkCenterTypeResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage,
        bool hasDependencies) =>
        response with
        {
            AllowedActions = resourceActionPolicyService.Evaluate(
                new ResourceActionContext(
                    ResourceKey: "WorkCenterTypes",
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

internal sealed class CreateWorkCenterTypeCommandHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterTypeRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateWorkCenterTypeCommand, WorkCenterTypeResponse>
{
    public async Task<Result<WorkCenterTypeResponse>> Handle(
        CreateWorkCenterTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterTypeResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(command.CompanyId, command.Code.Trim().ToUpperInvariant(), excludingWorkCenterTypeId: null, cancellationToken))
        {
            return Result<WorkCenterTypeResponse>.Failure(LocationErrors.WorkCenterTypeCodeConflict);
        }

        var workCenterType = WorkCenterType.Create(
            command.Code,
            command.Name,
            command.RequiresAddress,
            command.RequiresGeo,
            command.AllowsBiometric);
        workCenterType.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(workCenterType);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = WorkCenterTypeMapper.Map(workCenterType);
            await auditService.LogAsync(
                new AuditLogEntry(
                    "WORK_CENTER_TYPE_CREATED",
                    "WorkCenterType",
                    workCenterType.PublicId,
                    workCenterType.Code,
                    AuditActions.Create,
                    $"Created work center type {workCenterType.Code}.",
                    After: response),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterTypeResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateWorkCenterTypeCommandHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateWorkCenterTypeCommand, WorkCenterTypeResponse>
{
    public async Task<Result<WorkCenterTypeResponse>> Handle(
        UpdateWorkCenterTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkCenterTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterTypeResponse>.Failure(authorizationResult.Error);
        }

        var workCenterType = await repository.GetByIdAsync(command.WorkCenterTypeId, cancellationToken);
        if (workCenterType is null)
        {
            return Result<WorkCenterTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.WorkCenterTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.WorkCenterTypeNotFound);
        }

        if (workCenterType.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<WorkCenterTypeResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(workCenterType.TenantId, command.Code.Trim().ToUpperInvariant(), workCenterType.Id, cancellationToken))
        {
            return Result<WorkCenterTypeResponse>.Failure(LocationErrors.WorkCenterTypeCodeConflict);
        }

        var before = WorkCenterTypeMapper.Map(workCenterType);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workCenterType.Update(
                command.Code,
                command.Name,
                command.RequiresAddress,
                command.RequiresGeo,
                command.AllowsBiometric);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = WorkCenterTypeMapper.Map(workCenterType);
            await auditService.LogAsync(
                new AuditLogEntry(
                    "WORK_CENTER_TYPE_UPDATED",
                    "WorkCenterType",
                    workCenterType.PublicId,
                    workCenterType.Code,
                    AuditActions.Update,
                    $"Updated work center type {workCenterType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateWorkCenterTypeCommandHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateWorkCenterTypeCommand, WorkCenterTypeResponse>
{
    public async Task<Result<WorkCenterTypeResponse>> Handle(
        ActivateWorkCenterTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkCenterTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterTypeResponse>.Failure(authorizationResult.Error);
        }

        var workCenterType = await repository.GetByIdAsync(command.WorkCenterTypeId, cancellationToken);
        if (workCenterType is null)
        {
            return Result<WorkCenterTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.WorkCenterTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.WorkCenterTypeNotFound);
        }

        if (workCenterType.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<WorkCenterTypeResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        var before = WorkCenterTypeMapper.Map(workCenterType);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workCenterType.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = WorkCenterTypeMapper.Map(workCenterType);
            await auditService.LogAsync(
                new AuditLogEntry(
                    "WORK_CENTER_TYPE_ACTIVATED",
                    "WorkCenterType",
                    workCenterType.PublicId,
                    workCenterType.Code,
                    AuditActions.Reactivate,
                    $"Activated work center type {workCenterType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateWorkCenterTypeCommandHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterTypeRepository repository,
    ILocationDependencyPolicy dependencyPolicy,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateWorkCenterTypeCommand, WorkCenterTypeResponse>
{
    public async Task<Result<WorkCenterTypeResponse>> Handle(
        InactivateWorkCenterTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkCenterTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterTypeResponse>.Failure(authorizationResult.Error);
        }

        var workCenterType = await repository.GetByIdAsync(command.WorkCenterTypeId, cancellationToken);
        if (workCenterType is null)
        {
            return Result<WorkCenterTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.WorkCenterTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.WorkCenterTypeNotFound);
        }

        if (workCenterType.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<WorkCenterTypeResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        var dependencyResult = await dependencyPolicy.CanInactivateWorkCenterTypeAsync(workCenterType.PublicId, cancellationToken);
        if (dependencyResult.IsFailure)
        {
            return Result<WorkCenterTypeResponse>.Failure(dependencyResult.Error);
        }

        var before = WorkCenterTypeMapper.Map(workCenterType);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workCenterType.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = WorkCenterTypeMapper.Map(workCenterType);
            await auditService.LogAsync(
                new AuditLogEntry(
                    "WORK_CENTER_TYPE_INACTIVATED",
                    "WorkCenterType",
                    workCenterType.PublicId,
                    workCenterType.Code,
                    AuditActions.Deactivate,
                    $"Inactivated work center type {workCenterType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class WorkCenterTypeMapper
{
    public static WorkCenterTypeResponse Map(WorkCenterType workCenterType) =>
        new(
            workCenterType.PublicId,
            workCenterType.Code,
            workCenterType.Name,
            workCenterType.RequiresAddress,
            workCenterType.RequiresGeo,
            workCenterType.AllowsBiometric,
            workCenterType.IsActive,
            workCenterType.ConcurrencyToken,
            workCenterType.CreatedUtc,
            workCenterType.ModifiedUtc);
}
