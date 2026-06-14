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
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.CompetencyFramework;

public sealed record CompetencyConductBehaviorResponse(
    Guid BehaviorId,
    string BehaviorCode,
    string BehaviorName,
    string? Notes,
    int SortOrder);

public sealed record CompetencyConductListItemResponse(
    Guid Id,
    Guid CompanyId,
    Guid CompetencyId,
    string CompetencyCode,
    string CompetencyName,
    Guid CompetencyTypeId,
    string CompetencyTypeCode,
    string CompetencyTypeName,
    Guid BehaviorLevelId,
    string BehaviorLevelCode,
    string BehaviorLevelName,
    string Description,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions, IHasActivationState;

public sealed record CompetencyConductResponse(
    Guid Id,
    Guid CompanyId,
    Guid CompetencyId,
    string CompetencyCode,
    string CompetencyName,
    Guid CompetencyTypeId,
    string CompetencyTypeCode,
    string CompetencyTypeName,
    Guid BehaviorLevelId,
    string BehaviorLevelCode,
    string BehaviorLevelName,
    string Description,
    int SortOrder,
    bool IsActive,
    IReadOnlyCollection<CompetencyConductBehaviorResponse> Behaviors,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions, IHasActivationState;

public sealed record CompetencyConductBehaviorInput(
    Guid BehaviorId,
    string? Notes,
    int SortOrder);

public sealed record SearchCompetencyConductsQuery(
    Guid CompanyId,
    Guid? CompetencyId,
    Guid? CompetencyTypeId,
    Guid? BehaviorLevelId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = CompetencyFrameworkValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<CompetencyConductListItemResponse>>;

public sealed record GetCompetencyConductByIdQuery(Guid ConductId)
    : IQuery<CompetencyConductResponse>;

public sealed record CreateCompetencyConductCommand(
    Guid CompanyId,
    Guid CompetencyId,
    Guid CompetencyTypeId,
    Guid BehaviorLevelId,
    string Description,
    int SortOrder)
    : ICommand<CompetencyConductResponse>;

public sealed record UpdateCompetencyConductCommand(
    Guid ConductId,
    Guid CompetencyId,
    Guid CompetencyTypeId,
    Guid BehaviorLevelId,
    string Description,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<CompetencyConductResponse>;

public sealed record ActivateCompetencyConductCommand(Guid ConductId, Guid ConcurrencyToken)
    : ICommand<CompetencyConductResponse>;

public sealed record InactivateCompetencyConductCommand(Guid ConductId, Guid ConcurrencyToken)
    : ICommand<CompetencyConductResponse>;

public sealed record UpdateCompetencyConductBehaviorsCommand(
    Guid ConductId,
    IReadOnlyCollection<CompetencyConductBehaviorInput> Behaviors,
    Guid ConcurrencyToken)
    : ICommand<CompetencyConductResponse>;

internal sealed class SearchCompetencyConductsQueryValidator : AbstractValidator<SearchCompetencyConductsQuery>
{
    public SearchCompetencyConductsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.CompetencyId).NotEqual(Guid.Empty).When(static query => query.CompetencyId.HasValue);
        RuleFor(query => query.CompetencyTypeId).NotEqual(Guid.Empty).When(static query => query.CompetencyTypeId.HasValue);
        RuleFor(query => query.BehaviorLevelId).NotEqual(Guid.Empty).When(static query => query.BehaviorLevelId.HasValue);
        RuleFor(query => query)
            .Must(static query =>
            {
                var selectedFilters = 0;
                selectedFilters += query.CompetencyId.HasValue ? 1 : 0;
                selectedFilters += query.CompetencyTypeId.HasValue ? 1 : 0;
                selectedFilters += query.BehaviorLevelId.HasValue ? 1 : 0;

                return selectedFilters is 0 or 3;
            })
            .WithMessage("CompetencyId, CompetencyTypeId and BehaviorLevelId must be provided together.");
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, CompetencyFrameworkValidationRules.MaxPageSize);
    }
}

internal sealed class GetCompetencyConductByIdQueryValidator : AbstractValidator<GetCompetencyConductByIdQuery>
{
    public GetCompetencyConductByIdQueryValidator()
    {
        RuleFor(query => query.ConductId).NotEmpty();
    }
}

internal sealed class CreateCompetencyConductCommandValidator : AbstractValidator<CreateCompetencyConductCommand>
{
    public CreateCompetencyConductCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.CompetencyId).NotEmpty();
        RuleFor(command => command.CompetencyTypeId).NotEmpty();
        RuleFor(command => command.BehaviorLevelId).NotEmpty();
        RuleFor(command => command.Description).NotEmpty().MaximumLength(CompetencyConduct.MaxDescriptionLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateCompetencyConductCommandValidator : AbstractValidator<UpdateCompetencyConductCommand>
{
    public UpdateCompetencyConductCommandValidator()
    {
        RuleFor(command => command.ConductId).NotEmpty();
        RuleFor(command => command.CompetencyId).NotEmpty();
        RuleFor(command => command.CompetencyTypeId).NotEmpty();
        RuleFor(command => command.BehaviorLevelId).NotEmpty();
        RuleFor(command => command.Description).NotEmpty().MaximumLength(CompetencyConduct.MaxDescriptionLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateCompetencyConductCommandValidator : AbstractValidator<ActivateCompetencyConductCommand>
{
    public ActivateCompetencyConductCommandValidator()
    {
        RuleFor(command => command.ConductId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateCompetencyConductCommandValidator : AbstractValidator<InactivateCompetencyConductCommand>
{
    public InactivateCompetencyConductCommandValidator()
    {
        RuleFor(command => command.ConductId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class UpdateCompetencyConductBehaviorsCommandValidator : AbstractValidator<UpdateCompetencyConductBehaviorsCommand>
{
    public UpdateCompetencyConductBehaviorsCommandValidator()
    {
        RuleFor(command => command.ConductId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Behaviors)
            .Must(static behaviors => behaviors is null || behaviors.Count <= CompetencyFrameworkValidationRules.MaxBehaviorsPerConduct)
            .WithMessage("A maximum of 50 behaviors per competency conduct is allowed.");
        RuleForEach(command => command.Behaviors).SetValidator(new CompetencyConductBehaviorInputValidator());
    }
}

internal sealed class CompetencyConductBehaviorInputValidator : AbstractValidator<CompetencyConductBehaviorInput>
{
    public CompetencyConductBehaviorInputValidator()
    {
        RuleFor(input => input.BehaviorId).NotEmpty();
        RuleFor(input => input.Notes).MaximumLength(1000);
        RuleFor(input => input.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class SearchCompetencyConductsQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchCompetencyConductsQuery, PagedResponse<CompetencyConductListItemResponse>>
{
    public async Task<Result<PagedResponse<CompetencyConductListItemResponse>>> Handle(
        SearchCompetencyConductsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<CompetencyConductListItemResponse>>.Failure(authorizationResult.Error);
        }

        var payload = await repository.SearchCompetencyConductsAsync(
            query.CompanyId,
            query.CompetencyId,
            query.CompetencyTypeId,
            query.BehaviorLevelId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<CompetencyConductListItemResponse>>.Success(payload);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = payload.Items
            .Select(item => CompetencyFrameworkPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();

        return Result<PagedResponse<CompetencyConductListItemResponse>>.Success(payload with { Items = items });
    }
}

internal sealed class GetCompetencyConductByIdQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetCompetencyConductByIdQuery, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        GetCompetencyConductByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompetencyConductResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetCompetencyConductResponseByIdAsync(query.ConductId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = CompetencyFrameworkPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);
            return Result<CompetencyConductResponse>.Success(response);
        }

        return Result<CompetencyConductResponse>.Failure(
            await repository.CompetencyConductExistsOutsideTenantAsync(query.ConductId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CompetencyFrameworkErrors.CompetencyConductNotFound);
    }
}

internal sealed class CreateCompetencyConductCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCompetencyConductCommand, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        CreateCompetencyConductCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var competencyResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            command.CompanyId,
            command.CompetencyId,
            JobCatalogCategory.Competency,
            repository,
            authorizationService,
            RbacPermissionAction.Create,
            cancellationToken);
        if (competencyResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(competencyResolution.Error);
        }

        var typeResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            command.CompanyId,
            command.CompetencyTypeId,
            JobCatalogCategory.CompetencyType,
            repository,
            authorizationService,
            RbacPermissionAction.Create,
            cancellationToken);
        if (typeResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(typeResolution.Error);
        }

        var levelResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            command.CompanyId,
            command.BehaviorLevelId,
            JobCatalogCategory.BehaviorLevel,
            repository,
            authorizationService,
            RbacPermissionAction.Create,
            cancellationToken);
        if (levelResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(levelResolution.Error);
        }

        var normalizedDescription = command.Description.Trim().ToUpperInvariant();
        if (await repository.CompetencyConductDuplicateExistsAsync(
                command.CompanyId,
                competencyResolution.Value.Id,
                typeResolution.Value.Id,
                levelResolution.Value.Id,
                normalizedDescription,
                excludingInternalId: null,
                cancellationToken))
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.CompetencyConductDuplicate);
        }

        var conduct = CompetencyConduct.Create(
            competencyResolution.Value.Id,
            typeResolution.Value.Id,
            levelResolution.Value.Id,
            command.Description,
            command.SortOrder);
        conduct.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddCompetencyConduct(conduct);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Competency conduct response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyConductCreated,
                    AuditEntityTypes.CompetencyConduct,
                    conduct.PublicId,
                    conduct.Description,
                    AuditActions.Create,
                    $"Created competency conduct {conduct.Description}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyConductResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCompetencyConductCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompetencyConductCommand, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        UpdateCompetencyConductCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompetencyConductResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var conduct = await repository.GetCompetencyConductByIdAsync(command.ConductId, includeBehaviors: true, cancellationToken);
        if (conduct is null)
        {
            return Result<CompetencyConductResponse>.Failure(
                await repository.CompetencyConductExistsOutsideTenantAsync(command.ConductId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.CompetencyConductNotFound);
        }

        if (conduct.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var competencyResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            conduct.TenantId,
            command.CompetencyId,
            JobCatalogCategory.Competency,
            repository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (competencyResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(competencyResolution.Error);
        }

        var typeResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            conduct.TenantId,
            command.CompetencyTypeId,
            JobCatalogCategory.CompetencyType,
            repository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (typeResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(typeResolution.Error);
        }

        var levelResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            conduct.TenantId,
            command.BehaviorLevelId,
            JobCatalogCategory.BehaviorLevel,
            repository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (levelResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(levelResolution.Error);
        }

        var normalizedDescription = command.Description.Trim().ToUpperInvariant();
        if (await repository.CompetencyConductDuplicateExistsAsync(
                conduct.TenantId,
                competencyResolution.Value.Id,
                typeResolution.Value.Id,
                levelResolution.Value.Id,
                normalizedDescription,
                conduct.Id,
                cancellationToken))
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.CompetencyConductDuplicate);
        }

        var before = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Competency conduct response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            conduct.Update(
                competencyResolution.Value.Id,
                typeResolution.Value.Id,
                levelResolution.Value.Id,
                command.Description,
                command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Competency conduct response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyConductUpdated,
                    AuditEntityTypes.CompetencyConduct,
                    conduct.PublicId,
                    conduct.Description,
                    AuditActions.Update,
                    $"Updated competency conduct {conduct.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyConductResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateCompetencyConductCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateCompetencyConductCommand, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        ActivateCompetencyConductCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompetencyConductResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var conduct = await repository.GetCompetencyConductByIdAsync(command.ConductId, includeBehaviors: true, cancellationToken);
        if (conduct is null)
        {
            return Result<CompetencyConductResponse>.Failure(
                await repository.CompetencyConductExistsOutsideTenantAsync(command.ConductId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.CompetencyConductNotFound);
        }

        if (conduct.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var before = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Competency conduct response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            conduct.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Competency conduct response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyConductActivated,
                    AuditEntityTypes.CompetencyConduct,
                    conduct.PublicId,
                    conduct.Description,
                    AuditActions.Reactivate,
                    $"Activated competency conduct {conduct.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyConductResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateCompetencyConductCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateCompetencyConductCommand, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        InactivateCompetencyConductCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompetencyConductResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var conduct = await repository.GetCompetencyConductByIdAsync(command.ConductId, includeBehaviors: true, cancellationToken);
        if (conduct is null)
        {
            return Result<CompetencyConductResponse>.Failure(
                await repository.CompetencyConductExistsOutsideTenantAsync(command.ConductId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.CompetencyConductNotFound);
        }

        if (conduct.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        if (await repository.CompetencyConductHasActiveUsageAsync(conduct.Id, cancellationToken))
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.CompetencyConductInUse);
        }

        var before = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Competency conduct response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            conduct.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Competency conduct response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyConductInactivated,
                    AuditEntityTypes.CompetencyConduct,
                    conduct.PublicId,
                    conduct.Description,
                    AuditActions.Deactivate,
                    $"Inactivated competency conduct {conduct.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyConductResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCompetencyConductBehaviorsCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompetencyConductBehaviorsCommand, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        UpdateCompetencyConductBehaviorsCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompetencyConductResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var conduct = await repository.GetCompetencyConductByIdAsync(command.ConductId, includeBehaviors: true, cancellationToken);
        if (conduct is null)
        {
            return Result<CompetencyConductResponse>.Failure(
                await repository.CompetencyConductExistsOutsideTenantAsync(command.ConductId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.CompetencyConductNotFound);
        }

        if (conduct.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        // Batch-resolve the behavior catalog items up front (one query) to avoid a per-behavior N+1.
        var behaviorCatalogById = await repository.ResolveActiveCatalogItemsAsync(
            conduct.TenantId,
            JobCatalogCategory.Behavior,
            command.Behaviors.Select(behavior => behavior.BehaviorId).ToArray(),
            cancellationToken);

        var behaviorById = new Dictionary<Guid, long>();
        foreach (var behavior in command.Behaviors)
        {
            if (behaviorById.ContainsKey(behavior.BehaviorId))
            {
                return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.CompetencyConductBehaviorDuplicate);
            }

            var resolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogFromMapAsync(
                behaviorCatalogById,
                behavior.BehaviorId,
                JobCatalogCategory.Behavior,
                repository,
                authorizationService,
                RbacPermissionAction.Update,
                cancellationToken);
            if (resolution.IsFailure)
            {
                return Result<CompetencyConductResponse>.Failure(resolution.Error);
            }

            behaviorById.Add(behavior.BehaviorId, resolution.Value.Id);
        }

        var before = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Competency conduct response could not be resolved before behavior update.");

        var behaviorEntities = command.Behaviors
            .Select(input =>
            {
                var entity = CompetencyConductBehavior.Create(
                    behaviorById[input.BehaviorId],
                    input.Notes,
                    input.SortOrder);
                entity.SetTenantId(conduct.TenantId);
                return entity;
            })
            .ToArray();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            conduct.ReplaceBehaviors(behaviorEntities);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Competency conduct response could not be resolved after behavior update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyBehaviorLinked,
                    AuditEntityTypes.CompetencyConduct,
                    conduct.PublicId,
                    conduct.Description,
                    AuditActions.Update,
                    $"Updated behavior associations for competency conduct {conduct.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyConductResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
