using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.CompetencyFramework;
using FluentValidation;

namespace CLARIHR.Application.Features.CompetencyFramework;

public sealed record OccupationalPyramidLevelListItemResponse(
    Guid Id,
    string Code,
    string Name,
    int LevelOrder,
    string? Description,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record OccupationalPyramidLevelResponse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    int LevelOrder,
    string? Description,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record SearchOccupationalPyramidLevelsQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = CompetencyFrameworkValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<OccupationalPyramidLevelListItemResponse>>;

public sealed record GetOccupationalPyramidLevelByIdQuery(Guid LevelId)
    : IQuery<OccupationalPyramidLevelResponse>;

public sealed record CreateOccupationalPyramidLevelCommand(
    Guid CompanyId,
    string Code,
    string Name,
    int LevelOrder,
    string? Description)
    : ICommand<OccupationalPyramidLevelResponse>;

public sealed record UpdateOccupationalPyramidLevelCommand(
    Guid LevelId,
    string Code,
    string Name,
    int LevelOrder,
    string? Description,
    Guid ConcurrencyToken)
    : ICommand<OccupationalPyramidLevelResponse>;

public sealed record ActivateOccupationalPyramidLevelCommand(Guid LevelId, Guid ConcurrencyToken)
    : ICommand<OccupationalPyramidLevelResponse>;

public sealed record InactivateOccupationalPyramidLevelCommand(Guid LevelId, Guid ConcurrencyToken)
    : ICommand<OccupationalPyramidLevelResponse>;

internal sealed class SearchOccupationalPyramidLevelsQueryValidator : AbstractValidator<SearchOccupationalPyramidLevelsQuery>
{
    public SearchOccupationalPyramidLevelsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, CompetencyFrameworkValidationRules.MaxPageSize);
    }
}

internal sealed class GetOccupationalPyramidLevelByIdQueryValidator : AbstractValidator<GetOccupationalPyramidLevelByIdQuery>
{
    public GetOccupationalPyramidLevelByIdQueryValidator()
    {
        RuleFor(query => query.LevelId).NotEmpty();
    }
}

internal sealed class CreateOccupationalPyramidLevelCommandValidator : AbstractValidator<CreateOccupationalPyramidLevelCommand>
{
    public CreateOccupationalPyramidLevelCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(OccupationalPyramidLevel.MaxCodeLength)
            .Must(CompetencyFrameworkValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(OccupationalPyramidLevel.MaxNameLength);
        RuleFor(command => command.LevelOrder).GreaterThan(0);
        RuleFor(command => command.Description).MaximumLength(OccupationalPyramidLevel.MaxDescriptionLength);
    }
}

internal sealed class UpdateOccupationalPyramidLevelCommandValidator : AbstractValidator<UpdateOccupationalPyramidLevelCommand>
{
    public UpdateOccupationalPyramidLevelCommandValidator()
    {
        RuleFor(command => command.LevelId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(OccupationalPyramidLevel.MaxCodeLength)
            .Must(CompetencyFrameworkValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(OccupationalPyramidLevel.MaxNameLength);
        RuleFor(command => command.LevelOrder).GreaterThan(0);
        RuleFor(command => command.Description).MaximumLength(OccupationalPyramidLevel.MaxDescriptionLength);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateOccupationalPyramidLevelCommandValidator : AbstractValidator<ActivateOccupationalPyramidLevelCommand>
{
    public ActivateOccupationalPyramidLevelCommandValidator()
    {
        RuleFor(command => command.LevelId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateOccupationalPyramidLevelCommandValidator : AbstractValidator<InactivateOccupationalPyramidLevelCommand>
{
    public InactivateOccupationalPyramidLevelCommandValidator()
    {
        RuleFor(command => command.LevelId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchOccupationalPyramidLevelsQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchOccupationalPyramidLevelsQuery, PagedResponse<OccupationalPyramidLevelListItemResponse>>
{
    public async Task<Result<PagedResponse<OccupationalPyramidLevelListItemResponse>>> Handle(
        SearchOccupationalPyramidLevelsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<OccupationalPyramidLevelListItemResponse>>.Failure(authorizationResult.Error);
        }

        var payload = await repository.SearchOccupationalPyramidLevelsAsync(
            query.CompanyId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<OccupationalPyramidLevelListItemResponse>>.Success(payload);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = payload.Items
            .Select(item => CompetencyFrameworkPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();

        return Result<PagedResponse<OccupationalPyramidLevelListItemResponse>>.Success(payload with { Items = items });
    }
}

internal sealed class GetOccupationalPyramidLevelByIdQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetOccupationalPyramidLevelByIdQuery, OccupationalPyramidLevelResponse>
{
    public async Task<Result<OccupationalPyramidLevelResponse>> Handle(
        GetOccupationalPyramidLevelByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetOccupationalPyramidLevelResponseByIdAsync(query.LevelId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = CompetencyFrameworkPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);
            return Result<OccupationalPyramidLevelResponse>.Success(response);
        }

        return Result<OccupationalPyramidLevelResponse>.Failure(
            await repository.OccupationalPyramidLevelExistsOutsideTenantAsync(query.LevelId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound);
    }
}

internal sealed class CreateOccupationalPyramidLevelCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateOccupationalPyramidLevelCommand, OccupationalPyramidLevelResponse>
{
    public async Task<Result<OccupationalPyramidLevelResponse>> Handle(
        CreateOccupationalPyramidLevelCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(authorizationResult.Error);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.OccupationalPyramidLevelCodeExistsAsync(command.CompanyId, normalizedCode, excludingInternalId: null, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelCodeConflict);
        }

        if (await repository.OccupationalPyramidLevelOrderExistsAsync(command.CompanyId, command.LevelOrder, excludingInternalId: null, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelOrderConflict);
        }

        var level = OccupationalPyramidLevel.Create(command.Code, command.Name, command.LevelOrder, command.Description);
        level.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddOccupationalPyramidLevel(level);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OccupationalPyramidLevelCreated,
                    AuditEntityTypes.OccupationalPyramidLevel,
                    level.PublicId,
                    level.Code,
                    AuditActions.Create,
                    $"Created occupational pyramid level {level.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OccupationalPyramidLevelResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateOccupationalPyramidLevelCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateOccupationalPyramidLevelCommand, OccupationalPyramidLevelResponse>
{
    public async Task<Result<OccupationalPyramidLevelResponse>> Handle(
        UpdateOccupationalPyramidLevelCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(authorizationResult.Error);
        }

        var level = await repository.GetOccupationalPyramidLevelByIdAsync(command.LevelId, cancellationToken);
        if (level is null)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(
                await repository.OccupationalPyramidLevelExistsOutsideTenantAsync(command.LevelId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound);
        }

        if (level.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.OccupationalPyramidLevelCodeExistsAsync(level.TenantId, normalizedCode, level.Id, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelCodeConflict);
        }

        if (await repository.OccupationalPyramidLevelOrderExistsAsync(level.TenantId, command.LevelOrder, level.Id, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelOrderConflict);
        }

        var before = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            level.Update(command.Code, command.Name, command.LevelOrder, command.Description);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OccupationalPyramidLevelUpdated,
                    AuditEntityTypes.OccupationalPyramidLevel,
                    level.PublicId,
                    level.Code,
                    AuditActions.Update,
                    $"Updated occupational pyramid level {level.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OccupationalPyramidLevelResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateOccupationalPyramidLevelCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateOccupationalPyramidLevelCommand, OccupationalPyramidLevelResponse>
{
    public async Task<Result<OccupationalPyramidLevelResponse>> Handle(
        ActivateOccupationalPyramidLevelCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(authorizationResult.Error);
        }

        var level = await repository.GetOccupationalPyramidLevelByIdAsync(command.LevelId, cancellationToken);
        if (level is null)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(
                await repository.OccupationalPyramidLevelExistsOutsideTenantAsync(command.LevelId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound);
        }

        if (level.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var before = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            level.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OccupationalPyramidLevelActivated,
                    AuditEntityTypes.OccupationalPyramidLevel,
                    level.PublicId,
                    level.Code,
                    AuditActions.Reactivate,
                    $"Activated occupational pyramid level {level.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OccupationalPyramidLevelResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateOccupationalPyramidLevelCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateOccupationalPyramidLevelCommand, OccupationalPyramidLevelResponse>
{
    public async Task<Result<OccupationalPyramidLevelResponse>> Handle(
        InactivateOccupationalPyramidLevelCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(authorizationResult.Error);
        }

        var level = await repository.GetOccupationalPyramidLevelByIdAsync(command.LevelId, cancellationToken);
        if (level is null)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(
                await repository.OccupationalPyramidLevelExistsOutsideTenantAsync(command.LevelId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound);
        }

        if (level.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        if (await repository.OccupationalPyramidLevelHasActiveUsageAsync(level.Id, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelInUse);
        }

        var before = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            level.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OccupationalPyramidLevelInactivated,
                    AuditEntityTypes.OccupationalPyramidLevel,
                    level.PublicId,
                    level.Code,
                    AuditActions.Deactivate,
                    $"Inactivated occupational pyramid level {level.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OccupationalPyramidLevelResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
