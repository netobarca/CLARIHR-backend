using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Overtime;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Overtime.Common;
using CLARIHR.Domain.Overtime;

namespace CLARIHR.Application.Features.PersonnelFiles.Overtime;

internal sealed class SearchOvertimeJustificationTypesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeJustificationTypeRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchOvertimeJustificationTypesQuery, PagedResponse<OvertimeJustificationTypeListItemResponse>>
{
    public async Task<Result<PagedResponse<OvertimeJustificationTypeListItemResponse>>> Handle(
        SearchOvertimeJustificationTypesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOvertimeRecordsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<OvertimeJustificationTypeListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<OvertimeJustificationTypeListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageOvertimeRecordsAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => OvertimeJustificationTypePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<OvertimeJustificationTypeListItemResponse>>.Success(response);
    }
}

internal sealed class GetOvertimeJustificationTypeByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeJustificationTypeRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetOvertimeJustificationTypeByIdQuery, OvertimeJustificationTypeResponse>
{
    public async Task<Result<OvertimeJustificationTypeResponse>> Handle(
        GetOvertimeJustificationTypeByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanViewOvertimeRecordsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.JustificationTypeId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageOvertimeRecordsAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = OvertimeJustificationTypePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<OvertimeJustificationTypeResponse>.Success(response);
        }

        return Result<OvertimeJustificationTypeResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.JustificationTypeId, cancellationToken)
                ? OvertimeJustificationTypeErrors.TenantMismatch(RbacPermissionAction.Read)
                : OvertimeJustificationTypeErrors.JustificationTypeNotFound);
    }
}

internal sealed class CreateOvertimeJustificationTypeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeJustificationTypeRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateOvertimeJustificationTypeCommand, OvertimeJustificationTypeResponse>
{
    public async Task<Result<OvertimeJustificationTypeResponse>> Handle(
        CreateOvertimeJustificationTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageOvertimeRecordsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Code.Trim().ToUpperInvariant(),
                excludingJustificationTypeId: null,
                cancellationToken))
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(OvertimeJustificationTypeErrors.CodeTaken);
        }

        var type = OvertimeJustificationType.Create(
            command.Code,
            command.Name,
            command.Description,
            command.SortOrder);
        type.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(type);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Overtime justification type response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OvertimeJustificationTypeCreated,
                    AuditEntityTypes.OvertimeJustificationType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Create,
                    $"Created overtime justification type {type.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OvertimeJustificationTypeResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (OvertimeJustificationTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<OvertimeJustificationTypeResponse>.Failure(OvertimeJustificationTypeErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateOvertimeJustificationTypeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeJustificationTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateOvertimeJustificationTypeCommand, OvertimeJustificationTypeResponse>
{
    public async Task<Result<OvertimeJustificationTypeResponse>> Handle(
        UpdateOvertimeJustificationTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageOvertimeRecordsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.JustificationTypeId, cancellationToken);
        if (type is null)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JustificationTypeId, cancellationToken)
                    ? OvertimeJustificationTypeErrors.TenantMismatch(RbacPermissionAction.Update)
                    : OvertimeJustificationTypeErrors.JustificationTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(OvertimeJustificationTypeErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(
                type.TenantId,
                command.Code.Trim().ToUpperInvariant(),
                type.PublicId,
                cancellationToken))
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(OvertimeJustificationTypeErrors.CodeTaken);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Overtime justification type response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Update(
                command.Code,
                command.Name,
                command.Description,
                command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Overtime justification type response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OvertimeJustificationTypeUpdated,
                    AuditEntityTypes.OvertimeJustificationType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Update,
                    $"Updated overtime justification type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OvertimeJustificationTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (OvertimeJustificationTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<OvertimeJustificationTypeResponse>.Failure(OvertimeJustificationTypeErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateOvertimeJustificationTypeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeJustificationTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateOvertimeJustificationTypeCommand, OvertimeJustificationTypeResponse>
{
    public async Task<Result<OvertimeJustificationTypeResponse>> Handle(
        ActivateOvertimeJustificationTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageOvertimeRecordsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.JustificationTypeId, cancellationToken);
        if (type is null)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JustificationTypeId, cancellationToken)
                    ? OvertimeJustificationTypeErrors.TenantMismatch(RbacPermissionAction.Update)
                    : OvertimeJustificationTypeErrors.JustificationTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(OvertimeJustificationTypeErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Overtime justification type response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Overtime justification type response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OvertimeJustificationTypeActivated,
                    AuditEntityTypes.OvertimeJustificationType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Reactivate,
                    $"Activated overtime justification type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OvertimeJustificationTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (OvertimeJustificationTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<OvertimeJustificationTypeResponse>.Failure(OvertimeJustificationTypeErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateOvertimeJustificationTypeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeJustificationTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateOvertimeJustificationTypeCommand, OvertimeJustificationTypeResponse>
{
    public async Task<Result<OvertimeJustificationTypeResponse>> Handle(
        InactivateOvertimeJustificationTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageOvertimeRecordsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.JustificationTypeId, cancellationToken);
        if (type is null)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JustificationTypeId, cancellationToken)
                    ? OvertimeJustificationTypeErrors.TenantMismatch(RbacPermissionAction.Update)
                    : OvertimeJustificationTypeErrors.JustificationTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(OvertimeJustificationTypeErrors.ConcurrencyConflict);
        }

        // Logical-delete usage guard (OVERTIME_JUSTIFICATION_TYPE_IN_USE). PR-1 has no overtime record table
        // yet (M2/PR-2), so IsInUseAsync is a stub returning false today; the real reference check is wired
        // in PR-3 once that table exists.
        if (await repository.IsInUseAsync(type.TenantId, type.PublicId, cancellationToken))
        {
            return Result<OvertimeJustificationTypeResponse>.Failure(OvertimeJustificationTypeErrors.InUse);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Overtime justification type response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Overtime justification type response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OvertimeJustificationTypeInactivated,
                    AuditEntityTypes.OvertimeJustificationType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Deactivate,
                    $"Inactivated overtime justification type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OvertimeJustificationTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class OvertimeJustificationTypeConstraintViolations
{
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, OvertimeMasterConstraintNames.OvertimeJustificationTypeCodeUnique, StringComparison.Ordinal);
}

internal static class OvertimeJustificationTypePolicyAdapter
{
    public static OvertimeJustificationTypeListItemResponse ApplyAllowedActions(
        OvertimeJustificationTypeListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    public static OvertimeJustificationTypeResponse ApplyAllowedActions(
        OvertimeJustificationTypeResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    private static AllowedActionsResponse Evaluate(
        bool isActive,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                OvertimeConfigurationResourceKeys.OvertimeJustificationTypes,
                State: null,
                isActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));
}
