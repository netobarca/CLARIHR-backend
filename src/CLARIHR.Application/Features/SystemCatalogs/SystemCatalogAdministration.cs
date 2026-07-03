using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Abstractions.SystemCatalogs;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.Countries;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.GeneralCatalogs;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.SystemCatalogs;

public enum SystemCatalogType
{
    Language,
    LanguageLevel,
    TrainingType,
    DurationUnit,
    ReferenceType,
    Currency,
    EducationStatus,
    EducationStudyType,
    EducationCareer,
    EducationShift,
    EducationModality,
    IdentificationType,
    Profession,
    MaritalStatus,
    Kinship,
    Department,
    Municipality,
    PersonalTitle,
    AddressType,
    Hobby,
    Association,
    AdditionalBenefitType
}

public sealed record SystemCatalogItemResponse(
    Guid Id,
    SystemCatalogType CatalogType,
    string CountryCode,
    string Code,
    string Name,
    bool IsActive,
    int SortOrder,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    Guid? ParentId,
    string? ParentCode,
    string? ParentName);

public sealed record DepartmentCatalogLookup(
    long InternalId,
    Guid PublicId,
    long CountryCatalogItemId,
    string CountryCode,
    string Code,
    string Name,
    bool IsActive);

public sealed record SearchSystemCatalogItemsQuery(
    SystemCatalogType CatalogType,
    string CountryCode,
    bool? IsActive,
    string? Search,
    Guid? ParentId,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedResponse<SystemCatalogItemResponse>>;

public sealed record GetSystemCatalogItemByIdQuery(
    SystemCatalogType CatalogType,
    Guid Id)
    : IQuery<SystemCatalogItemResponse>;

public sealed record CreateSystemCatalogItemCommand(
    SystemCatalogType CatalogType,
    string CountryCode,
    string Code,
    string Name,
    int SortOrder,
    Guid? ParentId = null)
    : ICommand<SystemCatalogItemResponse>;

public sealed record UpdateSystemCatalogItemCommand(
    SystemCatalogType CatalogType,
    Guid Id,
    string CountryCode,
    string Code,
    string Name,
    int SortOrder,
    Guid ConcurrencyToken,
    Guid? ParentId = null)
    : ICommand<SystemCatalogItemResponse>;

public sealed record ActivateSystemCatalogItemCommand(
    SystemCatalogType CatalogType,
    Guid Id,
    Guid ConcurrencyToken)
    : ICommand<SystemCatalogItemResponse>;

public sealed record InactivateSystemCatalogItemCommand(
    SystemCatalogType CatalogType,
    Guid Id,
    Guid ConcurrencyToken)
    : ICommand<SystemCatalogItemResponse>;

internal sealed class SearchSystemCatalogItemsQueryValidator : AbstractValidator<SearchSystemCatalogItemsQuery>
{
    public SearchSystemCatalogItemsQueryValidator()
    {
        RuleFor(query => query.CountryCode)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$");
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetSystemCatalogItemByIdQueryValidator : AbstractValidator<GetSystemCatalogItemByIdQuery>
{
    public GetSystemCatalogItemByIdQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}

internal sealed class CreateSystemCatalogItemCommandValidator : AbstractValidator<CreateSystemCatalogItemCommand>
{
    public CreateSystemCatalogItemCommandValidator()
    {
        RuleFor(command => command.CountryCode)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$");
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Matches("^[A-Za-z0-9_]+$");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateSystemCatalogItemCommandValidator : AbstractValidator<UpdateSystemCatalogItemCommand>
{
    public UpdateSystemCatalogItemCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.CountryCode)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$");
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Matches("^[A-Za-z0-9_]+$");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateSystemCatalogItemCommandValidator : AbstractValidator<ActivateSystemCatalogItemCommand>
{
    public ActivateSystemCatalogItemCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateSystemCatalogItemCommandValidator : AbstractValidator<InactivateSystemCatalogItemCommand>
{
    public InactivateSystemCatalogItemCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchSystemCatalogItemsQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICountryCatalogRepository countryCatalogRepository,
    ISystemCatalogRepository repository)
    : IQueryHandler<SearchSystemCatalogItemsQuery, PagedResponse<SystemCatalogItemResponse>>
{
    public async Task<Result<PagedResponse<SystemCatalogItemResponse>>> Handle(
        SearchSystemCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<SystemCatalogItemResponse>>.Failure(authorizationResult.Error);
        }

        var country = await countryCatalogRepository.GetActiveByCodeAsync(query.CountryCode, cancellationToken);
        if (country is null)
        {
            return Result<PagedResponse<SystemCatalogItemResponse>>.Failure(SystemCatalogErrors.CountryNotFound(query.CountryCode));
        }

        var response = await repository.SearchAsync(
            query.CatalogType,
            country.InternalId,
            query.IsActive,
            query.Search,
            query.ParentId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<SystemCatalogItemResponse>>.Success(response);
    }
}

internal sealed class GetSystemCatalogItemByIdQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ISystemCatalogRepository repository)
    : IQueryHandler<GetSystemCatalogItemByIdQuery, SystemCatalogItemResponse>
{
    public async Task<Result<SystemCatalogItemResponse>> Handle(
        GetSystemCatalogItemByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SystemCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.CatalogType, query.Id, cancellationToken);
        return response is null
            ? Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.NotFound)
            : Result<SystemCatalogItemResponse>.Success(response);
    }
}

internal sealed class CreateSystemCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICountryCatalogRepository countryCatalogRepository,
    ISystemCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateSystemCatalogItemCommand, SystemCatalogItemResponse>
{
    public async Task<Result<SystemCatalogItemResponse>> Handle(
        CreateSystemCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SystemCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var country = await countryCatalogRepository.GetActiveByCodeAsync(command.CountryCode, cancellationToken);
        if (country is null)
        {
            return Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.CountryNotFound(command.CountryCode));
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.ExistsByCodeAsync(command.CatalogType, country.InternalId, normalizedCode, excludingId: null, cancellationToken))
        {
            return Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.CodeConflict);
        }

        var parent = await SystemCatalogParentResolver.ResolveParentAsync(command.CatalogType, country, command.ParentId, repository, cancellationToken);
        if (parent.IsFailure)
        {
            return Result<SystemCatalogItemResponse>.Failure(parent.Error);
        }

        var entity = SystemCatalogFactory.Create(
            command.CatalogType,
            country.InternalId,
            country.Code,
            command.Code,
            command.Name,
            command.SortOrder,
            parent.Value?.InternalId);

        repository.Add(entity);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.CatalogType, entity.PublicId, cancellationToken);
        return response is null
            ? Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.NotFound)
            : Result<SystemCatalogItemResponse>.Success(response);
    }
}

internal sealed class UpdateSystemCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICountryCatalogRepository countryCatalogRepository,
    ISystemCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateSystemCatalogItemCommand, SystemCatalogItemResponse>
{
    public async Task<Result<SystemCatalogItemResponse>> Handle(
        UpdateSystemCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SystemCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.CatalogType, command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.ConcurrencyConflict);
        }

        if (!string.Equals(entity.CountryCode, command.CountryCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.CountryChangeForbidden);
        }

        var country = await countryCatalogRepository.GetActiveByCodeAsync(command.CountryCode, cancellationToken);
        if (country is null)
        {
            return Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.CountryNotFound(command.CountryCode));
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.ExistsByCodeAsync(command.CatalogType, country.InternalId, normalizedCode, entity.Id, cancellationToken))
        {
            return Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.CodeConflict);
        }

        var parent = await SystemCatalogParentResolver.ResolveParentAsync(command.CatalogType, country, command.ParentId, repository, cancellationToken);
        if (parent.IsFailure)
        {
            return Result<SystemCatalogItemResponse>.Failure(parent.Error);
        }

        if (entity is MunicipalityCatalogItem municipality)
        {
            municipality.Update(country.InternalId, country.Code, command.Code, command.Name, command.SortOrder, parent.Value!.InternalId);
        }
        else
        {
            entity.Update(country.InternalId, country.Code, command.Code, command.Name, command.SortOrder);
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.CatalogType, command.Id, cancellationToken);
        return response is null
            ? Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.NotFound)
            : Result<SystemCatalogItemResponse>.Success(response);
    }
}

internal sealed class ActivateSystemCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ISystemCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateSystemCatalogItemCommand, SystemCatalogItemResponse>
{
    public async Task<Result<SystemCatalogItemResponse>> Handle(
        ActivateSystemCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SystemCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.CatalogType, command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.ConcurrencyConflict);
        }

        entity.Activate();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.CatalogType, command.Id, cancellationToken);
        return response is null
            ? Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.NotFound)
            : Result<SystemCatalogItemResponse>.Success(response);
    }
}

internal sealed class InactivateSystemCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ISystemCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateSystemCatalogItemCommand, SystemCatalogItemResponse>
{
    public async Task<Result<SystemCatalogItemResponse>> Handle(
        InactivateSystemCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SystemCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.CatalogType, command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.ConcurrencyConflict);
        }

        entity.Inactivate();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.CatalogType, command.Id, cancellationToken);
        return response is null
            ? Result<SystemCatalogItemResponse>.Failure(SystemCatalogErrors.NotFound)
            : Result<SystemCatalogItemResponse>.Success(response);
    }
}

internal static class SystemCatalogFactory
{
    public static CountryScopedCatalogItem Create(
        SystemCatalogType catalogType,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder,
        long? parentInternalId)
        => catalogType switch
        {
            SystemCatalogType.Language => LanguageCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.LanguageLevel => LanguageLevelCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.TrainingType => TrainingTypeCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.DurationUnit => DurationUnitCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.ReferenceType => ReferenceTypeCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.Currency => CurrencyCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.IdentificationType => IdentificationTypeCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.Profession => ProfessionCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.MaritalStatus => MaritalStatusCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.Kinship => KinshipCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.Department => DepartmentCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.PersonalTitle => PersonalTitleCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.AddressType => AddressTypeCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.Hobby => HobbyCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.Association => AssociationCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.AdditionalBenefitType => AdditionalBenefitTypeCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder),
            SystemCatalogType.Municipality when parentInternalId.HasValue => MunicipalityCatalogItem.Create(countryCatalogItemId, countryCode, code, name, true, sortOrder, parentInternalId.Value),
            SystemCatalogType.Municipality => throw new InvalidOperationException("Municipality requires a department parent."),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported system catalog type.")
        };
}

internal static class SystemCatalogErrors
{
    public static readonly Error NotFound = new("SYSTEM_CATALOG_NOT_FOUND", "The system catalog item could not be found.", ErrorType.NotFound);
    public static readonly Error CodeConflict = new("SYSTEM_CATALOG_CODE_CONFLICT", "Another system catalog item already uses the requested code for that country.", ErrorType.Conflict);
    public static readonly Error ParentRequired = new("SYSTEM_CATALOG_PARENT_REQUIRED", "This catalog requires a parent department.", ErrorType.Validation);
    public static readonly Error ParentNotFound = new("SYSTEM_CATALOG_PARENT_NOT_FOUND", "The requested parent department could not be found.", ErrorType.NotFound);
    public static readonly Error ParentCountryMismatch = new("SYSTEM_CATALOG_PARENT_COUNTRY_MISMATCH", "The selected parent department belongs to a different country.", ErrorType.Validation);
    public static readonly Error ConcurrencyConflict = new("CONCURRENCY_CONFLICT", "The resource was modified by another request. Refresh and try again.", ErrorType.Conflict);
    public static readonly Error CountryChangeForbidden = new("SYSTEM_CATALOG_COUNTRY_CHANGE_FORBIDDEN", "Changing the country of an existing system catalog item is not supported.", ErrorType.Validation);

    public static Error CountryNotFound(string countryCode) =>
        new(
            "SYSTEM_CATALOG_COUNTRY_NOT_FOUND",
            $"Country '{countryCode.Trim().ToUpperInvariant()}' is not active.",
            ErrorType.NotFound,
            MessageArguments: [countryCode.Trim().ToUpperInvariant()]);
}

internal static class SystemCatalogParentResolver
{
    public static async Task<Result<DepartmentCatalogLookup?>> ResolveParentAsync(
        SystemCatalogType catalogType,
        CountryCatalogLookup country,
        Guid? parentId,
        ISystemCatalogRepository repository,
        CancellationToken cancellationToken)
    {
        if (catalogType != SystemCatalogType.Municipality)
        {
            return Result<DepartmentCatalogLookup?>.Success(null);
        }

        if (!parentId.HasValue)
        {
            return Result<DepartmentCatalogLookup?>.Failure(SystemCatalogErrors.ParentRequired);
        }

        var parent = await repository.GetDepartmentLookupByIdAsync(parentId.Value, cancellationToken);
        if (parent is null)
        {
            return Result<DepartmentCatalogLookup?>.Failure(SystemCatalogErrors.ParentNotFound);
        }

        if (parent.CountryCatalogItemId != country.InternalId)
        {
            return Result<DepartmentCatalogLookup?>.Failure(SystemCatalogErrors.ParentCountryMismatch);
        }

        return Result<DepartmentCatalogLookup?>.Success(parent);
    }
}

internal static class SystemCatalogKeyMap
{
    public static bool TryParse(string key, out SystemCatalogType catalogType)
    {
        switch (key.Trim().ToLowerInvariant())
        {
            case "languages":
                catalogType = SystemCatalogType.Language;
                return true;
            case "language-levels":
                catalogType = SystemCatalogType.LanguageLevel;
                return true;
            case "training-types":
                catalogType = SystemCatalogType.TrainingType;
                return true;
            case "duration-units":
                catalogType = SystemCatalogType.DurationUnit;
                return true;
            case "reference-types":
                catalogType = SystemCatalogType.ReferenceType;
                return true;
            case "currencies":
                catalogType = SystemCatalogType.Currency;
                return true;
            case "education-statuses":
                catalogType = SystemCatalogType.EducationStatus;
                return true;
            case "education-study-types":
                catalogType = SystemCatalogType.EducationStudyType;
                return true;
            case "education-careers":
                catalogType = SystemCatalogType.EducationCareer;
                return true;
            case "education-shifts":
                catalogType = SystemCatalogType.EducationShift;
                return true;
            case "education-modalities":
                catalogType = SystemCatalogType.EducationModality;
                return true;
            case "identification-types":
                catalogType = SystemCatalogType.IdentificationType;
                return true;
            case "professions":
                catalogType = SystemCatalogType.Profession;
                return true;
            case "marital-statuses":
                catalogType = SystemCatalogType.MaritalStatus;
                return true;
            case "kinships":
                catalogType = SystemCatalogType.Kinship;
                return true;
            case "departments":
                catalogType = SystemCatalogType.Department;
                return true;
            case "municipalities":
                catalogType = SystemCatalogType.Municipality;
                return true;
            case "personal-titles":
                catalogType = SystemCatalogType.PersonalTitle;
                return true;
            case "address-types":
                catalogType = SystemCatalogType.AddressType;
                return true;
            case "hobbies":
                catalogType = SystemCatalogType.Hobby;
                return true;
            case "associations":
                catalogType = SystemCatalogType.Association;
                return true;
            case "additional-benefit-types":
                catalogType = SystemCatalogType.AdditionalBenefitType;
                return true;
            default:
                catalogType = default;
                return false;
        }
    }
}
