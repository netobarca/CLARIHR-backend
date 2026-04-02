using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record JobCatalogItemResponse(
    Guid Id,
    JobCatalogCategory Category,
    string Code,
    string Name,
    bool IsSystem,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record SearchJobCatalogItemsQuery(
    Guid CompanyId,
    JobCatalogCategory Category,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = JobProfileValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<JobCatalogItemResponse>>;

public sealed record CreateJobCatalogItemCommand(
    Guid CompanyId,
    JobCatalogCategory Category,
    string Code,
    string Name) : ICommand<JobCatalogItemResponse>;

public sealed record ActivateJobCatalogItemCommand(Guid ItemId, Guid ConcurrencyToken) : ICommand<JobCatalogItemResponse>;

public sealed record InactivateJobCatalogItemCommand(Guid ItemId, Guid ConcurrencyToken) : ICommand<JobCatalogItemResponse>;

internal sealed class SearchJobCatalogItemsQueryValidator : AbstractValidator<SearchJobCatalogItemsQuery>
{
    public SearchJobCatalogItemsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, JobProfileValidationRules.MaxPageSize);
    }
}

internal sealed class CreateJobCatalogItemCommandValidator : AbstractValidator<CreateJobCatalogItemCommand>
{
    public CreateJobCatalogItemCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
    }
}

internal sealed class ActivateJobCatalogItemCommandValidator : AbstractValidator<ActivateJobCatalogItemCommand>
{
    public ActivateJobCatalogItemCommandValidator()
    {
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateJobCatalogItemCommandValidator : AbstractValidator<InactivateJobCatalogItemCommand>
{
    public InactivateJobCatalogItemCommandValidator()
    {
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchJobCatalogItemsQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobCatalogRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchJobCatalogItemsQuery, PagedResponse<JobCatalogItemResponse>>
{
    public async Task<Result<PagedResponse<JobCatalogItemResponse>>> Handle(
        SearchJobCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<JobCatalogItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.Category,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<JobCatalogItemResponse>>.Success(response);
        }

        var canManageCatalogs = (await authorizationService.EnsureCanManageCatalogsAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => JobCatalogPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManageCatalogs))
            .ToArray();

        return Result<PagedResponse<JobCatalogItemResponse>>.Success(response with { Items = items });
    }
}

internal static class JobCatalogPolicyAdapter
{
    public static JobCatalogItemResponse ApplyAllowedActions(
        JobCatalogItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManageCatalogs)
    {
        var state = response.Category.ToString();
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                JobProfilePermissionCodes.ResourceKey,
                state,
                response.IsActive,
                IsSystem: response.IsSystem,
                SupportsEdit: false,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManageCatalogs,
                SupportsInactivate: true,
                InactivateAllowed: canManageCatalogs));

        return response with { AllowedActions = allowedActions };
    }
}

internal sealed class CreateJobCatalogItemCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateJobCatalogItemCommand, JobCatalogItemResponse>
{
    public async Task<Result<JobCatalogItemResponse>> Handle(
        CreateJobCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageCatalogsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Category,
                command.Code.Trim().ToUpperInvariant(),
                excludingItemId: null,
                cancellationToken))
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.CatalogCodeConflict);
        }

        var item = JobCatalogItem.Create(command.Category, command.Code, command.Name);
        item.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(item);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(item.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobCatalogItemCreated,
                    AuditEntityTypes.JobCatalogItem,
                    item.PublicId,
                    item.Code,
                    AuditActions.Create,
                    $"Created job catalog item {item.Code} ({item.Category}).",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            repository.InvalidateCategoryCache(command.CompanyId, command.Category);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobCatalogItemResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateJobCatalogItemCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobCatalogRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateJobCatalogItemCommand, JobCatalogItemResponse>
{
    public async Task<Result<JobCatalogItemResponse>> Handle(
        ActivateJobCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageCatalogsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var item = await repository.GetByIdAsync(command.ItemId, cancellationToken);
        if (item is null)
        {
            return Result<JobCatalogItemResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.ItemId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.CatalogItemNotFound);
        }

        if (item.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(item.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Catalog item response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            item.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(item.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobCatalogItemUpdated,
                    AuditEntityTypes.JobCatalogItem,
                    item.PublicId,
                    item.Code,
                    AuditActions.Update,
                    $"Activated job catalog item {item.Code} ({item.Category}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            repository.InvalidateCategoryCache(item.TenantId, item.Category);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateJobCatalogItemCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobCatalogRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateJobCatalogItemCommand, JobCatalogItemResponse>
{
    public async Task<Result<JobCatalogItemResponse>> Handle(
        InactivateJobCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageCatalogsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var item = await repository.GetByIdAsync(command.ItemId, cancellationToken);
        if (item is null)
        {
            return Result<JobCatalogItemResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.ItemId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.CatalogItemNotFound);
        }

        if (item.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(item.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Catalog item response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            item.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(item.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobCatalogItemUpdated,
                    AuditEntityTypes.JobCatalogItem,
                    item.PublicId,
                    item.Code,
                    AuditActions.Update,
                    $"Inactivated job catalog item {item.Code} ({item.Category}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            repository.InvalidateCategoryCache(item.TenantId, item.Category);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
