using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using FluentValidation;

namespace CLARIHR.Application.Features.PositionDescriptionCatalogs;

public sealed record CatalogReferenceInternal(
    long InternalId,
    Guid Id,
    string Code,
    string Name,
    bool IsActive);

public sealed record CatalogReferenceResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsActive);

public sealed record PositionDescriptionCatalogItemResponse(
    Guid Id,
    PositionDescriptionCatalogType CatalogType,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record PositionCategoryClassificationResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    CatalogReferenceResponse PositionFunctionType,
    CatalogReferenceResponse PositionContractType,
    CatalogReferenceResponse OrgUnitType,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record PositionCategoryResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    CatalogReferenceResponse Classification,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record PositionSlotContractTypeLookup(
    Guid PositionSlotId,
    Guid JobProfileId,
    Guid? PositionCategoryId,
    Guid? ClassificationId,
    Guid? ContractTypeId,
    string? ContractTypeCode,
    string? ContractTypeName);

public sealed record SearchPositionDescriptionCatalogItemsQuery(
    Guid CompanyId,
    PositionDescriptionCatalogType CatalogType,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<PositionDescriptionCatalogItemResponse>>;

public sealed record GetPositionDescriptionCatalogItemByIdQuery(Guid ItemId)
    : IQuery<PositionDescriptionCatalogItemResponse>;

public sealed record CreatePositionDescriptionCatalogItemCommand(
    Guid CompanyId,
    PositionDescriptionCatalogType CatalogType,
    string Code,
    string Name,
    string? Description,
    int SortOrder)
    : ICommand<PositionDescriptionCatalogItemResponse>;

public sealed record SearchPositionCategoryClassificationsQuery(
    Guid CompanyId,
    Guid? PositionFunctionTypeId,
    Guid? PositionContractTypeId,
    Guid? OrgUnitTypeId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<PositionCategoryClassificationResponse>>;

public sealed record GetPositionCategoryClassificationByIdQuery(Guid ClassificationId)
    : IQuery<PositionCategoryClassificationResponse>;

public sealed record CreatePositionCategoryClassificationCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string? Description,
    Guid PositionFunctionTypeId,
    Guid PositionContractTypeId,
    Guid OrgUnitTypeId,
    int SortOrder)
    : ICommand<PositionCategoryClassificationResponse>;

public sealed record SearchPositionCategoriesQuery(
    Guid CompanyId,
    Guid? ClassificationId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<PositionCategoryResponse>>;

public sealed record GetPositionCategoryByIdQuery(Guid CategoryId)
    : IQuery<PositionCategoryResponse>;

public sealed record CreatePositionCategoryCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string? Description,
    Guid ClassificationId,
    int SortOrder)
    : ICommand<PositionCategoryResponse>;

internal sealed class SearchPositionDescriptionCatalogItemsQueryValidator : AbstractValidator<SearchPositionDescriptionCatalogItemsQuery>
{
    public SearchPositionDescriptionCatalogItemsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PositionDescriptionCatalogValidationRules.MaxPageSize);
    }
}

internal sealed class GetPositionDescriptionCatalogItemByIdQueryValidator : AbstractValidator<GetPositionDescriptionCatalogItemByIdQuery>
{
    public GetPositionDescriptionCatalogItemByIdQueryValidator() => RuleFor(query => query.ItemId).NotEmpty();
}

internal sealed class CreatePositionDescriptionCatalogItemCommandValidator : AbstractValidator<CreatePositionDescriptionCatalogItemCommand>
{
    public CreatePositionDescriptionCatalogItemCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(PositionDescriptionCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.CatalogType)
            .Must(PositionDescriptionCatalogErrors.IsSimpleCatalogType)
            .WithMessage("Unsupported catalog type.");
    }
}

internal sealed class SearchPositionCategoryClassificationsQueryValidator : AbstractValidator<SearchPositionCategoryClassificationsQuery>
{
    public SearchPositionCategoryClassificationsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PositionFunctionTypeId).NotEqual(Guid.Empty).When(query => query.PositionFunctionTypeId.HasValue);
        RuleFor(query => query.PositionContractTypeId).NotEqual(Guid.Empty).When(query => query.PositionContractTypeId.HasValue);
        RuleFor(query => query.OrgUnitTypeId).NotEqual(Guid.Empty).When(query => query.OrgUnitTypeId.HasValue);
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PositionDescriptionCatalogValidationRules.MaxPageSize);
    }
}

internal sealed class GetPositionCategoryClassificationByIdQueryValidator : AbstractValidator<GetPositionCategoryClassificationByIdQuery>
{
    public GetPositionCategoryClassificationByIdQueryValidator() => RuleFor(query => query.ClassificationId).NotEmpty();
}

internal sealed class CreatePositionCategoryClassificationCommandValidator : AbstractValidator<CreatePositionCategoryClassificationCommand>
{
    public CreatePositionCategoryClassificationCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(PositionDescriptionCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.PositionFunctionTypeId).NotEmpty();
        RuleFor(command => command.PositionContractTypeId).NotEmpty();
        RuleFor(command => command.OrgUnitTypeId).NotEmpty();
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class SearchPositionCategoriesQueryValidator : AbstractValidator<SearchPositionCategoriesQuery>
{
    public SearchPositionCategoriesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.ClassificationId).NotEqual(Guid.Empty).When(query => query.ClassificationId.HasValue);
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PositionDescriptionCatalogValidationRules.MaxPageSize);
    }
}

internal sealed class GetPositionCategoryByIdQueryValidator : AbstractValidator<GetPositionCategoryByIdQuery>
{
    public GetPositionCategoryByIdQueryValidator() => RuleFor(query => query.CategoryId).NotEmpty();
}

internal sealed class CreatePositionCategoryCommandValidator : AbstractValidator<CreatePositionCategoryCommand>
{
    public CreatePositionCategoryCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(PositionDescriptionCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.ClassificationId).NotEmpty();
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class SearchPositionDescriptionCatalogItemsQueryHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchPositionDescriptionCatalogItemsQuery, PagedResponse<PositionDescriptionCatalogItemResponse>>
{
    public async Task<Result<PagedResponse<PositionDescriptionCatalogItemResponse>>> Handle(
        SearchPositionDescriptionCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PagedResponse<PositionDescriptionCatalogItemResponse>>.Failure(authResult.Error);
        }

        var response = await repository.SearchCatalogItemsAsync(
            query.CompanyId,
            query.CatalogType,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (query.IncludeAllowedActions)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
            var enrichedItems = new List<PositionDescriptionCatalogItemResponse>(response.Items.Count);

            foreach (var item in response.Items)
            {
                var hasDependencies = item.IsActive &&
                    await PositionDescriptionCatalogPolicyAdapter.HasSimpleDependenciesAsync(repository, item.Id, cancellationToken);

                enrichedItems.Add(
                    PositionDescriptionCatalogPolicyAdapter.ApplyAllowedActions(
                        item,
                        resourceActionPolicyService,
                        canManage,
                        hasDependencies));
            }

            response = new PagedResponse<PositionDescriptionCatalogItemResponse>(
                enrichedItems,
                response.PageNumber,
                response.PageSize,
                response.TotalCount);
        }

        return Result<PagedResponse<PositionDescriptionCatalogItemResponse>>.Success(response);
    }
}

internal sealed class GetPositionDescriptionCatalogItemByIdQueryHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetPositionDescriptionCatalogItemByIdQuery, PositionDescriptionCatalogItemResponse>
{
    public async Task<Result<PositionDescriptionCatalogItemResponse>> Handle(
        GetPositionDescriptionCatalogItemByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(authResult.Error);
        }

        var response = await repository.GetCatalogItemResponseByIdAsync(query.ItemId, cancellationToken);
        if (response is not null)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Success(response);
        }

        return Result<PositionDescriptionCatalogItemResponse>.Failure(
            await repository.ExistsCatalogItemOutsideTenantAsync(query.ItemId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : PositionDescriptionCatalogErrors.CatalogItemNotFound);
    }
}

internal sealed class CreatePositionDescriptionCatalogItemCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePositionDescriptionCatalogItemCommand, PositionDescriptionCatalogItemResponse>
{
    public async Task<Result<PositionDescriptionCatalogItemResponse>> Handle(
        CreatePositionDescriptionCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(authResult.Error);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CatalogItemCodeExistsAsync(command.CompanyId, command.CatalogType, normalizedCode, excludingId: null, cancellationToken))
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(PositionDescriptionCatalogErrors.CatalogCodeConflict);
        }

        var entity = PositionDescriptionCatalogItem.Create(
            command.CatalogType,
            command.Code,
            command.Name,
            command.Description,
            command.SortOrder);
        entity.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddCatalogItem(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            PositionDescriptionCatalogCacheInvalidation.InvalidateSimple(repository, entity.TenantId, entity.CatalogType);

            var response = await repository.GetCatalogItemResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionDescriptionCatalogItemCreated,
                    AuditEntityTypes.PositionDescriptionCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Create,
                    $"Created position description catalog item {entity.Code} ({entity.CatalogType}).",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionDescriptionCatalogItemResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class SearchPositionCategoryClassificationsQueryHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchPositionCategoryClassificationsQuery, PagedResponse<PositionCategoryClassificationResponse>>
{
    public async Task<Result<PagedResponse<PositionCategoryClassificationResponse>>> Handle(
        SearchPositionCategoryClassificationsQuery query,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PagedResponse<PositionCategoryClassificationResponse>>.Failure(authResult.Error);
        }

        var response = await repository.SearchClassificationsAsync(
            query.CompanyId,
            query.PositionFunctionTypeId,
            query.PositionContractTypeId,
            query.OrgUnitTypeId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (query.IncludeAllowedActions)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
            var enrichedItems = new List<PositionCategoryClassificationResponse>(response.Items.Count);

            foreach (var item in response.Items)
            {
                var hasDependencies = item.IsActive &&
                    await PositionDescriptionCatalogPolicyAdapter.HasClassificationDependenciesAsync(repository, item.Id, cancellationToken);

                enrichedItems.Add(
                    PositionDescriptionCatalogPolicyAdapter.ApplyAllowedActions(
                        item,
                        resourceActionPolicyService,
                        canManage,
                        hasDependencies));
            }

            response = new PagedResponse<PositionCategoryClassificationResponse>(
                enrichedItems,
                response.PageNumber,
                response.PageSize,
                response.TotalCount);
        }

        return Result<PagedResponse<PositionCategoryClassificationResponse>>.Success(response);
    }
}

internal sealed class GetPositionCategoryClassificationByIdQueryHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetPositionCategoryClassificationByIdQuery, PositionCategoryClassificationResponse>
{
    public async Task<Result<PositionCategoryClassificationResponse>> Handle(
        GetPositionCategoryClassificationByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(authResult.Error);
        }

        var response = await repository.GetClassificationResponseByIdAsync(query.ClassificationId, cancellationToken);
        if (response is not null)
        {
            return Result<PositionCategoryClassificationResponse>.Success(response);
        }

        return Result<PositionCategoryClassificationResponse>.Failure(
            await repository.ExistsClassificationOutsideTenantAsync(query.ClassificationId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : PositionDescriptionCatalogErrors.ClassificationNotFound);
    }
}

internal sealed class CreatePositionCategoryClassificationCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePositionCategoryClassificationCommand, PositionCategoryClassificationResponse>
{
    public async Task<Result<PositionCategoryClassificationResponse>> Handle(
        CreatePositionCategoryClassificationCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(authResult.Error);
        }

        var positionFunctionLookup = await repository.GetActiveCatalogReferenceAsync(
            command.CompanyId,
            PositionDescriptionCatalogType.PositionFunctionType,
            command.PositionFunctionTypeId,
            cancellationToken);
        var contractTypeLookup = await repository.GetActiveCatalogReferenceAsync(
            command.CompanyId,
            PositionDescriptionCatalogType.PositionContractType,
            command.PositionContractTypeId,
            cancellationToken);
        var orgUnitTypeLookup = await repository.GetActiveOrgUnitTypeReferenceAsync(
            command.CompanyId,
            command.OrgUnitTypeId,
            cancellationToken);

        if (positionFunctionLookup is null || contractTypeLookup is null)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound);
        }

        if (orgUnitTypeLookup is null)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.OrgUnitTypeNotFound);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.ClassificationCodeExistsAsync(command.CompanyId, normalizedCode, excludingId: null, cancellationToken))
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ClassificationCodeConflict);
        }

        if (await repository.ClassificationAxesExistsAsync(
                command.CompanyId,
                positionFunctionLookup.InternalId,
                contractTypeLookup.InternalId,
                orgUnitTypeLookup.InternalId,
                excludingId: null,
                cancellationToken))
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ClassificationDuplicateAxes);
        }

        var entity = PositionCategoryClassification.Create(
            command.Code,
            command.Name,
            command.Description,
            positionFunctionLookup.InternalId,
            contractTypeLookup.InternalId,
            orgUnitTypeLookup.InternalId,
            command.SortOrder);
        entity.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddClassification(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            PositionDescriptionCatalogCacheInvalidation.InvalidateClassificationAndDependents(repository, entity.TenantId);

            var response = await repository.GetClassificationResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Classification response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionCategoryClassificationCreated,
                    AuditEntityTypes.PositionCategoryClassification,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Create,
                    $"Created position category classification {entity.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionCategoryClassificationResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class SearchPositionCategoriesQueryHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchPositionCategoriesQuery, PagedResponse<PositionCategoryResponse>>
{
    public async Task<Result<PagedResponse<PositionCategoryResponse>>> Handle(
        SearchPositionCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PagedResponse<PositionCategoryResponse>>.Failure(authResult.Error);
        }

        var response = await repository.SearchCategoriesAsync(
            query.CompanyId,
            query.ClassificationId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (query.IncludeAllowedActions)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
            var enrichedItems = new List<PositionCategoryResponse>(response.Items.Count);

            foreach (var item in response.Items)
            {
                var hasDependencies = item.IsActive &&
                    await PositionDescriptionCatalogPolicyAdapter.HasCategoryDependenciesAsync(repository, item.Id, cancellationToken);

                enrichedItems.Add(
                    PositionDescriptionCatalogPolicyAdapter.ApplyAllowedActions(
                        item,
                        resourceActionPolicyService,
                        canManage,
                        hasDependencies));
            }

            response = new PagedResponse<PositionCategoryResponse>(
                enrichedItems,
                response.PageNumber,
                response.PageSize,
                response.TotalCount);
        }

        return Result<PagedResponse<PositionCategoryResponse>>.Success(response);
    }
}

internal sealed class GetPositionCategoryByIdQueryHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetPositionCategoryByIdQuery, PositionCategoryResponse>
{
    public async Task<Result<PositionCategoryResponse>> Handle(
        GetPositionCategoryByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionCategoryResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryResponse>.Failure(authResult.Error);
        }

        var response = await repository.GetCategoryResponseByIdAsync(query.CategoryId, cancellationToken);
        if (response is not null)
        {
            return Result<PositionCategoryResponse>.Success(response);
        }

        return Result<PositionCategoryResponse>.Failure(
            await repository.ExistsCategoryOutsideTenantAsync(query.CategoryId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : PositionDescriptionCatalogErrors.CategoryNotFound);
    }
}

internal sealed class CreatePositionCategoryCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePositionCategoryCommand, PositionCategoryResponse>
{
    public async Task<Result<PositionCategoryResponse>> Handle(
        CreatePositionCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryResponse>.Failure(authResult.Error);
        }

        var classificationLookup = await repository.GetClassificationResponseByIdAsync(command.ClassificationId, cancellationToken);
        if (classificationLookup is null)
        {
            return Result<PositionCategoryResponse>.Failure(PositionDescriptionCatalogErrors.ClassificationNotFound);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CategoryCodeExistsAsync(command.CompanyId, normalizedCode, excludingId: null, cancellationToken))
        {
            return Result<PositionCategoryResponse>.Failure(PositionDescriptionCatalogErrors.CategoryCodeConflict);
        }

        var classificationEntity = await repository.GetClassificationByIdAsync(command.ClassificationId, cancellationToken)
            ?? throw new InvalidOperationException("Classification entity could not be resolved during category creation.");

        var entity = PositionCategory.Create(
            command.Code,
            command.Name,
            command.Description,
            classificationEntity.Id,
            command.SortOrder);
        entity.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddCategory(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            repository.InvalidateCategoryCache(entity.TenantId);

            var response = await repository.GetCategoryResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Category response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionCategoryCreated,
                    AuditEntityTypes.PositionCategory,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Create,
                    $"Created position category {entity.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionCategoryResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class PositionDescriptionCatalogPolicyAdapter
{
    public static PositionDescriptionCatalogItemResponse ApplyAllowedActions(
        PositionDescriptionCatalogItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage,
        bool hasDependencies) =>
        response with
        {
            AllowedActions = Evaluate(resourceActionPolicyService, response.IsActive, canManage, hasDependencies, response.CatalogType.ToString())
        };

    public static PositionCategoryClassificationResponse ApplyAllowedActions(
        PositionCategoryClassificationResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage,
        bool hasDependencies) =>
        response with
        {
            AllowedActions = Evaluate(resourceActionPolicyService, response.IsActive, canManage, hasDependencies, "PositionCategoryClassifications")
        };

    public static PositionCategoryResponse ApplyAllowedActions(
        PositionCategoryResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage,
        bool hasDependencies) =>
        response with
        {
            AllowedActions = Evaluate(resourceActionPolicyService, response.IsActive, canManage, hasDependencies, "PositionCategories")
        };

    public static async Task<bool> HasSimpleDependenciesAsync(
        IPositionDescriptionCatalogRepository repository,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var entity = await repository.GetCatalogItemByIdAsync(itemId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        return entity.CatalogType switch
        {
            PositionDescriptionCatalogType.PositionFunctionType => await repository.HasClassificationsUsingCatalogItemAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.PositionContractType => await repository.HasClassificationsUsingCatalogItemAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.Frequency => await repository.HasFunctionsUsingFrequencyAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.RequirementType => await repository.HasRequirementsUsingRequirementTypeAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.WorkConditionType => await repository.HasWorkConditionsUsingWorkConditionTypeAsync(entity.Id, cancellationToken),
            _ => await repository.HasJobProfilesUsingCatalogItemAsync(entity.Id, cancellationToken)
        };
    }

    public static async Task<bool> HasClassificationDependenciesAsync(
        IPositionDescriptionCatalogRepository repository,
        Guid classificationId,
        CancellationToken cancellationToken)
    {
        var entity = await repository.GetClassificationByIdAsync(classificationId, cancellationToken);
        return entity is not null &&
               await repository.HasCategoriesUsingClassificationAsync(entity.Id, cancellationToken);
    }

    public static async Task<bool> HasCategoryDependenciesAsync(
        IPositionDescriptionCatalogRepository repository,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var entity = await repository.GetCategoryByIdAsync(categoryId, cancellationToken);
        return entity is not null &&
               await repository.HasJobProfilesUsingCategoryAsync(entity.Id, cancellationToken);
    }

    private static AllowedActionsResponse Evaluate(
        IResourceActionPolicyService resourceActionPolicyService,
        bool isActive,
        bool canManage,
        bool hasDependencies,
        string resourceKey) =>
        resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                ResourceKey: resourceKey,
                State: isActive ? "Active" : "Inactive",
                IsActive: isActive,
                HasDependencies: hasDependencies,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));
}

internal static class PositionDescriptionCatalogCacheInvalidation
{
    public static void InvalidateSimple(
        IPositionDescriptionCatalogRepository repository,
        Guid tenantId,
        PositionDescriptionCatalogType catalogType)
    {
        repository.InvalidateSimpleCatalogCache(tenantId, catalogType);
        if (catalogType is PositionDescriptionCatalogType.PositionFunctionType or PositionDescriptionCatalogType.PositionContractType)
        {
            InvalidateClassificationAndDependents(repository, tenantId);
        }
    }

    public static void InvalidateClassificationAndDependents(
        IPositionDescriptionCatalogRepository repository,
        Guid tenantId)
    {
        repository.InvalidateClassificationCache(tenantId);
        repository.InvalidateCategoryCache(tenantId);
    }
}
