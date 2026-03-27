using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialPlans.Common;
using CLARIHR.Domain.Companies;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.CommercialPlans;

internal sealed class SearchCommercialPlansQueryHandler(
    ICommercialPlanAuthorizationService authorizationService,
    ICommercialPlanRepository repository)
    : IQueryHandler<SearchCommercialPlansQuery, PagedResponse<CommercialPlanSummaryResponse>>
{
    public async Task<Result<PagedResponse<CommercialPlanSummaryResponse>>> Handle(
        SearchCommercialPlansQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsurePlatformAdministrationAsync(cancellationToken);
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
    ICommercialPlanAuthorizationService authorizationService,
    ICommercialPlanRepository repository)
    : IQueryHandler<GetCommercialPlanByIdQuery, CommercialPlanResponse>
{
    public async Task<Result<CommercialPlanResponse>> Handle(
        GetCommercialPlanByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsurePlatformAdministrationAsync(cancellationToken);
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
    ICommercialPlanAuthorizationService authorizationService,
    ICommercialPlanRepository repository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<CreateCommercialPlanCommandHandler> logger)
    : ICommandHandler<CreateCommercialPlanCommand, CommercialPlanResponse>
{
    public async Task<Result<CommercialPlanResponse>> Handle(
        CreateCommercialPlanCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsurePlatformAdministrationAsync(cancellationToken);
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
            CommercialPlanMapper.ToLimitData(command.Limits));

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(plan);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial plan {CommercialPlanCode} created by user {UserId}.",
                plan.Code,
                currentUserService.UserId);

            return Result<CommercialPlanResponse>.Success(CommercialPlanMapper.Map(plan));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCommercialPlanCommandHandler(
    ICommercialPlanAuthorizationService authorizationService,
    ICommercialPlanRepository repository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<UpdateCommercialPlanCommandHandler> logger)
    : ICommandHandler<UpdateCommercialPlanCommand, CommercialPlanResponse>
{
    public async Task<Result<CommercialPlanResponse>> Handle(
        UpdateCommercialPlanCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsurePlatformAdministrationAsync(cancellationToken);
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
            plan.Update(
                command.Code,
                command.Name,
                command.Description,
                command.BaseMonthlyFee,
                command.PricePerActiveEmployee,
                CommercialPlanMapper.ToLimitData(command.Limits));

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial plan {CommercialPlanCode} updated by user {UserId}.",
                plan.Code,
                currentUserService.UserId);

            return Result<CommercialPlanResponse>.Success(CommercialPlanMapper.Map(plan));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateCommercialPlanCommandHandler(
    ICommercialPlanAuthorizationService authorizationService,
    ICommercialPlanRepository repository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<ActivateCommercialPlanCommandHandler> logger)
    : ICommandHandler<ActivateCommercialPlanCommand, CommercialPlanResponse>
{
    public async Task<Result<CommercialPlanResponse>> Handle(
        ActivateCommercialPlanCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsurePlatformAdministrationAsync(cancellationToken);
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
            plan.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial plan {CommercialPlanCode} activated by user {UserId}.",
                plan.Code,
                currentUserService.UserId);

            return Result<CommercialPlanResponse>.Success(CommercialPlanMapper.Map(plan));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateCommercialPlanCommandHandler(
    ICommercialPlanAuthorizationService authorizationService,
    ICommercialPlanRepository repository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<InactivateCommercialPlanCommandHandler> logger)
    : ICommandHandler<InactivateCommercialPlanCommand, CommercialPlanResponse>
{
    public async Task<Result<CommercialPlanResponse>> Handle(
        InactivateCommercialPlanCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsurePlatformAdministrationAsync(cancellationToken);
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
            plan.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial plan {CommercialPlanCode} inactivated by user {UserId}.",
                plan.Code,
                currentUserService.UserId);

            return Result<CommercialPlanResponse>.Success(CommercialPlanMapper.Map(plan));
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
    public static CommercialPlanResponse Map(CommercialPlan plan) =>
        new(
            plan.PublicId,
            plan.Code,
            plan.Name,
            plan.Description,
            plan.BaseMonthlyFee,
            plan.PricePerActiveEmployee,
            plan.Status,
            plan.IsSystemPlan,
            plan.ConcurrencyToken,
            plan.CreatedUtc,
            plan.ModifiedUtc,
            plan.Limits
                .OrderBy(limit => limit.NormalizedLimitCode, StringComparer.Ordinal)
                .Select(limit => new CommercialPlanLimitResponse(limit.LimitCode, limit.Value))
                .ToArray());

    public static IReadOnlyCollection<(string LimitCode, decimal Value)> ToLimitData(
        IReadOnlyCollection<CommercialPlanLimitInput> limits) =>
        limits
            .Select(limit => (limit.Code, limit.Value))
            .ToArray();
}
