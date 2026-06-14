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

namespace CLARIHR.Application.Features.Locations.WorkCenterTypes;

public sealed record WorkCenterTypeResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool RequiresAddress,
    bool RequiresGeo,
    bool AllowsBiometric,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record GetWorkCenterTypesQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = LocationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<WorkCenterTypeResponse>>;

public sealed record GetWorkCenterTypeByIdQuery(Guid WorkCenterTypeId) : IQuery<WorkCenterTypeResponse>;

public sealed record CreateWorkCenterTypeCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string? Description,
    bool RequiresAddress,
    bool RequiresGeo,
    bool AllowsBiometric) : ICommand<WorkCenterTypeResponse>;

public sealed record UpdateWorkCenterTypeCommand(
    Guid WorkCenterTypeId,
    string Code,
    string Name,
    string? Description,
    bool RequiresAddress,
    bool RequiresGeo,
    bool AllowsBiometric,
    Guid ConcurrencyToken) : ICommand<WorkCenterTypeResponse>;

public sealed record ActivateWorkCenterTypeCommand(Guid WorkCenterTypeId, Guid ConcurrencyToken) : ICommand<WorkCenterTypeResponse>;

public sealed record InactivateWorkCenterTypeCommand(Guid WorkCenterTypeId, Guid ConcurrencyToken) : ICommand<WorkCenterTypeResponse>;

public sealed record WorkCenterTypePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchWorkCenterTypeCommand(
    Guid WorkCenterTypeId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<WorkCenterTypePatchOperation> Operations) : ICommand<WorkCenterTypeResponse>;

internal sealed class GetWorkCenterTypesQueryValidator : AbstractValidator<GetWorkCenterTypesQuery>
{
    public GetWorkCenterTypesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(LocationValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {LocationValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, LocationValidationRules.MaxPageSize);
    }
}

internal sealed class GetWorkCenterTypeByIdQueryValidator : AbstractValidator<GetWorkCenterTypeByIdQuery>
{
    public GetWorkCenterTypeByIdQueryValidator()
    {
        RuleFor(query => query.WorkCenterTypeId).NotEmpty();
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
        RuleFor(command => command.Description).MaximumLength(500);
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
        RuleFor(command => command.Description).MaximumLength(500);
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

internal sealed class PatchWorkCenterTypeCommandValidator : AbstractValidator<PatchWorkCenterTypeCommand>
{
    public PatchWorkCenterTypeCommandValidator()
    {
        RuleFor(command => command.WorkCenterTypeId).NotEmpty();
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

internal sealed class GetWorkCenterTypesQueryHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterTypeRepository repository,
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
            // §12.7 (ADR-0001): list AllowedActions derive ONLY from the caller's permission (canManage),
            // never from per-item dependency state — resolving dependencies per row is the forbidden N+1.
            // The real inactivation block is enforced server-side in InactivateWorkCenterTypeCommandHandler.
            var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
            var enrichedItems = response.Items
                .Select(workCenterType => WorkCenterTypePolicyAdapter.ApplyAllowedActions(workCenterType, resourceActionPolicyService, canManage))
                .ToArray();

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
        bool canManage) =>
        response with
        {
            AllowedActions = resourceActionPolicyService.Evaluate(
                new ResourceActionContext(
                    ResourceKey: "WorkCenterTypes",
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
            command.Description,
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
                    AuditEventTypes.WorkCenterTypeCreated,
                    AuditEntityTypes.WorkCenterType,
                    workCenterType.PublicId,
                    workCenterType.Code,
                    AuditActions.Create,
                    $"Created work center type {workCenterType.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterTypeResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException exception)
            when (LocationConstraintViolations.IsWorkCenterTypeCodeConflict(exception.ConstraintName))
        {
            // WCT-A: two concurrent creates with the same code both pass CodeExistsAsync; the second trips
            // the (TenantId, NormalizedCode) unique index → the same clean 409 as the probe (mirror CostCenters R2).
            await transaction.RollbackAsync(cancellationToken);
            return Result<WorkCenterTypeResponse>.Failure(LocationErrors.WorkCenterTypeCodeConflict);
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
                command.Description,
                command.RequiresAddress,
                command.RequiresGeo,
                command.AllowsBiometric);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = WorkCenterTypeMapper.Map(workCenterType);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.WorkCenterTypeUpdated,
                    AuditEntityTypes.WorkCenterType,
                    workCenterType.PublicId,
                    workCenterType.Code,
                    AuditActions.Update,
                    $"Updated work center type {workCenterType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException exception)
            when (LocationConstraintViolations.IsWorkCenterTypeCodeConflict(exception.ConstraintName))
        {
            // WCT-A: a concurrent rename to the same code trips the (TenantId, NormalizedCode) unique index
            // after CodeExistsAsync passed → the same clean 409 as the probe (mirror CostCenters R2).
            await transaction.RollbackAsync(cancellationToken);
            return Result<WorkCenterTypeResponse>.Failure(LocationErrors.WorkCenterTypeCodeConflict);
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
                    AuditEventTypes.WorkCenterTypeActivated,
                    AuditEntityTypes.WorkCenterType,
                    workCenterType.PublicId,
                    workCenterType.Code,
                    AuditActions.Reactivate,
                    $"Activated work center type {workCenterType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

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
                    AuditEventTypes.WorkCenterTypeInactivated,
                    AuditEntityTypes.WorkCenterType,
                    workCenterType.PublicId,
                    workCenterType.Code,
                    AuditActions.Deactivate,
                    $"Inactivated work center type {workCenterType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

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

internal sealed class GetWorkCenterTypeByIdQueryHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterTypeRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetWorkCenterTypeByIdQuery, WorkCenterTypeResponse>
{
    public async Task<Result<WorkCenterTypeResponse>> Handle(
        GetWorkCenterTypeByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkCenterTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkCenterTypeResponse>.Failure(authorizationResult.Error);
        }

        var workCenterType = await repository.GetByIdAsync(query.WorkCenterTypeId, cancellationToken);
        if (workCenterType is not null)
        {
            return Result<WorkCenterTypeResponse>.Success(WorkCenterTypeMapper.Map(workCenterType));
        }

        return Result<WorkCenterTypeResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.WorkCenterTypeId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : LocationErrors.WorkCenterTypeNotFound);
    }
}

internal sealed class PatchWorkCenterTypeCommandHandler(
    ILocationAuthorizationService authorizationService,
    IWorkCenterTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchWorkCenterTypeCommand, WorkCenterTypeResponse>
{
    public async Task<Result<WorkCenterTypeResponse>> Handle(
        PatchWorkCenterTypeCommand command,
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
        var state = WorkCenterTypePatchState.From(before);

        var applied = WorkCenterTypePatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<WorkCenterTypeResponse>.Failure(applied.Error);
        }

        var validation = WorkCenterTypePatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<WorkCenterTypeResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<WorkCenterTypeResponse>.Success(before);
        }

        if (await repository.CodeExistsAsync(workCenterType.TenantId, state.Code.Trim().ToUpperInvariant(), workCenterType.Id, cancellationToken))
        {
            return Result<WorkCenterTypeResponse>.Failure(LocationErrors.WorkCenterTypeCodeConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            workCenterType.Update(state.Code, state.Name, state.Description, state.RequiresAddress, state.RequiresGeo, state.AllowsBiometric);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = WorkCenterTypeMapper.Map(workCenterType);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.WorkCenterTypeUpdated,
                    AuditEntityTypes.WorkCenterType,
                    workCenterType.PublicId,
                    workCenterType.Code,
                    AuditActions.Update,
                    $"Patched work center type {workCenterType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkCenterTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException exception)
            when (LocationConstraintViolations.IsWorkCenterTypeCodeConflict(exception.ConstraintName))
        {
            // WCT-A: a concurrent patch to the same code trips the (TenantId, NormalizedCode) unique index
            // after CodeExistsAsync passed → the same clean 409 as the probe (mirror CostCenters R2).
            await transaction.RollbackAsync(cancellationToken);
            return Result<WorkCenterTypeResponse>.Failure(LocationErrors.WorkCenterTypeCodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class WorkCenterTypePatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RequiresAddress { get; set; }
    public bool RequiresGeo { get; set; }
    public bool AllowsBiometric { get; set; }
    public bool HasMutation { get; set; }

    public static WorkCenterTypePatchState From(WorkCenterTypeResponse response) =>
        new()
        {
            Code = response.Code,
            Name = response.Name,
            Description = response.Description,
            RequiresAddress = response.RequiresAddress,
            RequiresGeo = response.RequiresGeo,
            AllowsBiometric = response.AllowsBiometric
        };
}

internal sealed class WorkCenterTypePatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class WorkCenterTypePatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<WorkCenterTypePatchOperation> operations, WorkCenterTypePatchState state)
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
                return ValidationFailure(operation.Path, "Only root work center type properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (WorkCenterTypePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(WorkCenterTypePatchState state)
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
        WorkCenterTypePatchState state,
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

        if (IsSegment(property, "requiresAddress"))
        {
            state.RequiresAddress = ReadBool(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "requiresGeo"))
        {
            state.RequiresGeo = ReadBool(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "allowsBiometric"))
        {
            state.AllowsBiometric = ReadBool(value, path);
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
            throw new WorkCenterTypePatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new WorkCenterTypePatchValueException(path, "Value must be a string.");
    }

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new WorkCenterTypePatchValueException(path, "Value must be a string or null.");
    }

    private static bool ReadBool(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new WorkCenterTypePatchValueException(path, "Value must be a boolean.");
        }

        return value!.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.Value.GetString(), out var parsed) => parsed,
            _ => throw new WorkCenterTypePatchValueException(path, "Value must be a boolean.")
        };
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

internal static class WorkCenterTypeMapper
{
    public static WorkCenterTypeResponse Map(WorkCenterType workCenterType) =>
        new(
            workCenterType.PublicId,
            workCenterType.Code,
            workCenterType.Name,
            workCenterType.Description,
            workCenterType.RequiresAddress,
            workCenterType.RequiresGeo,
            workCenterType.AllowsBiometric,
            workCenterType.IsActive,
            workCenterType.ConcurrencyToken,
            workCenterType.CreatedUtc,
            workCenterType.ModifiedUtc);
}
