using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CommercialAddons.Common;
using CLARIHR.Domain.Companies;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.CommercialAddons;

internal sealed class SearchCommercialAddonsQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialAddonRepository repository)
    : IQueryHandler<SearchCommercialAddonsQuery, PagedResponse<CommercialAddonSummaryResponse>>
{
    public async Task<Result<PagedResponse<CommercialAddonSummaryResponse>>> Handle(
        SearchCommercialAddonsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<CommercialAddonSummaryResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.Status,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<CommercialAddonSummaryResponse>>.Success(response);
    }
}

internal sealed class GetCommercialAddonByIdQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialAddonRepository repository)
    : IQueryHandler<GetCommercialAddonByIdQuery, CommercialAddonResponse>
{
    public async Task<Result<CommercialAddonResponse>> Handle(
        GetCommercialAddonByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CommercialAddonResponse>.Failure(authorizationResult.Error);
        }

        var addon = await repository.GetByIdAsync(query.CommercialAddonId, cancellationToken);
        return addon is null
            ? Result<CommercialAddonResponse>.Failure(CommercialAddonErrors.NotFound)
            : Result<CommercialAddonResponse>.Success(CommercialAddonMapper.Map(addon));
    }
}

internal sealed class CreateCommercialAddonCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialAddonRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<CreateCommercialAddonCommandHandler> logger)
    : ICommandHandler<CreateCommercialAddonCommand, CommercialAddonResponse>
{
    public async Task<Result<CommercialAddonResponse>> Handle(
        CreateCommercialAddonCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CommercialAddonResponse>.Failure(authorizationResult.Error);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(normalizedCode, excludingId: null, cancellationToken))
        {
            return Result<CommercialAddonResponse>.Failure(CommercialAddonErrors.CodeConflict);
        }

        var addon = CommercialAddon.Create(
            command.Code,
            command.Name,
            command.Description,
            command.Type,
            command.PricePerActiveEmployee,
            command.MinimumMonthlyFee,
            command.Periodicity,
            command.Status);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(addon);
            var response = CommercialAddonMapper.Map(addon);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CommercialAddonCreated,
                    AuditEntityTypes.CommercialAddon,
                    addon.PublicId,
                    addon.Code,
                    AuditActions.Create,
                    $"Created commercial addon {addon.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial addon {CommercialAddonCode} created by user {UserId}.",
                addon.Code,
                currentUserService.UserId);

            return Result<CommercialAddonResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCommercialAddonCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialAddonRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<UpdateCommercialAddonCommandHandler> logger)
    : ICommandHandler<UpdateCommercialAddonCommand, CommercialAddonResponse>
{
    public async Task<Result<CommercialAddonResponse>> Handle(
        UpdateCommercialAddonCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CommercialAddonResponse>.Failure(authorizationResult.Error);
        }

        var addon = await repository.GetByIdAsync(command.CommercialAddonId, cancellationToken);
        if (addon is null)
        {
            return Result<CommercialAddonResponse>.Failure(CommercialAddonErrors.NotFound);
        }

        if (addon.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CommercialAddonResponse>.Failure(CommercialAddonErrors.ConcurrencyConflict);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(normalizedCode, addon.Id, cancellationToken))
        {
            return Result<CommercialAddonResponse>.Failure(CommercialAddonErrors.CodeConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = CommercialAddonMapper.Map(addon);
            addon.Update(
                command.Code,
                command.Name,
                command.Description,
                command.Type,
                command.PricePerActiveEmployee,
                command.MinimumMonthlyFee,
                command.Periodicity);

            var after = CommercialAddonMapper.Map(addon);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CommercialAddonUpdated,
                    AuditEntityTypes.CommercialAddon,
                    addon.PublicId,
                    addon.Code,
                    AuditActions.Update,
                    $"Updated commercial addon {addon.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial addon {CommercialAddonCode} updated by user {UserId}.",
                addon.Code,
                currentUserService.UserId);

            return Result<CommercialAddonResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateCommercialAddonCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialAddonRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<ActivateCommercialAddonCommandHandler> logger)
    : ICommandHandler<ActivateCommercialAddonCommand, CommercialAddonResponse>
{
    public async Task<Result<CommercialAddonResponse>> Handle(
        ActivateCommercialAddonCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CommercialAddonResponse>.Failure(authorizationResult.Error);
        }

        var addon = await repository.GetByIdAsync(command.CommercialAddonId, cancellationToken);
        if (addon is null)
        {
            return Result<CommercialAddonResponse>.Failure(CommercialAddonErrors.NotFound);
        }

        if (addon.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CommercialAddonResponse>.Failure(CommercialAddonErrors.ConcurrencyConflict);
        }

        if (addon.Status == CommercialAddonStatus.Active)
        {
            return Result<CommercialAddonResponse>.Failure(CommercialAddonErrors.AlreadyActive);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = CommercialAddonMapper.Map(addon);
            addon.Activate();
            var after = CommercialAddonMapper.Map(addon);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CommercialAddonActivated,
                    AuditEntityTypes.CommercialAddon,
                    addon.PublicId,
                    addon.Code,
                    AuditActions.Reactivate,
                    $"Activated commercial addon {addon.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial addon {CommercialAddonCode} activated by user {UserId}.",
                addon.Code,
                currentUserService.UserId);

            return Result<CommercialAddonResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateCommercialAddonCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICommercialAddonRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<InactivateCommercialAddonCommandHandler> logger)
    : ICommandHandler<InactivateCommercialAddonCommand, CommercialAddonResponse>
{
    public async Task<Result<CommercialAddonResponse>> Handle(
        InactivateCommercialAddonCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CommercialAddonResponse>.Failure(authorizationResult.Error);
        }

        var addon = await repository.GetByIdAsync(command.CommercialAddonId, cancellationToken);
        if (addon is null)
        {
            return Result<CommercialAddonResponse>.Failure(CommercialAddonErrors.NotFound);
        }

        if (addon.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CommercialAddonResponse>.Failure(CommercialAddonErrors.ConcurrencyConflict);
        }

        if (addon.Status == CommercialAddonStatus.Inactive)
        {
            return Result<CommercialAddonResponse>.Failure(CommercialAddonErrors.AlreadyInactive);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = CommercialAddonMapper.Map(addon);
            addon.Inactivate();
            var after = CommercialAddonMapper.Map(addon);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CommercialAddonInactivated,
                    AuditEntityTypes.CommercialAddon,
                    addon.PublicId,
                    addon.Code,
                    AuditActions.Deactivate,
                    $"Inactivated commercial addon {addon.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Commercial addon {CommercialAddonCode} inactivated by user {UserId}.",
                addon.Code,
                currentUserService.UserId);

            return Result<CommercialAddonResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class CommercialAddonMapper
{
    public static CommercialAddonResponse Map(CommercialAddon addon) =>
        new(
            addon.PublicId,
            addon.Code,
            addon.Name,
            addon.Description,
            addon.Type,
            addon.PricePerActiveEmployee,
            addon.MinimumMonthlyFee,
            addon.Periodicity,
            addon.Status,
            addon.ConcurrencyToken,
            addon.CreatedUtc,
            addon.ModifiedUtc);
}
