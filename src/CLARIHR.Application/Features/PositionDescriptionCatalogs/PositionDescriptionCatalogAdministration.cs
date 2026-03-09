using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using FluentValidation;

namespace CLARIHR.Application.Features.PositionDescriptionCatalogs;

public sealed record CatalogReferenceResponse(
    long InternalId,
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
    DateTime? ModifiedAtUtc);

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
    DateTime? ModifiedAtUtc);

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
    DateTime? ModifiedAtUtc);

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
    int PageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize)
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

public sealed record UpdatePositionDescriptionCatalogItemCommand(
    Guid ItemId,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<PositionDescriptionCatalogItemResponse>;

public sealed record ActivatePositionDescriptionCatalogItemCommand(Guid ItemId, Guid ConcurrencyToken)
    : ICommand<PositionDescriptionCatalogItemResponse>;

public sealed record InactivatePositionDescriptionCatalogItemCommand(Guid ItemId, Guid ConcurrencyToken)
    : ICommand<PositionDescriptionCatalogItemResponse>;

public sealed record SearchPositionCategoryClassificationsQuery(
    Guid CompanyId,
    Guid? PositionFunctionTypeId,
    Guid? PositionContractTypeId,
    Guid? OrgUnitTypeId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize)
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

public sealed record UpdatePositionCategoryClassificationCommand(
    Guid ClassificationId,
    string Code,
    string Name,
    string? Description,
    Guid PositionFunctionTypeId,
    Guid PositionContractTypeId,
    Guid OrgUnitTypeId,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<PositionCategoryClassificationResponse>;

public sealed record ActivatePositionCategoryClassificationCommand(Guid ClassificationId, Guid ConcurrencyToken)
    : ICommand<PositionCategoryClassificationResponse>;

public sealed record InactivatePositionCategoryClassificationCommand(Guid ClassificationId, Guid ConcurrencyToken)
    : ICommand<PositionCategoryClassificationResponse>;

public sealed record SearchPositionCategoriesQuery(
    Guid CompanyId,
    Guid? ClassificationId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize)
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

public sealed record UpdatePositionCategoryCommand(
    Guid CategoryId,
    string Code,
    string Name,
    string? Description,
    Guid ClassificationId,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<PositionCategoryResponse>;

public sealed record ActivatePositionCategoryCommand(Guid CategoryId, Guid ConcurrencyToken)
    : ICommand<PositionCategoryResponse>;

public sealed record InactivatePositionCategoryCommand(Guid CategoryId, Guid ConcurrencyToken)
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

internal sealed class UpdatePositionDescriptionCatalogItemCommandValidator : AbstractValidator<UpdatePositionDescriptionCatalogItemCommand>
{
    public UpdatePositionDescriptionCatalogItemCommandValidator()
    {
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(PositionDescriptionCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivatePositionDescriptionCatalogItemCommandValidator : AbstractValidator<ActivatePositionDescriptionCatalogItemCommand>
{
    public ActivatePositionDescriptionCatalogItemCommandValidator()
    {
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivatePositionDescriptionCatalogItemCommandValidator : AbstractValidator<InactivatePositionDescriptionCatalogItemCommand>
{
    public InactivatePositionDescriptionCatalogItemCommandValidator()
    {
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
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

internal sealed class UpdatePositionCategoryClassificationCommandValidator : AbstractValidator<UpdatePositionCategoryClassificationCommand>
{
    public UpdatePositionCategoryClassificationCommandValidator()
    {
        RuleFor(command => command.ClassificationId).NotEmpty();
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
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivatePositionCategoryClassificationCommandValidator : AbstractValidator<ActivatePositionCategoryClassificationCommand>
{
    public ActivatePositionCategoryClassificationCommandValidator()
    {
        RuleFor(command => command.ClassificationId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivatePositionCategoryClassificationCommandValidator : AbstractValidator<InactivatePositionCategoryClassificationCommand>
{
    public InactivatePositionCategoryClassificationCommandValidator()
    {
        RuleFor(command => command.ClassificationId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
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

internal sealed class UpdatePositionCategoryCommandValidator : AbstractValidator<UpdatePositionCategoryCommand>
{
    public UpdatePositionCategoryCommandValidator()
    {
        RuleFor(command => command.CategoryId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(PositionDescriptionCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.ClassificationId).NotEmpty();
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivatePositionCategoryCommandValidator : AbstractValidator<ActivatePositionCategoryCommand>
{
    public ActivatePositionCategoryCommandValidator()
    {
        RuleFor(command => command.CategoryId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivatePositionCategoryCommandValidator : AbstractValidator<InactivatePositionCategoryCommand>
{
    public InactivatePositionCategoryCommandValidator()
    {
        RuleFor(command => command.CategoryId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchPositionDescriptionCatalogItemsQueryHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository)
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

internal sealed class UpdatePositionDescriptionCatalogItemCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePositionDescriptionCatalogItemCommand, PositionDescriptionCatalogItemResponse>
{
    public async Task<Result<PositionDescriptionCatalogItemResponse>> Handle(
        UpdatePositionDescriptionCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetCatalogItemByIdAsync(command.ItemId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(
                await repository.ExistsCatalogItemOutsideTenantAsync(command.ItemId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.CatalogItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CatalogItemCodeExistsAsync(entity.TenantId, entity.CatalogType, normalizedCode, entity.Id, cancellationToken))
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(PositionDescriptionCatalogErrors.CatalogCodeConflict);
        }

        var before = await repository.GetCatalogItemResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Catalog item response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Update(command.Code, command.Name, command.Description, command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            PositionDescriptionCatalogCacheInvalidation.InvalidateSimple(repository, entity.TenantId, entity.CatalogType);

            var after = await repository.GetCatalogItemResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionDescriptionCatalogItemUpdated,
                    AuditEntityTypes.PositionDescriptionCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Update,
                    $"Updated position description catalog item {entity.Code} ({entity.CatalogType}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionDescriptionCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivatePositionDescriptionCatalogItemCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivatePositionDescriptionCatalogItemCommand, PositionDescriptionCatalogItemResponse>
{
    public async Task<Result<PositionDescriptionCatalogItemResponse>> Handle(
        ActivatePositionDescriptionCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetCatalogItemByIdAsync(command.ItemId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(
                await repository.ExistsCatalogItemOutsideTenantAsync(command.ItemId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.CatalogItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        var before = await repository.GetCatalogItemResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Catalog item response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            PositionDescriptionCatalogCacheInvalidation.InvalidateSimple(repository, entity.TenantId, entity.CatalogType);

            var after = await repository.GetCatalogItemResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionDescriptionCatalogItemActivated,
                    AuditEntityTypes.PositionDescriptionCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Reactivate,
                    $"Activated position description catalog item {entity.Code} ({entity.CatalogType}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionDescriptionCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivatePositionDescriptionCatalogItemCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivatePositionDescriptionCatalogItemCommand, PositionDescriptionCatalogItemResponse>
{
    public async Task<Result<PositionDescriptionCatalogItemResponse>> Handle(
        InactivatePositionDescriptionCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetCatalogItemByIdAsync(command.ItemId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(
                await repository.ExistsCatalogItemOutsideTenantAsync(command.ItemId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.CatalogItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        if (await IsCatalogItemInUseAsync(entity, repository, cancellationToken))
        {
            return Result<PositionDescriptionCatalogItemResponse>.Failure(PositionDescriptionCatalogErrors.CatalogInUse);
        }

        var before = await repository.GetCatalogItemResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Catalog item response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            PositionDescriptionCatalogCacheInvalidation.InvalidateSimple(repository, entity.TenantId, entity.CatalogType);

            var after = await repository.GetCatalogItemResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionDescriptionCatalogItemInactivated,
                    AuditEntityTypes.PositionDescriptionCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Deactivate,
                    $"Inactivated position description catalog item {entity.Code} ({entity.CatalogType}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionDescriptionCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static Task<bool> IsCatalogItemInUseAsync(
        PositionDescriptionCatalogItem entity,
        IPositionDescriptionCatalogRepository repository,
        CancellationToken cancellationToken) =>
        entity.CatalogType switch
        {
            PositionDescriptionCatalogType.PositionFunctionType => repository.HasClassificationsUsingCatalogItemAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.PositionContractType => repository.HasClassificationsUsingCatalogItemAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.Frequency => repository.HasFunctionsUsingFrequencyAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.RequirementType => repository.HasRequirementsUsingRequirementTypeAsync(entity.Id, cancellationToken),
            PositionDescriptionCatalogType.WorkConditionType => repository.HasWorkConditionsUsingWorkConditionTypeAsync(entity.Id, cancellationToken),
            _ => repository.HasJobProfilesUsingCatalogItemAsync(entity.Id, cancellationToken)
        };
}

internal sealed class SearchPositionCategoryClassificationsQueryHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository)
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

internal sealed class UpdatePositionCategoryClassificationCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePositionCategoryClassificationCommand, PositionCategoryClassificationResponse>
{
    public async Task<Result<PositionCategoryClassificationResponse>> Handle(
        UpdatePositionCategoryClassificationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetClassificationByIdAsync(command.ClassificationId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(
                await repository.ExistsClassificationOutsideTenantAsync(command.ClassificationId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.ClassificationNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        var positionFunctionLookup = await repository.GetActiveCatalogReferenceAsync(
            entity.TenantId,
            PositionDescriptionCatalogType.PositionFunctionType,
            command.PositionFunctionTypeId,
            cancellationToken);
        var contractTypeLookup = await repository.GetActiveCatalogReferenceAsync(
            entity.TenantId,
            PositionDescriptionCatalogType.PositionContractType,
            command.PositionContractTypeId,
            cancellationToken);
        var orgUnitTypeLookup = await repository.GetActiveOrgUnitTypeReferenceAsync(
            entity.TenantId,
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
        if (await repository.ClassificationCodeExistsAsync(entity.TenantId, normalizedCode, entity.Id, cancellationToken))
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ClassificationCodeConflict);
        }

        if (await repository.ClassificationAxesExistsAsync(
                entity.TenantId,
                positionFunctionLookup.InternalId,
                contractTypeLookup.InternalId,
                orgUnitTypeLookup.InternalId,
                entity.Id,
                cancellationToken))
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ClassificationDuplicateAxes);
        }

        var before = await repository.GetClassificationResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Classification response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Update(
                command.Code,
                command.Name,
                command.Description,
                positionFunctionLookup.InternalId,
                contractTypeLookup.InternalId,
                orgUnitTypeLookup.InternalId,
                command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            PositionDescriptionCatalogCacheInvalidation.InvalidateClassificationAndDependents(repository, entity.TenantId);

            var after = await repository.GetClassificationResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Classification response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionCategoryClassificationUpdated,
                    AuditEntityTypes.PositionCategoryClassification,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Update,
                    $"Updated position category classification {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionCategoryClassificationResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivatePositionCategoryClassificationCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivatePositionCategoryClassificationCommand, PositionCategoryClassificationResponse>
{
    public async Task<Result<PositionCategoryClassificationResponse>> Handle(
        ActivatePositionCategoryClassificationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetClassificationByIdAsync(command.ClassificationId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(
                await repository.ExistsClassificationOutsideTenantAsync(command.ClassificationId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.ClassificationNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        var before = await repository.GetClassificationResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Classification response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            PositionDescriptionCatalogCacheInvalidation.InvalidateClassificationAndDependents(repository, entity.TenantId);

            var after = await repository.GetClassificationResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Classification response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionCategoryClassificationActivated,
                    AuditEntityTypes.PositionCategoryClassification,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Reactivate,
                    $"Activated position category classification {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionCategoryClassificationResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivatePositionCategoryClassificationCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivatePositionCategoryClassificationCommand, PositionCategoryClassificationResponse>
{
    public async Task<Result<PositionCategoryClassificationResponse>> Handle(
        InactivatePositionCategoryClassificationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetClassificationByIdAsync(command.ClassificationId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(
                await repository.ExistsClassificationOutsideTenantAsync(command.ClassificationId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.ClassificationNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        if (await repository.HasCategoriesUsingClassificationAsync(entity.Id, cancellationToken))
        {
            return Result<PositionCategoryClassificationResponse>.Failure(PositionDescriptionCatalogErrors.ClassificationInUse);
        }

        var before = await repository.GetClassificationResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Classification response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            PositionDescriptionCatalogCacheInvalidation.InvalidateClassificationAndDependents(repository, entity.TenantId);

            var after = await repository.GetClassificationResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Classification response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionCategoryClassificationInactivated,
                    AuditEntityTypes.PositionCategoryClassification,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Deactivate,
                    $"Inactivated position category classification {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionCategoryClassificationResponse>.Success(after);
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
    IPositionDescriptionCatalogRepository repository)
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

internal sealed class UpdatePositionCategoryCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePositionCategoryCommand, PositionCategoryResponse>
{
    public async Task<Result<PositionCategoryResponse>> Handle(
        UpdatePositionCategoryCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionCategoryResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetCategoryByIdAsync(command.CategoryId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionCategoryResponse>.Failure(
                await repository.ExistsCategoryOutsideTenantAsync(command.CategoryId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.CategoryNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionCategoryResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        var classificationEntity = await repository.GetClassificationByIdAsync(command.ClassificationId, cancellationToken);
        if (classificationEntity is null)
        {
            return Result<PositionCategoryResponse>.Failure(
                await repository.ExistsClassificationOutsideTenantAsync(command.ClassificationId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.ClassificationNotFound);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CategoryCodeExistsAsync(entity.TenantId, normalizedCode, entity.Id, cancellationToken))
        {
            return Result<PositionCategoryResponse>.Failure(PositionDescriptionCatalogErrors.CategoryCodeConflict);
        }

        var before = await repository.GetCategoryResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Category response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Update(command.Code, command.Name, command.Description, classificationEntity.Id, command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            repository.InvalidateCategoryCache(entity.TenantId);

            var after = await repository.GetCategoryResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Category response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionCategoryUpdated,
                    AuditEntityTypes.PositionCategory,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Update,
                    $"Updated position category {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionCategoryResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivatePositionCategoryCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivatePositionCategoryCommand, PositionCategoryResponse>
{
    public async Task<Result<PositionCategoryResponse>> Handle(
        ActivatePositionCategoryCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionCategoryResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetCategoryByIdAsync(command.CategoryId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionCategoryResponse>.Failure(
                await repository.ExistsCategoryOutsideTenantAsync(command.CategoryId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.CategoryNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionCategoryResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        var before = await repository.GetCategoryResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Category response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            repository.InvalidateCategoryCache(entity.TenantId);

            var after = await repository.GetCategoryResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Category response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionCategoryActivated,
                    AuditEntityTypes.PositionCategory,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Reactivate,
                    $"Activated position category {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionCategoryResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivatePositionCategoryCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivatePositionCategoryCommand, PositionCategoryResponse>
{
    public async Task<Result<PositionCategoryResponse>> Handle(
        InactivatePositionCategoryCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionCategoryResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PositionCategoryResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetCategoryByIdAsync(command.CategoryId, cancellationToken);
        if (entity is null)
        {
            return Result<PositionCategoryResponse>.Failure(
                await repository.ExistsCategoryOutsideTenantAsync(command.CategoryId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionDescriptionCatalogErrors.CategoryNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionCategoryResponse>.Failure(PositionDescriptionCatalogErrors.ConcurrencyConflict);
        }

        if (await repository.HasJobProfilesUsingCategoryAsync(entity.Id, cancellationToken))
        {
            return Result<PositionCategoryResponse>.Failure(PositionDescriptionCatalogErrors.CategoryInUse);
        }

        var before = await repository.GetCategoryResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Category response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            repository.InvalidateCategoryCache(entity.TenantId);

            var after = await repository.GetCategoryResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Category response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionCategoryInactivated,
                    AuditEntityTypes.PositionCategory,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Deactivate,
                    $"Inactivated position category {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionCategoryResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
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
