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
    string? Search,
    int? MaxRows = null)
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

public sealed record CostCenterPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchCostCenterCommand(
    Guid CostCenterId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<CostCenterPatchOperation> Operations)
    : ICommand<CostCenterResponse>;

internal sealed class SearchCostCentersQueryValidator : AbstractValidator<SearchCostCentersQuery>
{
    public SearchCostCentersQueryValidator()
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
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(CostCenterValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {CostCenterValidationRules.MinSearchLength} characters when provided.");
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

internal sealed class PatchCostCenterCommandValidator : AbstractValidator<PatchCostCenterCommand>
{
    public PatchCostCenterCommandValidator()
    {
        RuleFor(command => command.CostCenterId).NotEmpty();
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
            // CC1 (perf): the detail GET only needs the boolean "has active usage" to populate
            // allowedActions. Use the cheap probe (1-2 queries, early-exit) keyed by the authenticated
            // tenant and the cost center's normalized code — instead of GetUsageByIdAsync, which
            // computes the full 5-query reference breakdown (reserved for the dedicated /usage
            // endpoint). `response.Code` is the normalized code (CostCenter keeps Code == NormalizedCode).
            var hasActiveUsage = await repository.HasActiveUsageAsync(
                tenantContext.TenantId.Value,
                response.Code,
                cancellationToken);
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = CostCenterPolicyAdapter.ApplyAllowedActions(
                response,
                resourceActionPolicyService,
                canManage,
                hasActiveUsage: hasActiveUsage);

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
            query.MaxRows,
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
        catch (UniqueConstraintViolationException ex) when (CostCenterConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CostCenterResponse>.Failure(CostCenterErrors.CodeConflict);
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
        catch (UniqueConstraintViolationException ex) when (CostCenterConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CostCenterResponse>.Failure(CostCenterErrors.CodeConflict);
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

        // R3 (perf): the entity is already loaded, so probe usage by (tenant, normalized code)
        // directly — avoids the extra round-trip the long-id overload paid to re-resolve them.
        if (await repository.HasActiveUsageAsync(costCenter.TenantId, costCenter.NormalizedCode, cancellationToken))
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

internal sealed class PatchCostCenterCommandHandler(
    ICostCenterAuthorizationService authorizationService,
    ICostCenterRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchCostCenterCommand, CostCenterResponse>
{
    public async Task<Result<CostCenterResponse>> Handle(
        PatchCostCenterCommand command,
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
            ?? throw new InvalidOperationException("Cost center response could not be resolved before patch.");

        var state = CostCenterPatchState.From(before);

        var applied = CostCenterPatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<CostCenterResponse>.Failure(applied.Error);
        }

        var validation = CostCenterPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<CostCenterResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<CostCenterResponse>.Success(before);
        }

        if (await repository.CodeExistsAsync(costCenter.TenantId, state.Code.Trim().ToUpperInvariant(), costCenter.Id, cancellationToken))
        {
            return Result<CostCenterResponse>.Failure(CostCenterErrors.CodeConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            costCenter.Update(
                state.Code,
                state.Name,
                state.Type,
                state.PayrollExpenseAccountCode,
                state.EmployerContributionAccountCode,
                state.ProvisionAccountCode,
                state.Description);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(costCenter.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Cost center response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CostCenterUpdated,
                    AuditEntityTypes.CostCenter,
                    costCenter.PublicId,
                    costCenter.Code,
                    AuditActions.Update,
                    $"Patched cost center {costCenter.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CostCenterResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (CostCenterConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CostCenterResponse>.Failure(CostCenterErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class CostCenterConstraintViolations
{
    // The (TenantId, NormalizedCode) unique index is the real guard against duplicate codes; the
    // up-front CodeExistsAsync probe only closes the common (sequential) case. On a concurrent
    // create/update of the same code, the second writer trips this index — map it to the same clean
    // 409 as the probe instead of letting the 23505 escape as an HTTP 500 (mirrors the
    // JobProfileCompensation per-profile-constraint pattern).
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, CostCenterValidationRules.CodeUniqueConstraintName, StringComparison.Ordinal);
}

internal sealed class CostCenterPatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CostCenterType Type { get; set; }
    public string? PayrollExpenseAccountCode { get; set; }
    public string? EmployerContributionAccountCode { get; set; }
    public string? ProvisionAccountCode { get; set; }
    public string? Description { get; set; }
    public bool HasMutation { get; set; }

    public static CostCenterPatchState From(CostCenterResponse response) =>
        new()
        {
            Code = response.Code,
            Name = response.Name,
            Type = response.Type,
            PayrollExpenseAccountCode = response.PayrollExpenseAccountCode,
            EmployerContributionAccountCode = response.EmployerContributionAccountCode,
            ProvisionAccountCode = response.ProvisionAccountCode,
            Description = response.Description
        };
}

internal sealed class CostCenterPatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class CostCenterPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<CostCenterPatchOperation> operations, CostCenterPatchState state)
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
                return ValidationFailure(operation.Path, "Only root cost center properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (CostCenterPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(CostCenterPatchState state)
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

        ValidateAccountCode(errors, "payrollExpenseAccountCode", state.PayrollExpenseAccountCode);
        ValidateAccountCode(errors, "employerContributionAccountCode", state.EmployerContributionAccountCode);
        ValidateAccountCode(errors, "provisionAccountCode", state.ProvisionAccountCode);

        if (state.Description is { Length: > 500 })
        {
            errors["description"] = ["Description must be 500 characters or fewer."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static void ValidateAccountCode(Dictionary<string, string[]> errors, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.Length > 100)
        {
            errors[key] = ["Account code must be 100 characters or fewer."];
        }
        else if (!CostCenterValidationRules.IsValidAccountCode(value))
        {
            errors[key] = ["Account code format is invalid."];
        }
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        CostCenterPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "concurrencyToken"))
        {
            return ValidationFailure(path, "The concurrency token cannot be patched; send the current token in the If-Match header.");
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

        if (IsSegment(property, "type"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Type cannot be removed.");
            }

            state.Type = ReadEnum<CostCenterType>(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "payrollExpenseAccountCode"))
        {
            state.PayrollExpenseAccountCode = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "employerContributionAccountCode"))
        {
            state.EmployerContributionAccountCode = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "provisionAccountCode"))
        {
            state.ProvisionAccountCode = isRemove ? null : ReadNullableString(value, path);
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
            throw new CostCenterPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new CostCenterPatchValueException(path, "Value must be a string.");
    }

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new CostCenterPatchValueException(path, "Value must be a string or null.");
    }

    private static TEnum ReadEnum<TEnum>(JsonElement? value, string path)
        where TEnum : struct, Enum
    {
        if (IsNull(value))
        {
            throw new CostCenterPatchValueException(path, "Value is required.");
        }

        if (value!.Value.ValueKind == JsonValueKind.String &&
            Enum.TryParse<TEnum>(value.Value.GetString(), ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new CostCenterPatchValueException(path, $"Value must be a valid {typeof(TEnum).Name}.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
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
