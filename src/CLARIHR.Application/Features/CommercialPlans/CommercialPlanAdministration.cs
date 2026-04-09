using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CommercialPlans.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Companies;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.CommercialPlans;

internal sealed class SearchCommercialPlansQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialPlanRepository repository)
    : IQueryHandler<SearchCommercialPlansQuery, PagedResponse<CommercialPlanSummaryResponse>>
{
    public async Task<Result<PagedResponse<CommercialPlanSummaryResponse>>> Handle(
        SearchCommercialPlansQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<CommercialPlanSummaryResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.Status,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<CommercialPlanSummaryResponse>>.Success(response);
    }
}

internal sealed class GetCommercialPlanByIdQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialPlanRepository repository)
    : IQueryHandler<GetCommercialPlanByIdQuery, CommercialPlanResponse>
{
    public async Task<Result<CommercialPlanResponse>> Handle(
        GetCommercialPlanByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CommercialPlanResponse>.Failure(authorizationResult.Error);
        }

        var plan = await repository.GetByIdAsync(query.CommercialPlanId, cancellationToken);
        return plan is null
            ? Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.NotFound)
            : Result<CommercialPlanResponse>.Success(CommercialPlanMapper.Map(plan));
    }
}

internal sealed class CreateCommercialPlanCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialPlanRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    ILogger<CreateCommercialPlanCommandHandler> logger)
    : ICommandHandler<CreateCommercialPlanCommand, CommercialPlanResponse>
{
    public async Task<Result<CommercialPlanResponse>> Handle(
        CreateCommercialPlanCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CommercialPlanResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(command.Code.Trim().ToUpperInvariant(), excludingId: null, cancellationToken))
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.CodeConflict);
        }

        var plan = CommercialPlan.Create(
            command.Code,
            command.Name,
            command.Description,
            command.BaseMonthlyFee,
            command.PricePerActiveEmployee,
            command.Status,
            isSystemPlan: false,
            command.ModuleKeys,
            CommercialPlanMapper.ToLimitData(command.Limits),
            dateTimeProvider.UtcNow);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(plan);
            var response = CommercialPlanMapper.Map(plan);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CommercialPlanCreated,
                    AuditEntityTypes.CommercialPlan,
                    plan.PublicId,
                    plan.Code,
                    AuditActions.Create,
                    $"Created commercial plan {plan.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial plan {CommercialPlanCode} created by user {UserId}.",
                plan.Code,
                currentUserService.UserId);

            return Result<CommercialPlanResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCommercialPlanCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialPlanRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    ILogger<UpdateCommercialPlanCommandHandler> logger)
    : ICommandHandler<UpdateCommercialPlanCommand, CommercialPlanResponse>
{
    public async Task<Result<CommercialPlanResponse>> Handle(
        UpdateCommercialPlanCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CommercialPlanResponse>.Failure(authorizationResult.Error);
        }

        var plan = await repository.GetByIdAsync(command.CommercialPlanId, cancellationToken);
        if (plan is null)
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.NotFound);
        }

        if (plan.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.ConcurrencyConflict);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(normalizedCode, plan.Id, cancellationToken))
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.CodeConflict);
        }

        if (plan.IsSystemPlan &&
            (!string.Equals(plan.Code, normalizedCode, StringComparison.Ordinal) ||
             !string.Equals(plan.Name, command.Name.Trim(), StringComparison.Ordinal)))
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.SystemPlanRenameForbidden);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = CommercialPlanMapper.Map(plan);
            var moduleKeys = plan.IsSystemPlan &&
                             string.Equals(plan.Code, ProvisioningConstants.MasterPlanCode, StringComparison.Ordinal)
                ? CommercialModuleCatalog.DefaultMasterModuleKeys
                : command.ModuleKeys;
            plan.Update(
                command.Code,
                command.Name,
                command.Description,
                command.BaseMonthlyFee,
                command.PricePerActiveEmployee,
                moduleKeys,
                CommercialPlanMapper.ToLimitData(command.Limits),
                dateTimeProvider.UtcNow);

            var after = CommercialPlanMapper.Map(plan);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CommercialPlanUpdated,
                    AuditEntityTypes.CommercialPlan,
                    plan.PublicId,
                    plan.Code,
                    AuditActions.Update,
                    $"Updated commercial plan {plan.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial plan {CommercialPlanCode} updated by user {UserId}.",
                plan.Code,
                currentUserService.UserId);

            return Result<CommercialPlanResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateCommercialPlanCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialPlanRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<ActivateCommercialPlanCommandHandler> logger)
    : ICommandHandler<ActivateCommercialPlanCommand, CommercialPlanResponse>
{
    public async Task<Result<CommercialPlanResponse>> Handle(
        ActivateCommercialPlanCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CommercialPlanResponse>.Failure(authorizationResult.Error);
        }

        var plan = await repository.GetByIdAsync(command.CommercialPlanId, cancellationToken);
        if (plan is null)
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.NotFound);
        }

        if (plan.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.ConcurrencyConflict);
        }

        if (plan.Status == CommercialPlanStatus.Active)
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.AlreadyActive);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = CommercialPlanMapper.Map(plan);
            plan.Activate();
            var after = CommercialPlanMapper.Map(plan);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CommercialPlanActivated,
                    AuditEntityTypes.CommercialPlan,
                    plan.PublicId,
                    plan.Code,
                    AuditActions.Reactivate,
                    $"Activated commercial plan {plan.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial plan {CommercialPlanCode} activated by user {UserId}.",
                plan.Code,
                currentUserService.UserId);

            return Result<CommercialPlanResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateCommercialPlanCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialPlanRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<InactivateCommercialPlanCommandHandler> logger)
    : ICommandHandler<InactivateCommercialPlanCommand, CommercialPlanResponse>
{
    public async Task<Result<CommercialPlanResponse>> Handle(
        InactivateCommercialPlanCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CommercialPlanResponse>.Failure(authorizationResult.Error);
        }

        var plan = await repository.GetByIdAsync(command.CommercialPlanId, cancellationToken);
        if (plan is null)
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.NotFound);
        }

        if (plan.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.ConcurrencyConflict);
        }

        if (plan.IsSystemPlan)
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.SystemPlanInactivationForbidden);
        }

        if (plan.Status == CommercialPlanStatus.Inactive)
        {
            return Result<CommercialPlanResponse>.Failure(CommercialPlanErrors.AlreadyInactive);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = CommercialPlanMapper.Map(plan);
            plan.Inactivate();
            var after = CommercialPlanMapper.Map(plan);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CommercialPlanInactivated,
                    AuditEntityTypes.CommercialPlan,
                    plan.PublicId,
                    plan.Code,
                    AuditActions.Deactivate,
                    $"Inactivated commercial plan {plan.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial plan {CommercialPlanCode} inactivated by user {UserId}.",
                plan.Code,
                currentUserService.UserId);

            return Result<CommercialPlanResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class CommercialPlanMapper
{
    public static CommercialPlanResponse Map(CommercialPlan plan)
    {
        var currentVersion = plan.GetCurrentVersion();

        return new CommercialPlanResponse(
            plan.PublicId,
            plan.Code,
            plan.Name,
            plan.Description,
            plan.BaseMonthlyFee,
            plan.PricePerActiveEmployee,
            currentVersion.VersionNumber,
            currentVersion.CurrencyCode,
            plan.Status,
            plan.IsSystemPlan,
            plan.Entitlements.Count(entitlement => entitlement.IsEnabled),
            plan.ConcurrencyToken,
            plan.CreatedUtc,
            plan.ModifiedUtc,
            plan.Entitlements
                .Where(entitlement => entitlement.IsEnabled)
                .OrderBy(entitlement => entitlement.ModuleKey, StringComparer.Ordinal)
                .Select(entitlement => entitlement.ModuleKey)
                .ToArray(),
            plan.Limits
                .OrderBy(limit => limit.NormalizedLimitCode, StringComparer.Ordinal)
                .Select(limit => new CommercialPlanLimitResponse(limit.LimitCode, limit.Value))
                .ToArray());
    }

    public static IReadOnlyCollection<(string LimitCode, decimal Value)> ToLimitData(
        IReadOnlyCollection<CommercialPlanLimitInput> limits) =>
        limits
            .Select(limit => (limit.Code, limit.Value))
            .ToArray();
}
