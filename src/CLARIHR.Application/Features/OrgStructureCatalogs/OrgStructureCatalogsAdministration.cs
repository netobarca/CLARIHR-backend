using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using CLARIHR.Domain.OrgStructureCatalogs;
using FluentValidation;

namespace CLARIHR.Application.Features.OrgStructureCatalogs;

public sealed record CatalogReferenceLookup(
    long InternalId,
    Guid Id,
    string Code,
    string Name,
    bool IsActive);

public sealed record CompanyTypeCatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record OrgUnitTypeCatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record FunctionalAreaCatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record SearchOrgUnitTypesQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = OrgStructureCatalogValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<OrgUnitTypeCatalogItemResponse>>;

public sealed record GetOrgUnitTypeByIdQuery(Guid OrgUnitTypeId)
    : IQuery<OrgUnitTypeCatalogItemResponse>;

public sealed record CreateOrgUnitTypeCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string? Description,
    int SortOrder)
    : ICommand<OrgUnitTypeCatalogItemResponse>;

public sealed record UpdateOrgUnitTypeCommand(
    Guid OrgUnitTypeId,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<OrgUnitTypeCatalogItemResponse>;

public sealed record ActivateOrgUnitTypeCommand(
    Guid OrgUnitTypeId,
    Guid ConcurrencyToken)
    : ICommand<OrgUnitTypeCatalogItemResponse>;

public sealed record InactivateOrgUnitTypeCommand(
    Guid OrgUnitTypeId,
    Guid ConcurrencyToken)
    : ICommand<OrgUnitTypeCatalogItemResponse>;

public sealed record SearchFunctionalAreasQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = OrgStructureCatalogValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<FunctionalAreaCatalogItemResponse>>;

public sealed record GetFunctionalAreaByIdQuery(Guid FunctionalAreaId)
    : IQuery<FunctionalAreaCatalogItemResponse>;

public sealed record CreateFunctionalAreaCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string? Description,
    int SortOrder)
    : ICommand<FunctionalAreaCatalogItemResponse>;

public sealed record UpdateFunctionalAreaCommand(
    Guid FunctionalAreaId,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<FunctionalAreaCatalogItemResponse>;

public sealed record ActivateFunctionalAreaCommand(
    Guid FunctionalAreaId,
    Guid ConcurrencyToken)
    : ICommand<FunctionalAreaCatalogItemResponse>;

public sealed record InactivateFunctionalAreaCommand(
    Guid FunctionalAreaId,
    Guid ConcurrencyToken)
    : ICommand<FunctionalAreaCatalogItemResponse>;

internal sealed class SearchOrgUnitTypesQueryValidator : AbstractValidator<SearchOrgUnitTypesQuery>
{
    public SearchOrgUnitTypesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, OrgStructureCatalogValidationRules.MaxPageSize);
    }
}

internal sealed class GetOrgUnitTypeByIdQueryValidator : AbstractValidator<GetOrgUnitTypeByIdQuery>
{
    public GetOrgUnitTypeByIdQueryValidator()
    {
        RuleFor(query => query.OrgUnitTypeId).NotEmpty();
    }
}

internal sealed class CreateOrgUnitTypeCommandValidator : AbstractValidator<CreateOrgUnitTypeCommand>
{
    public CreateOrgUnitTypeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(OrgStructureCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateOrgUnitTypeCommandValidator : AbstractValidator<UpdateOrgUnitTypeCommand>
{
    public UpdateOrgUnitTypeCommandValidator()
    {
        RuleFor(command => command.OrgUnitTypeId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(OrgStructureCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateOrgUnitTypeCommandValidator : AbstractValidator<ActivateOrgUnitTypeCommand>
{
    public ActivateOrgUnitTypeCommandValidator()
    {
        RuleFor(command => command.OrgUnitTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateOrgUnitTypeCommandValidator : AbstractValidator<InactivateOrgUnitTypeCommand>
{
    public InactivateOrgUnitTypeCommandValidator()
    {
        RuleFor(command => command.OrgUnitTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchFunctionalAreasQueryValidator : AbstractValidator<SearchFunctionalAreasQuery>
{
    public SearchFunctionalAreasQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, OrgStructureCatalogValidationRules.MaxPageSize);
    }
}

internal sealed class GetFunctionalAreaByIdQueryValidator : AbstractValidator<GetFunctionalAreaByIdQuery>
{
    public GetFunctionalAreaByIdQueryValidator()
    {
        RuleFor(query => query.FunctionalAreaId).NotEmpty();
    }
}

internal sealed class CreateFunctionalAreaCommandValidator : AbstractValidator<CreateFunctionalAreaCommand>
{
    public CreateFunctionalAreaCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(OrgStructureCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateFunctionalAreaCommandValidator : AbstractValidator<UpdateFunctionalAreaCommand>
{
    public UpdateFunctionalAreaCommandValidator()
    {
        RuleFor(command => command.FunctionalAreaId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(OrgStructureCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateFunctionalAreaCommandValidator : AbstractValidator<ActivateFunctionalAreaCommand>
{
    public ActivateFunctionalAreaCommandValidator()
    {
        RuleFor(command => command.FunctionalAreaId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateFunctionalAreaCommandValidator : AbstractValidator<InactivateFunctionalAreaCommand>
{
    public InactivateFunctionalAreaCommandValidator()
    {
        RuleFor(command => command.FunctionalAreaId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchOrgUnitTypesQueryHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository)
    : IQueryHandler<SearchOrgUnitTypesQuery, PagedResponse<OrgUnitTypeCatalogItemResponse>>
{
    public async Task<Result<PagedResponse<OrgUnitTypeCatalogItemResponse>>> Handle(
        SearchOrgUnitTypesQuery query,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanReadTenantAsync(query.CompanyId, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PagedResponse<OrgUnitTypeCatalogItemResponse>>.Failure(authResult.Error);
        }

        var response = await repository.SearchOrgUnitTypesAsync(
            query.CompanyId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<OrgUnitTypeCatalogItemResponse>>.Success(response);
    }
}

internal sealed class GetOrgUnitTypeByIdQueryHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetOrgUnitTypeByIdQuery, OrgUnitTypeCatalogItemResponse>
{
    public async Task<Result<OrgUnitTypeCatalogItemResponse>> Handle(
        GetOrgUnitTypeByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanReadTenantAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(authResult.Error);
        }

        var response = await repository.GetOrgUnitTypeResponseByIdAsync(query.OrgUnitTypeId, cancellationToken);
        if (response is not null)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Success(response);
        }

        return Result<OrgUnitTypeCatalogItemResponse>.Failure(
            await repository.ExistsOrgUnitTypeOutsideTenantAsync(query.OrgUnitTypeId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : OrgStructureCatalogErrors.CatalogNotFound);
    }
}

internal sealed class CreateOrgUnitTypeCommandHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateOrgUnitTypeCommand, OrgUnitTypeCatalogItemResponse>
{
    public async Task<Result<OrgUnitTypeCatalogItemResponse>> Handle(
        CreateOrgUnitTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageTenantAsync(command.CompanyId, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(authResult.Error);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.OrgUnitTypeCodeExistsAsync(command.CompanyId, normalizedCode, excludingId: null, cancellationToken))
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(OrgStructureCatalogErrors.CatalogCodeConflict);
        }

        var entity = OrgUnitTypeCatalogItem.Create(command.Code, command.Name, command.Description, command.SortOrder);
        entity.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddOrgUnitType(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetOrgUnitTypeResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Org unit type response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OrgUnitTypeCatalogItemCreated,
                    AuditEntityTypes.OrgUnitTypeCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Create,
                    $"Created org unit type {entity.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OrgUnitTypeCatalogItemResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateOrgUnitTypeCommandHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateOrgUnitTypeCommand, OrgUnitTypeCatalogItemResponse>
{
    public async Task<Result<OrgUnitTypeCatalogItemResponse>> Handle(
        UpdateOrgUnitTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageTenantAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetOrgUnitTypeByIdAsync(command.OrgUnitTypeId, cancellationToken);
        if (entity is null)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(
                await repository.ExistsOrgUnitTypeOutsideTenantAsync(command.OrgUnitTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgStructureCatalogErrors.CatalogNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(OrgStructureCatalogErrors.ConcurrencyConflict);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.OrgUnitTypeCodeExistsAsync(entity.TenantId, normalizedCode, entity.Id, cancellationToken))
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(OrgStructureCatalogErrors.CatalogCodeConflict);
        }

        var before = await repository.GetOrgUnitTypeResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Org unit type response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Update(command.Code, command.Name, command.Description, command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetOrgUnitTypeResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Org unit type response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OrgUnitTypeCatalogItemUpdated,
                    AuditEntityTypes.OrgUnitTypeCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Update,
                    $"Updated org unit type {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OrgUnitTypeCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateOrgUnitTypeCommandHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateOrgUnitTypeCommand, OrgUnitTypeCatalogItemResponse>
{
    public async Task<Result<OrgUnitTypeCatalogItemResponse>> Handle(
        ActivateOrgUnitTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageTenantAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetOrgUnitTypeByIdAsync(command.OrgUnitTypeId, cancellationToken);
        if (entity is null)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(
                await repository.ExistsOrgUnitTypeOutsideTenantAsync(command.OrgUnitTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgStructureCatalogErrors.CatalogNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(OrgStructureCatalogErrors.ConcurrencyConflict);
        }

        var before = await repository.GetOrgUnitTypeResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Org unit type response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetOrgUnitTypeResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Org unit type response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OrgUnitTypeCatalogItemActivated,
                    AuditEntityTypes.OrgUnitTypeCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Reactivate,
                    $"Activated org unit type {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OrgUnitTypeCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateOrgUnitTypeCommandHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateOrgUnitTypeCommand, OrgUnitTypeCatalogItemResponse>
{
    public async Task<Result<OrgUnitTypeCatalogItemResponse>> Handle(
        InactivateOrgUnitTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageTenantAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetOrgUnitTypeByIdAsync(command.OrgUnitTypeId, cancellationToken);
        if (entity is null)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(
                await repository.ExistsOrgUnitTypeOutsideTenantAsync(command.OrgUnitTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgStructureCatalogErrors.CatalogNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(OrgStructureCatalogErrors.ConcurrencyConflict);
        }

        if (await repository.HasOrgUnitsUsingOrgUnitTypeAsync(entity.Id, cancellationToken) ||
            await repository.HasPositionCategoryClassificationsUsingOrgUnitTypeAsync(entity.Id, cancellationToken))
        {
            return Result<OrgUnitTypeCatalogItemResponse>.Failure(OrgStructureCatalogErrors.CatalogInUse);
        }

        var before = await repository.GetOrgUnitTypeResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Org unit type response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetOrgUnitTypeResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Org unit type response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OrgUnitTypeCatalogItemInactivated,
                    AuditEntityTypes.OrgUnitTypeCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Deactivate,
                    $"Inactivated org unit type {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OrgUnitTypeCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class SearchFunctionalAreasQueryHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository)
    : IQueryHandler<SearchFunctionalAreasQuery, PagedResponse<FunctionalAreaCatalogItemResponse>>
{
    public async Task<Result<PagedResponse<FunctionalAreaCatalogItemResponse>>> Handle(
        SearchFunctionalAreasQuery query,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanReadTenantAsync(query.CompanyId, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PagedResponse<FunctionalAreaCatalogItemResponse>>.Failure(authResult.Error);
        }

        var response = await repository.SearchFunctionalAreasAsync(
            query.CompanyId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<FunctionalAreaCatalogItemResponse>>.Success(response);
    }
}

internal sealed class GetFunctionalAreaByIdQueryHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetFunctionalAreaByIdQuery, FunctionalAreaCatalogItemResponse>
{
    public async Task<Result<FunctionalAreaCatalogItemResponse>> Handle(
        GetFunctionalAreaByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanReadTenantAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(authResult.Error);
        }

        var response = await repository.GetFunctionalAreaResponseByIdAsync(query.FunctionalAreaId, cancellationToken);
        if (response is not null)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Success(response);
        }

        return Result<FunctionalAreaCatalogItemResponse>.Failure(
            await repository.ExistsFunctionalAreaOutsideTenantAsync(query.FunctionalAreaId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : OrgStructureCatalogErrors.CatalogNotFound);
    }
}

internal sealed class CreateFunctionalAreaCommandHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateFunctionalAreaCommand, FunctionalAreaCatalogItemResponse>
{
    public async Task<Result<FunctionalAreaCatalogItemResponse>> Handle(
        CreateFunctionalAreaCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageTenantAsync(command.CompanyId, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(authResult.Error);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.FunctionalAreaCodeExistsAsync(command.CompanyId, normalizedCode, excludingId: null, cancellationToken))
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(OrgStructureCatalogErrors.CatalogCodeConflict);
        }

        var entity = FunctionalAreaCatalogItem.Create(command.Code, command.Name, command.Description, command.SortOrder);
        entity.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddFunctionalArea(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetFunctionalAreaResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Functional area response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.FunctionalAreaCatalogItemCreated,
                    AuditEntityTypes.FunctionalAreaCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Create,
                    $"Created functional area {entity.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<FunctionalAreaCatalogItemResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateFunctionalAreaCommandHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateFunctionalAreaCommand, FunctionalAreaCatalogItemResponse>
{
    public async Task<Result<FunctionalAreaCatalogItemResponse>> Handle(
        UpdateFunctionalAreaCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageTenantAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetFunctionalAreaByIdAsync(command.FunctionalAreaId, cancellationToken);
        if (entity is null)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(
                await repository.ExistsFunctionalAreaOutsideTenantAsync(command.FunctionalAreaId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgStructureCatalogErrors.CatalogNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(OrgStructureCatalogErrors.ConcurrencyConflict);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.FunctionalAreaCodeExistsAsync(entity.TenantId, normalizedCode, entity.Id, cancellationToken))
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(OrgStructureCatalogErrors.CatalogCodeConflict);
        }

        var before = await repository.GetFunctionalAreaResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Functional area response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Update(command.Code, command.Name, command.Description, command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetFunctionalAreaResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Functional area response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.FunctionalAreaCatalogItemUpdated,
                    AuditEntityTypes.FunctionalAreaCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Update,
                    $"Updated functional area {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<FunctionalAreaCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateFunctionalAreaCommandHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateFunctionalAreaCommand, FunctionalAreaCatalogItemResponse>
{
    public async Task<Result<FunctionalAreaCatalogItemResponse>> Handle(
        ActivateFunctionalAreaCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageTenantAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetFunctionalAreaByIdAsync(command.FunctionalAreaId, cancellationToken);
        if (entity is null)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(
                await repository.ExistsFunctionalAreaOutsideTenantAsync(command.FunctionalAreaId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgStructureCatalogErrors.CatalogNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(OrgStructureCatalogErrors.ConcurrencyConflict);
        }

        var before = await repository.GetFunctionalAreaResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Functional area response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetFunctionalAreaResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Functional area response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.FunctionalAreaCatalogItemActivated,
                    AuditEntityTypes.FunctionalAreaCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Reactivate,
                    $"Activated functional area {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<FunctionalAreaCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateFunctionalAreaCommandHandler(
    IOrgStructureCatalogAuthorizationService authorizationService,
    IOrgStructureCatalogRepository repository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateFunctionalAreaCommand, FunctionalAreaCatalogItemResponse>
{
    public async Task<Result<FunctionalAreaCatalogItemResponse>> Handle(
        InactivateFunctionalAreaCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authResult = await authorizationService.EnsureCanManageTenantAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetFunctionalAreaByIdAsync(command.FunctionalAreaId, cancellationToken);
        if (entity is null)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(
                await repository.ExistsFunctionalAreaOutsideTenantAsync(command.FunctionalAreaId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : OrgStructureCatalogErrors.CatalogNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(OrgStructureCatalogErrors.ConcurrencyConflict);
        }

        if (await repository.HasOrgUnitsUsingFunctionalAreaAsync(entity.Id, cancellationToken))
        {
            return Result<FunctionalAreaCatalogItemResponse>.Failure(OrgStructureCatalogErrors.CatalogInUse);
        }

        var before = await repository.GetFunctionalAreaResponseByIdAsync(entity.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Functional area response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetFunctionalAreaResponseByIdAsync(entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Functional area response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.FunctionalAreaCatalogItemInactivated,
                    AuditEntityTypes.FunctionalAreaCatalogItem,
                    entity.PublicId,
                    entity.Code,
                    AuditActions.Deactivate,
                    $"Inactivated functional area {entity.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<FunctionalAreaCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
