using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Domain.Locations;
using FluentValidation;

namespace CLARIHR.Application.Features.Locations.Hierarchy;

public sealed record LocationHierarchyConfigResponse(
    Guid Id,
    bool IsMultiLevel,
    string DefaultGroupCode,
    string DefaultGroupName,
    Guid ConcurrencyToken);

public sealed record LocationLevelResponse(
    Guid Id,
    int LevelOrder,
    string DisplayName,
    bool IsActive,
    bool IsRequired,
    bool AllowsWorkCenters,
    Guid ConcurrencyToken);

public sealed record GetLocationHierarchyQuery(Guid CompanyId) : IQuery<LocationHierarchyConfigResponse>;

public sealed record UpdateLocationHierarchyConfigCommand(
    Guid CompanyId,
    bool IsMultiLevel,
    Guid ConcurrencyToken) : ICommand<LocationHierarchyConfigResponse>;

public sealed record GetLocationLevelsQuery(Guid CompanyId) : IQuery<IReadOnlyCollection<LocationLevelResponse>>;

public sealed record GetLocationLevelByIdQuery(Guid LevelId) : IQuery<LocationLevelResponse>;

public sealed record CreateLocationLevelCommand(
    Guid CompanyId,
    int LevelOrder,
    string DisplayName,
    bool IsActive,
    bool IsRequired,
    bool AllowsWorkCenters) : ICommand<LocationLevelResponse>;

public sealed record UpdateLocationLevelCommand(
    Guid LevelId,
    string DisplayName,
    bool IsActive,
    bool IsRequired,
    bool AllowsWorkCenters,
    Guid ConcurrencyToken) : ICommand<LocationLevelResponse>;

public sealed record ActivateLocationLevelCommand(Guid LevelId, Guid ConcurrencyToken) : ICommand<LocationLevelResponse>;

public sealed record InactivateLocationLevelCommand(Guid LevelId, Guid ConcurrencyToken) : ICommand<LocationLevelResponse>;

internal sealed class GetLocationHierarchyQueryValidator : AbstractValidator<GetLocationHierarchyQuery>
{
    public GetLocationHierarchyQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class UpdateLocationHierarchyConfigCommandValidator : AbstractValidator<UpdateLocationHierarchyConfigCommand>
{
    public UpdateLocationHierarchyConfigCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetLocationLevelsQueryValidator : AbstractValidator<GetLocationLevelsQuery>
{
    public GetLocationLevelsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class GetLocationLevelByIdQueryValidator : AbstractValidator<GetLocationLevelByIdQuery>
{
    public GetLocationLevelByIdQueryValidator()
    {
        RuleFor(query => query.LevelId).NotEmpty();
    }
}

internal sealed class CreateLocationLevelCommandValidator : AbstractValidator<CreateLocationLevelCommand>
{
    public CreateLocationLevelCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.LevelOrder).GreaterThan(0);
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(command => command)
            .Must(static command => !command.IsRequired || command.IsActive)
            .WithMessage("Required levels must be active.");
        RuleFor(command => command)
            .Must(static command => !command.AllowsWorkCenters || command.IsActive)
            .WithMessage("Levels that allow work centers must be active.");
    }
}

internal sealed class UpdateLocationLevelCommandValidator : AbstractValidator<UpdateLocationLevelCommand>
{
    public UpdateLocationLevelCommandValidator()
    {
        RuleFor(command => command.LevelId).NotEmpty();
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command)
            .Must(static command => !command.IsRequired || command.IsActive)
            .WithMessage("Required levels must be active.");
        RuleFor(command => command)
            .Must(static command => !command.AllowsWorkCenters || command.IsActive)
            .WithMessage("Levels that allow work centers must be active.");
    }
}

internal sealed class ActivateLocationLevelCommandValidator : AbstractValidator<ActivateLocationLevelCommand>
{
    public ActivateLocationLevelCommandValidator()
    {
        RuleFor(command => command.LevelId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateLocationLevelCommandValidator : AbstractValidator<InactivateLocationLevelCommand>
{
    public InactivateLocationLevelCommandValidator()
    {
        RuleFor(command => command.LevelId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetLocationHierarchyQueryHandler(
    ILocationAuthorizationService authorizationService,
    ILocationHierarchyRepository repository)
    : IQueryHandler<GetLocationHierarchyQuery, LocationHierarchyConfigResponse>
{
    public async Task<Result<LocationHierarchyConfigResponse>> Handle(
        GetLocationHierarchyQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationHierarchyConfigResponse>.Failure(authorizationResult.Error);
        }

        var config = await repository.GetConfigAsync(query.CompanyId, cancellationToken);
        return config is null
            ? Result<LocationHierarchyConfigResponse>.Failure(LocationErrors.HierarchyNotFound)
            : Result<LocationHierarchyConfigResponse>.Success(LocationHierarchyMapper.Map(config));
    }
}

internal sealed class UpdateLocationHierarchyConfigCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationHierarchyRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateLocationHierarchyConfigCommand, LocationHierarchyConfigResponse>
{
    public async Task<Result<LocationHierarchyConfigResponse>> Handle(
        UpdateLocationHierarchyConfigCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationHierarchyConfigResponse>.Failure(authorizationResult.Error);
        }

        var config = await repository.GetConfigAsync(command.CompanyId, cancellationToken);
        if (config is null)
        {
            return Result<LocationHierarchyConfigResponse>.Failure(LocationErrors.HierarchyNotFound);
        }

        if (config.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LocationHierarchyConfigResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        var levels = await repository.GetLevelsAsync(command.CompanyId, cancellationToken);
        if (!command.IsMultiLevel && levels.Count(static level => level.IsActive) != 1)
        {
            return Result<LocationHierarchyConfigResponse>.Failure(LocationErrors.SingleLevelRequiresOneActiveLevel);
        }

        var before = LocationHierarchyMapper.Map(config);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            config.Update(command.IsMultiLevel);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = LocationHierarchyMapper.Map(config);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LocationHierarchyUpdated,
                    AuditEntityTypes.LocationHierarchy,
                    config.PublicId,
                    LocationPermissionCodes.ResourceKey,
                    AuditActions.Update,
                    $"Updated location hierarchy for tenant {command.CompanyId}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationHierarchyConfigResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetLocationLevelsQueryHandler(
    ILocationAuthorizationService authorizationService,
    ILocationHierarchyRepository repository)
    : IQueryHandler<GetLocationLevelsQuery, IReadOnlyCollection<LocationLevelResponse>>
{
    public async Task<Result<IReadOnlyCollection<LocationLevelResponse>>> Handle(
        GetLocationLevelsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<LocationLevelResponse>>.Failure(authorizationResult.Error);
        }

        // GetLevelsAsync already orders by LevelOrder in SQL (uq/ix on tenant+order); no in-memory re-sort.
        var levels = await repository.GetLevelsAsync(query.CompanyId, cancellationToken);
        var response = levels
            .Select(LocationHierarchyMapper.Map)
            .ToArray();

        return Result<IReadOnlyCollection<LocationLevelResponse>>.Success(response);
    }
}

internal sealed class CreateLocationLevelCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationHierarchyRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateLocationLevelCommand, LocationLevelResponse>
{
    public async Task<Result<LocationLevelResponse>> Handle(
        CreateLocationLevelCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationLevelResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.GetConfigAsync(command.CompanyId, cancellationToken) is null)
        {
            return Result<LocationLevelResponse>.Failure(LocationErrors.HierarchyNotFound);
        }

        if (await repository.LevelOrderExistsAsync(command.CompanyId, command.LevelOrder, excludingLevelId: null, cancellationToken))
        {
            return Result<LocationLevelResponse>.Failure(LocationErrors.LevelOrderConflict);
        }

        // The required-must-stay-active flag combination is rejected with 400 by CreateLocationLevelCommandValidator
        // (RequestDispatcher runs validators first), and LocationLevel.Create throws as a final domain backstop — so
        // a handler re-check here would be dead code returning a misleading 409. Kept out deliberately (OBS-1, doc 14).
        var highestActiveLevelOrder = await repository.GetHighestActiveLevelOrderAsync(command.CompanyId, excludingLevelId: null, cancellationToken);
        if (command.AllowsWorkCenters)
        {
            if (await repository.HasAnyActiveWorkCenterLevelAsync(command.CompanyId, excludingLevelId: null, cancellationToken) ||
                (highestActiveLevelOrder.HasValue && command.LevelOrder <= highestActiveLevelOrder.Value))
            {
                return Result<LocationLevelResponse>.Failure(LocationErrors.WorkCentersAllowedOnlyOnLastLevel);
            }
        }

        var level = LocationLevel.Create(
            command.LevelOrder,
            command.DisplayName,
            command.IsActive,
            command.IsRequired,
            command.AllowsWorkCenters);
        level.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddLevel(level);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = LocationHierarchyMapper.Map(level);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LocationLevelCreated,
                    AuditEntityTypes.LocationLevel,
                    level.PublicId,
                    level.LevelOrder.ToString(),
                    AuditActions.Create,
                    $"Created location level {level.DisplayName}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationLevelResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (LocationConstraintViolations.IsLevelOrderConflict(ex.ConstraintName))
        {
            // Concurrent creates with the same level order both pass LevelOrderExistsAsync; the second trips
            // the (TenantId, LevelOrder) unique index → same clean 409 as the probe (mirrors CostCenters R2).
            await transaction.RollbackAsync(cancellationToken);
            return Result<LocationLevelResponse>.Failure(LocationErrors.LevelOrderConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateLocationLevelCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationHierarchyRepository repository,
    ILocationGroupRepository groupRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateLocationLevelCommand, LocationLevelResponse>
{
    public async Task<Result<LocationLevelResponse>> Handle(
        UpdateLocationLevelCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LocationLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationLevelResponse>.Failure(authorizationResult.Error);
        }

        var level = await repository.GetLevelByIdAsync(command.LevelId, cancellationToken);
        if (level is null)
        {
            return Result<LocationLevelResponse>.Failure(
                await repository.LevelExistsOutsideTenantAsync(command.LevelId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.LevelNotFound);
        }

        if (level.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LocationLevelResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        var levels = await repository.GetLevelsAsync(level.TenantId, cancellationToken);
        var otherActiveLevels = levels.Where(other => other.Id != level.Id && other.IsActive).ToArray();

        // The required-must-stay-active flag combination is rejected with 400 by UpdateLocationLevelCommandValidator
        // (RequestDispatcher runs validators first), and LocationLevel.Update throws as a final domain backstop — so
        // a handler re-check here would be dead code returning a misleading 409. Kept out deliberately (OBS-1, doc 14).
        if (!command.IsActive && otherActiveLevels.Length == 0)
        {
            return Result<LocationLevelResponse>.Failure(LocationErrors.LastActiveLevelRequired);
        }

        if (!command.IsActive && await groupRepository.HasActiveGroupsAtLevelAsync(level.TenantId, level.LevelOrder, cancellationToken))
        {
            return Result<LocationLevelResponse>.Failure(LocationErrors.LocationLevelHasActiveGroups);
        }

        if (command.AllowsWorkCenters)
        {
            var highestOtherActiveLevelOrder = otherActiveLevels.Length == 0
                ? (int?)null
                : otherActiveLevels.Max(other => other.LevelOrder);
            if (otherActiveLevels.Any(other => other.AllowsWorkCenters) ||
                (highestOtherActiveLevelOrder.HasValue && highestOtherActiveLevelOrder.Value > level.LevelOrder))
            {
                return Result<LocationLevelResponse>.Failure(LocationErrors.WorkCentersAllowedOnlyOnLastLevel);
            }
        }
        else if (level.AllowsWorkCenters && await groupRepository.HasActiveGroupsAtLevelAsync(level.TenantId, level.LevelOrder, cancellationToken))
        {
            return Result<LocationLevelResponse>.Failure(LocationErrors.LocationLevelAllowsWorkCentersInUse);
        }

        var before = LocationHierarchyMapper.Map(level);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            level.Update(command.DisplayName, command.IsActive, command.IsRequired, command.AllowsWorkCenters);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = LocationHierarchyMapper.Map(level);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LocationLevelUpdated,
                    AuditEntityTypes.LocationLevel,
                    level.PublicId,
                    level.LevelOrder.ToString(),
                    AuditActions.Update,
                    $"Updated location level {level.DisplayName}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationLevelResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateLocationLevelCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationHierarchyRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateLocationLevelCommand, LocationLevelResponse>
{
    public async Task<Result<LocationLevelResponse>> Handle(
        ActivateLocationLevelCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LocationLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationLevelResponse>.Failure(authorizationResult.Error);
        }

        var level = await repository.GetLevelByIdAsync(command.LevelId, cancellationToken);
        if (level is null)
        {
            return Result<LocationLevelResponse>.Failure(
                await repository.LevelExistsOutsideTenantAsync(command.LevelId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.LevelNotFound);
        }

        if (level.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LocationLevelResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        if (level.AllowsWorkCenters)
        {
            var highestActiveLevelOrder = await repository.GetHighestActiveLevelOrderAsync(level.TenantId, excludingLevelId: level.Id, cancellationToken);
            if (highestActiveLevelOrder.HasValue && highestActiveLevelOrder.Value > level.LevelOrder)
            {
                return Result<LocationLevelResponse>.Failure(LocationErrors.WorkCentersAllowedOnlyOnLastLevel);
            }
        }

        var before = LocationHierarchyMapper.Map(level);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            level.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = LocationHierarchyMapper.Map(level);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LocationLevelActivated,
                    AuditEntityTypes.LocationLevel,
                    level.PublicId,
                    level.LevelOrder.ToString(),
                    AuditActions.Reactivate,
                    $"Activated location level {level.DisplayName}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationLevelResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateLocationLevelCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationHierarchyRepository repository,
    ILocationGroupRepository groupRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateLocationLevelCommand, LocationLevelResponse>
{
    public async Task<Result<LocationLevelResponse>> Handle(
        InactivateLocationLevelCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LocationLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationLevelResponse>.Failure(authorizationResult.Error);
        }

        var level = await repository.GetLevelByIdAsync(command.LevelId, cancellationToken);
        if (level is null)
        {
            return Result<LocationLevelResponse>.Failure(
                await repository.LevelExistsOutsideTenantAsync(command.LevelId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LocationErrors.LevelNotFound);
        }

        if (level.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LocationLevelResponse>.Failure(LocationErrors.ConcurrencyConflict);
        }

        if (level.IsRequired)
        {
            return Result<LocationLevelResponse>.Failure(LocationErrors.RequiredLevelMustRemainActive);
        }

        if (await repository.CountActiveLevelsAsync(level.TenantId, excludingLevelId: level.Id, cancellationToken) == 0)
        {
            return Result<LocationLevelResponse>.Failure(LocationErrors.LastActiveLevelRequired);
        }

        if (await groupRepository.HasActiveGroupsAtLevelAsync(level.TenantId, level.LevelOrder, cancellationToken))
        {
            return Result<LocationLevelResponse>.Failure(LocationErrors.LocationLevelHasActiveGroups);
        }

        var before = LocationHierarchyMapper.Map(level);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            level.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = LocationHierarchyMapper.Map(level);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LocationLevelInactivated,
                    AuditEntityTypes.LocationLevel,
                    level.PublicId,
                    level.LevelOrder.ToString(),
                    AuditActions.Deactivate,
                    $"Inactivated location level {level.DisplayName}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationLevelResponse>.Success(after);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<LocationLevelResponse>.Failure(LocationErrors.RequiredLevelMustRemainActive);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetLocationLevelByIdQueryHandler(
    ILocationAuthorizationService authorizationService,
    ILocationHierarchyRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetLocationLevelByIdQuery, LocationLevelResponse>
{
    public async Task<Result<LocationLevelResponse>> Handle(
        GetLocationLevelByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LocationLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationLevelResponse>.Failure(authorizationResult.Error);
        }

        var level = await repository.GetLevelByIdAsync(query.LevelId, cancellationToken);
        if (level is not null)
        {
            return Result<LocationLevelResponse>.Success(LocationHierarchyMapper.Map(level));
        }

        return Result<LocationLevelResponse>.Failure(
            await repository.LevelExistsOutsideTenantAsync(query.LevelId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : LocationErrors.LevelNotFound);
    }
}

internal static class LocationHierarchyMapper
{
    public static LocationHierarchyConfigResponse Map(LocationHierarchyConfig config) =>
        new(
            config.PublicId,
            config.IsMultiLevel,
            config.DefaultGroupCode,
            config.DefaultGroupName,
            config.ConcurrencyToken);

    public static LocationLevelResponse Map(LocationLevel level) =>
        new(
            level.PublicId,
            level.LevelOrder,
            level.DisplayName,
            level.IsActive,
            level.IsRequired,
            level.AllowsWorkCenters,
            level.ConcurrencyToken);
}
