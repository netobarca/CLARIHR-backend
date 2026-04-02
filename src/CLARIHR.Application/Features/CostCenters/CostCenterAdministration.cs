using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.CostCenters;
using FluentValidation;

namespace CLARIHR.Application.Features.CostCenters;

public sealed record CostCenterListItemResponse(
    Guid Id,
    string Code,
    string Name,
    CostCenterType Type,
    string? PayrollExpenseAccountCode,
    string? EmployerContributionAccountCode,
    string? ProvisionAccountCode,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record CostCenterResponse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    CostCenterType Type,
    string? PayrollExpenseAccountCode,
    string? EmployerContributionAccountCode,
    string? ProvisionAccountCode,
    string? Description,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record CostCenterUsageResponse(
    Guid Id,
    string Code,
    string Name,
    int OrgUnitActiveReferences,
    int OrgUnitInactiveReferences,
    int PositionSlotActiveReferences,
    int PositionSlotInactiveReferences,
    bool HasActiveReferences);

public sealed record CostCenterExportRow(
    Guid Id,
    string Code,
    string Name,
    CostCenterType Type,
    string? PayrollExpenseAccountCode,
    string? EmployerContributionAccountCode,
    string? ProvisionAccountCode,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record SearchCostCentersQuery(
    Guid CompanyId,
    CostCenterType? Type,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = CostCenterValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<CostCenterListItemResponse>>;

public sealed record GetCostCenterByIdQuery(Guid CostCenterId) : IQuery<CostCenterResponse>;

public sealed record GetCostCenterUsageQuery(Guid CostCenterId) : IQuery<CostCenterUsageResponse>;

public sealed record ExportCostCentersQuery(
    Guid CompanyId,
    CostCenterType? Type,
    bool? IsActive,
    string? Search)
    : IQuery<IReadOnlyCollection<CostCenterExportRow>>;

public sealed record CreateCostCenterCommand(
    Guid CompanyId,
    string Code,
    string Name,
    CostCenterType Type,
    string? PayrollExpenseAccountCode,
    string? EmployerContributionAccountCode,
    string? ProvisionAccountCode,
    string? Description)
    : ICommand<CostCenterResponse>;

public sealed record UpdateCostCenterCommand(
    Guid CostCenterId,
    string Code,
    string Name,
    CostCenterType Type,
    string? PayrollExpenseAccountCode,
    string? EmployerContributionAccountCode,
    string? ProvisionAccountCode,
    string? Description,
    Guid ConcurrencyToken)
    : ICommand<CostCenterResponse>;

public sealed record ActivateCostCenterCommand(Guid CostCenterId, Guid ConcurrencyToken)
    : ICommand<CostCenterResponse>;

public sealed record InactivateCostCenterCommand(Guid CostCenterId, Guid ConcurrencyToken)
    : ICommand<CostCenterResponse>;

internal sealed class SearchCostCentersQueryValidator : AbstractValidator<SearchCostCentersQuery>
{
    public SearchCostCentersQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, CostCenterValidationRules.MaxPageSize);
    }
}

internal sealed class GetCostCenterByIdQueryValidator : AbstractValidator<GetCostCenterByIdQuery>
{
    public GetCostCenterByIdQueryValidator()
    {
        RuleFor(query => query.CostCenterId).NotEmpty();
    }
}

internal sealed class GetCostCenterUsageQueryValidator : AbstractValidator<GetCostCenterUsageQuery>
{
    public GetCostCenterUsageQueryValidator()
    {
        RuleFor(query => query.CostCenterId).NotEmpty();
    }
}

internal sealed class ExportCostCentersQueryValidator : AbstractValidator<ExportCostCentersQuery>
{
    public ExportCostCentersQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
    }
}

internal sealed class CreateCostCenterCommandValidator : AbstractValidator<CreateCostCenterCommand>
{
    public CreateCostCenterCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(CostCenterValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.PayrollExpenseAccountCode)
            .MaximumLength(100)
            .Must(CostCenterValidationRules.IsValidAccountCode)
            .When(static command => !string.IsNullOrWhiteSpace(command.PayrollExpenseAccountCode))
            .WithMessage("PayrollExpenseAccountCode format is invalid.");
        RuleFor(command => command.EmployerContributionAccountCode)
            .MaximumLength(100)
            .Must(CostCenterValidationRules.IsValidAccountCode)
            .When(static command => !string.IsNullOrWhiteSpace(command.EmployerContributionAccountCode))
            .WithMessage("EmployerContributionAccountCode format is invalid.");
        RuleFor(command => command.ProvisionAccountCode)
            .MaximumLength(100)
            .Must(CostCenterValidationRules.IsValidAccountCode)
            .When(static command => !string.IsNullOrWhiteSpace(command.ProvisionAccountCode))
            .WithMessage("ProvisionAccountCode format is invalid.");
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

internal sealed class UpdateCostCenterCommandValidator : AbstractValidator<UpdateCostCenterCommand>
{
    public UpdateCostCenterCommandValidator()
    {
        RuleFor(command => command.CostCenterId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(CostCenterValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.PayrollExpenseAccountCode)
            .MaximumLength(100)
            .Must(CostCenterValidationRules.IsValidAccountCode)
            .When(static command => !string.IsNullOrWhiteSpace(command.PayrollExpenseAccountCode))
            .WithMessage("PayrollExpenseAccountCode format is invalid.");
        RuleFor(command => command.EmployerContributionAccountCode)
            .MaximumLength(100)
            .Must(CostCenterValidationRules.IsValidAccountCode)
            .When(static command => !string.IsNullOrWhiteSpace(command.EmployerContributionAccountCode))
            .WithMessage("EmployerContributionAccountCode format is invalid.");
        RuleFor(command => command.ProvisionAccountCode)
            .MaximumLength(100)
            .Must(CostCenterValidationRules.IsValidAccountCode)
            .When(static command => !string.IsNullOrWhiteSpace(command.ProvisionAccountCode))
            .WithMessage("ProvisionAccountCode format is invalid.");
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateCostCenterCommandValidator : AbstractValidator<ActivateCostCenterCommand>
{
    public ActivateCostCenterCommandValidator()
    {
        RuleFor(command => command.CostCenterId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateCostCenterCommandValidator : AbstractValidator<InactivateCostCenterCommand>
{
    public InactivateCostCenterCommandValidator()
    {
        RuleFor(command => command.CostCenterId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchCostCentersQueryHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchCostCentersQuery, PagedResponse<CostCenterListItemResponse>>
{
    public async Task<Result<PagedResponse<CostCenterListItemResponse>>> Handle(
        SearchCostCentersQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<CostCenterListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.Type,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<CostCenterListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => CostCenterPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<CostCenterListItemResponse>>.Success(response);
    }
}

internal sealed class GetCostCenterByIdQueryHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetCostCenterByIdQuery, CostCenterResponse>
{
    public async Task<Result<CostCenterResponse>> Handle(
        GetCostCenterByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CostCenterResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.CostCenterId, cancellationToken);
        if (response is not null)
        {
            var usage = await repository.GetUsageByIdAsync(query.CostCenterId, cancellationToken);
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = CostCenterPolicyAdapter.ApplyAllowedActions(
                response,
                resourceActionPolicyService,
                canManage,
                hasActiveUsage: usage?.HasActiveReferences ?? false);

            return Result<CostCenterResponse>.Success(response);
        }

        return Result<CostCenterResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.CostCenterId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CostCenterErrors.CostCenterNotFound);
    }
}

internal sealed class GetCostCenterUsageQueryHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetCostCenterUsageQuery, CostCenterUsageResponse>
{
    public async Task<Result<CostCenterUsageResponse>> Handle(
        GetCostCenterUsageQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CostCenterUsageResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterUsageResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetUsageByIdAsync(query.CostCenterId, cancellationToken);
        if (response is not null)
        {
            return Result<CostCenterUsageResponse>.Success(response);
        }

        return Result<CostCenterUsageResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.CostCenterId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CostCenterErrors.CostCenterNotFound);
    }
}

internal sealed class ExportCostCentersQueryHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterRepository repository)
    : IQueryHandler<ExportCostCentersQuery, IReadOnlyCollection<CostCenterExportRow>>
{
    public async Task<Result<IReadOnlyCollection<CostCenterExportRow>>> Handle(
        ExportCostCentersQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<CostCenterExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await repository.GetExportRowsAsync(
            query.CompanyId,
            query.Type,
            query.IsActive,
            query.Search,
            cancellationToken);

        return Result<IReadOnlyCollection<CostCenterExportRow>>.Success(rows);
    }
}

internal sealed class CreateCostCenterCommandHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCostCenterCommand, CostCenterResponse>
{
    public async Task<Result<CostCenterResponse>> Handle(
        CreateCostCenterCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(command.CompanyId, command.Code.Trim().ToUpperInvariant(), excludingCostCenterId: null, cancellationToken))
        {
            return Result<CostCenterResponse>.Failure(CostCenterErrors.CodeConflict);
        }

        var costCenter = CostCenter.Create(
            command.Code,
            command.Name,
            command.Type,
            command.PayrollExpenseAccountCode,
            command.EmployerContributionAccountCode,
            command.ProvisionAccountCode,
            command.Description);
        costCenter.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(costCenter);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(costCenter.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Cost center response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CostCenterCreated,
                    AuditEntityTypes.CostCenter,
                    costCenter.PublicId,
                    costCenter.Code,
                    AuditActions.Create,
                    $"Created cost center {costCenter.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CostCenterResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCostCenterCommandHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCostCenterCommand, CostCenterResponse>
{
    public async Task<Result<CostCenterResponse>> Handle(
        UpdateCostCenterCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CostCenterResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterResponse>.Failure(authorizationResult.Error);
        }

        var costCenter = await repository.GetByIdAsync(command.CostCenterId, cancellationToken);
        if (costCenter is null)
        {
            return Result<CostCenterResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CostCenterId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CostCenterErrors.CostCenterNotFound);
        }

        if (costCenter.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CostCenterResponse>.Failure(CostCenterErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(costCenter.TenantId, command.Code.Trim().ToUpperInvariant(), costCenter.Id, cancellationToken))
        {
            return Result<CostCenterResponse>.Failure(CostCenterErrors.CodeConflict);
        }

        var before = await repository.GetResponseByIdAsync(costCenter.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Cost center response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            costCenter.Update(
                command.Code,
                command.Name,
                command.Type,
                command.PayrollExpenseAccountCode,
                command.EmployerContributionAccountCode,
                command.ProvisionAccountCode,
                command.Description);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(costCenter.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Cost center response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CostCenterUpdated,
                    AuditEntityTypes.CostCenter,
                    costCenter.PublicId,
                    costCenter.Code,
                    AuditActions.Update,
                    $"Updated cost center {costCenter.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CostCenterResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateCostCenterCommandHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateCostCenterCommand, CostCenterResponse>
{
    public async Task<Result<CostCenterResponse>> Handle(
        ActivateCostCenterCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CostCenterResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterResponse>.Failure(authorizationResult.Error);
        }

        var costCenter = await repository.GetByIdAsync(command.CostCenterId, cancellationToken);
        if (costCenter is null)
        {
            return Result<CostCenterResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CostCenterId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CostCenterErrors.CostCenterNotFound);
        }

        if (costCenter.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CostCenterResponse>.Failure(CostCenterErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(costCenter.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Cost center response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            costCenter.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(costCenter.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Cost center response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CostCenterActivated,
                    AuditEntityTypes.CostCenter,
                    costCenter.PublicId,
                    costCenter.Code,
                    AuditActions.Reactivate,
                    $"Activated cost center {costCenter.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CostCenterResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateCostCenterCommandHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateCostCenterCommand, CostCenterResponse>
{
    public async Task<Result<CostCenterResponse>> Handle(
        InactivateCostCenterCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CostCenterResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CostCenterResponse>.Failure(authorizationResult.Error);
        }

        var costCenter = await repository.GetByIdAsync(command.CostCenterId, cancellationToken);
        if (costCenter is null)
        {
            return Result<CostCenterResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CostCenterId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CostCenterErrors.CostCenterNotFound);
        }

        if (costCenter.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CostCenterResponse>.Failure(CostCenterErrors.ConcurrencyConflict);
        }

        if (await repository.HasActiveUsageAsync(costCenter.Id, cancellationToken))
        {
            return Result<CostCenterResponse>.Failure(CostCenterErrors.InUseConflict);
        }

        var before = await repository.GetResponseByIdAsync(costCenter.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Cost center response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            costCenter.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(costCenter.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Cost center response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CostCenterInactivated,
                    AuditEntityTypes.CostCenter,
                    costCenter.PublicId,
                    costCenter.Code,
                    AuditActions.Deactivate,
                    $"Inactivated cost center {costCenter.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CostCenterResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class CostCenterPolicyAdapter
{
    public static CostCenterListItemResponse ApplyAllowedActions(
        CostCenterListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                CostCenterPermissionCodes.ResourceKey,
                response.Type.ToString(),
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }

    public static CostCenterResponse ApplyAllowedActions(
        CostCenterResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage,
        bool hasActiveUsage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                CostCenterPermissionCodes.ResourceKey,
                response.Type.ToString(),
                response.IsActive,
                HasDependencies: hasActiveUsage,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }
}
