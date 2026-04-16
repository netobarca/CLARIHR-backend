using CLARIHR.Application.Abstractions.PersonnelEducationCatalogs;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelEducationCatalogs.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelEducationCatalogs;

public sealed record PersonnelEducationCatalogItemResponse(
    Guid Id,
    PersonnelEducationCatalogType CatalogType,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PersonnelEducationCatalogLookup(
    long InternalId,
    Guid Id,
    string Code,
    string Name,
    bool IsActive);

public sealed record PersonnelEducationCatalogCountryLookup(
    long CountryCatalogItemId,
    string CountryCode);

public sealed record SearchPersonnelEducationCatalogItemsQuery(
    Guid CompanyId,
    PersonnelEducationCatalogType CatalogType,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = PersonnelEducationCatalogValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PersonnelEducationCatalogItemResponse>>;

public sealed record GetPersonnelEducationCatalogItemByIdQuery(
    Guid CompanyId,
    PersonnelEducationCatalogType CatalogType,
    Guid Id)
    : IQuery<PersonnelEducationCatalogItemResponse>;

public sealed record CreatePersonnelEducationCatalogItemCommand(
    Guid CompanyId,
    PersonnelEducationCatalogType CatalogType,
    string Code,
    string Name,
    int SortOrder)
    : ICommand<PersonnelEducationCatalogItemResponse>;

public sealed record UpdatePersonnelEducationCatalogItemCommand(
    PersonnelEducationCatalogType CatalogType,
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<PersonnelEducationCatalogItemResponse>;

public sealed record ActivatePersonnelEducationCatalogItemCommand(
    PersonnelEducationCatalogType CatalogType,
    Guid Id,
    Guid ConcurrencyToken)
    : ICommand<PersonnelEducationCatalogItemResponse>;

public sealed record InactivatePersonnelEducationCatalogItemCommand(
    PersonnelEducationCatalogType CatalogType,
    Guid Id,
    Guid ConcurrencyToken)
    : ICommand<PersonnelEducationCatalogItemResponse>;

internal sealed class SearchPersonnelEducationCatalogItemsQueryValidator : AbstractValidator<SearchPersonnelEducationCatalogItemsQuery>
{
    public SearchPersonnelEducationCatalogItemsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PersonnelEducationCatalogValidationRules.MaxPageSize);
    }
}

internal sealed class GetPersonnelEducationCatalogItemByIdQueryValidator : AbstractValidator<GetPersonnelEducationCatalogItemByIdQuery>
{
    public GetPersonnelEducationCatalogItemByIdQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Id).NotEmpty();
    }
}

internal sealed class CreatePersonnelEducationCatalogItemCommandValidator : AbstractValidator<CreatePersonnelEducationCatalogItemCommand>
{
    public CreatePersonnelEducationCatalogItemCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelEducationCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdatePersonnelEducationCatalogItemCommandValidator : AbstractValidator<UpdatePersonnelEducationCatalogItemCommand>
{
    public UpdatePersonnelEducationCatalogItemCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelEducationCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivatePersonnelEducationCatalogItemCommandValidator : AbstractValidator<ActivatePersonnelEducationCatalogItemCommand>
{
    public ActivatePersonnelEducationCatalogItemCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivatePersonnelEducationCatalogItemCommandValidator : AbstractValidator<InactivatePersonnelEducationCatalogItemCommand>
{
    public InactivatePersonnelEducationCatalogItemCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchPersonnelEducationCatalogItemsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelEducationCatalogRepository repository)
    : IQueryHandler<SearchPersonnelEducationCatalogItemsQuery, PagedResponse<PersonnelEducationCatalogItemResponse>>
{
    public async Task<Result<PagedResponse<PersonnelEducationCatalogItemResponse>>> Handle(
        SearchPersonnelEducationCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PersonnelEducationCatalogItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.CatalogType,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        return Result<PagedResponse<PersonnelEducationCatalogItemResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelEducationCatalogItemByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelEducationCatalogRepository repository)
    : IQueryHandler<GetPersonnelEducationCatalogItemByIdQuery, PersonnelEducationCatalogItemResponse>
{
    public async Task<Result<PersonnelEducationCatalogItemResponse>> Handle(
        GetPersonnelEducationCatalogItemByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.CompanyId, query.CatalogType, query.Id, cancellationToken);
        if (response is not null)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Success(response);
        }

        if (await repository.ExistsOutsideTenantAsync(query.CatalogType, query.Id, cancellationToken))
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(authorizationService.TenantMismatch(RbacPermissionAction.Read));
        }

        return Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogItemNotFound);
    }
}

internal sealed class CreatePersonnelEducationCatalogItemCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelEducationCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePersonnelEducationCatalogItemCommand, PersonnelEducationCatalogItemResponse>
{
    public async Task<Result<PersonnelEducationCatalogItemResponse>> Handle(
        CreatePersonnelEducationCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(command.CompanyId, command.CatalogType, normalizedCode, excludingId: null, cancellationToken))
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogCodeConflict);
        }

        var companyCountry = await repository.GetCompanyCountryAsync(command.CompanyId, cancellationToken);
        if (companyCountry is null)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogItemNotFound);
        }

        var entity = PersonnelEducationCatalogEntityFactory.CreateEntity(
            command.CatalogType,
            companyCountry.CountryCatalogItemId,
            companyCountry.CountryCode,
            command.Code,
            command.Name,
            command.SortOrder);
        repository.Add(entity);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.CompanyId, command.CatalogType, entity.PublicId, cancellationToken);
        return response is null
            ? Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogItemNotFound)
            : Result<PersonnelEducationCatalogItemResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelEducationCatalogItemCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelEducationCatalogRepository repository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelEducationCatalogItemCommand, PersonnelEducationCatalogItemResponse>
{
    public async Task<Result<PersonnelEducationCatalogItemResponse>> Handle(
        UpdatePersonnelEducationCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var entity = await repository.GetByIdAsync(command.CatalogType, command.Id, cancellationToken);
        if (entity is null)
        {
            if (await repository.ExistsOutsideTenantAsync(command.CatalogType, command.Id, cancellationToken))
            {
                return Result<PersonnelEducationCatalogItemResponse>.Failure(authorizationService.TenantMismatch(RbacPermissionAction.Update));
            }

            return Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogItemNotFound);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.ConcurrencyConflict);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(tenantContext.TenantId.Value, command.CatalogType, normalizedCode, entity.Id, cancellationToken))
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogCodeConflict);
        }

        entity.Update(entity.CountryCatalogItemId, entity.CountryCode, command.Code, command.Name, command.SortOrder);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(tenantContext.TenantId.Value, command.CatalogType, command.Id, cancellationToken);
        return response is null
            ? Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogItemNotFound)
            : Result<PersonnelEducationCatalogItemResponse>.Success(response);
    }
}

internal sealed class ActivatePersonnelEducationCatalogItemCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelEducationCatalogRepository repository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivatePersonnelEducationCatalogItemCommand, PersonnelEducationCatalogItemResponse>
{
    public async Task<Result<PersonnelEducationCatalogItemResponse>> Handle(
        ActivatePersonnelEducationCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var entity = await repository.GetByIdAsync(command.CatalogType, command.Id, cancellationToken);
        if (entity is null)
        {
            if (await repository.ExistsOutsideTenantAsync(command.CatalogType, command.Id, cancellationToken))
            {
                return Result<PersonnelEducationCatalogItemResponse>.Failure(authorizationService.TenantMismatch(RbacPermissionAction.Update));
            }

            return Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogItemNotFound);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.ConcurrencyConflict);
        }

        entity.Activate();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(tenantContext.TenantId.Value, command.CatalogType, command.Id, cancellationToken);
        return response is null
            ? Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogItemNotFound)
            : Result<PersonnelEducationCatalogItemResponse>.Success(response);
    }
}

internal sealed class InactivatePersonnelEducationCatalogItemCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelEducationCatalogRepository repository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivatePersonnelEducationCatalogItemCommand, PersonnelEducationCatalogItemResponse>
{
    public async Task<Result<PersonnelEducationCatalogItemResponse>> Handle(
        InactivatePersonnelEducationCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var entity = await repository.GetByIdAsync(command.CatalogType, command.Id, cancellationToken);
        if (entity is null)
        {
            if (await repository.ExistsOutsideTenantAsync(command.CatalogType, command.Id, cancellationToken))
            {
                return Result<PersonnelEducationCatalogItemResponse>.Failure(authorizationService.TenantMismatch(RbacPermissionAction.Update));
            }

            return Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogItemNotFound);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.ConcurrencyConflict);
        }

        if (await repository.IsInUseAsync(command.CatalogType, entity.Id, cancellationToken))
        {
            return Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogItemInUse);
        }

        entity.Inactivate();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(tenantContext.TenantId.Value, command.CatalogType, command.Id, cancellationToken);
        return response is null
            ? Result<PersonnelEducationCatalogItemResponse>.Failure(PersonnelEducationCatalogErrors.CatalogItemNotFound)
            : Result<PersonnelEducationCatalogItemResponse>.Success(response);
    }
}

internal static class PersonnelEducationCatalogEntityFactory
{
    public static PersonnelEducationCatalogItem CreateEntity(
        PersonnelEducationCatalogType catalogType,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder) =>
        catalogType switch
        {
            PersonnelEducationCatalogType.EducationStatus => EducationStatusCatalogItem.Create(countryCatalogItemId, countryCode, code, name, sortOrder),
            PersonnelEducationCatalogType.StudyType => EducationStudyTypeCatalogItem.Create(countryCatalogItemId, countryCode, code, name, sortOrder),
            PersonnelEducationCatalogType.Career => EducationCareerCatalogItem.Create(countryCatalogItemId, countryCode, code, name, sortOrder),
            PersonnelEducationCatalogType.Shift => EducationShiftCatalogItem.Create(countryCatalogItemId, countryCode, code, name, sortOrder),
            PersonnelEducationCatalogType.Modality => EducationModalityCatalogItem.Create(countryCatalogItemId, countryCode, code, name, sortOrder),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported personnel education catalog type.")
        };
}
