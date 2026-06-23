using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelEducationCatalogReferenceResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsActive);

public sealed record PersonnelCatalogItemResponse(
    Guid Id,
    string Category,
    string Code,
    string Name,
    bool IsSystem,
    bool IsActive,
    int SortOrder);

public sealed record PersonnelReferenceCatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    int SortOrder);

public sealed record GetPersonnelCatalogItemsQuery(string Category, string? CountryCode = null) : IQuery<IReadOnlyCollection<PersonnelCatalogItemResponse>>;

public sealed record GetPersonnelReferenceCatalogItemsQuery(
    string Category,
    string CountryCode,
    string? ParentCode = null) : IQuery<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>;

internal sealed class GetPersonnelCatalogItemsQueryValidator : AbstractValidator<GetPersonnelCatalogItemsQuery>
{
    public GetPersonnelCatalogItemsQueryValidator()
    {
        RuleFor(query => query.Category).NotEmpty().MaximumLength(80);

        // countryCode is optional here: system-scoped catalogs (document types, education-*) ignore it,
        // country-scoped ones (banks, currencies, languages) use it. Only the format is constrained.
        RuleFor(query => query.CountryCode)
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .WithMessage("Country code must be a 2 or 3 letter ISO-style code.")
            .When(query => !string.IsNullOrWhiteSpace(query.CountryCode));
    }
}

internal sealed class GetPersonnelReferenceCatalogItemsQueryValidator : AbstractValidator<GetPersonnelReferenceCatalogItemsQuery>
{
    public GetPersonnelReferenceCatalogItemsQueryValidator()
    {
        RuleFor(query => query.Category)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .WithMessage("Category format is invalid.");

        // Reference catalogs are country-scoped, so the country code is required (supplied explicitly now
        // that the route carries no company).
        RuleFor(query => query.CountryCode)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .WithMessage("Country code must be a 2 or 3 letter ISO-style code.");
        RuleFor(query => query.ParentCode)
            .MaximumLength(120)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .When(query => !string.IsNullOrWhiteSpace(query.ParentCode))
            .WithMessage("ParentCode format is invalid.");
    }
}

internal static class PersonnelCurriculumCatalogCategories
{
    public const string EducationStatus = "CurriculumEducationStatus";
    public const string StudyType = "CurriculumStudyType";
    public const string Shift = "CurriculumShift";
    public const string Modality = "CurriculumModality";
    public const string Language = "CurriculumLanguage";
    public const string LanguageLevel = "CurriculumLanguageLevel";
    public const string TrainingType = "CurriculumTrainingType";
    public const string DurationUnit = "CurriculumDurationUnit";
    public const string ReferenceType = "CurriculumReferenceType";
    public const string AssignmentType = "CurriculumAssignmentType";
    public const string SubstitutionType = "CurriculumSubstitutionType";
    public const string EmploymentStatus = "EmploymentStatus";
    public const string Career = "CurriculumCareer";
    public const string Country = "Country";
    public const string Currency = "Currency";
    public const string FileDocumentType = "FileDocumentType";
    public const string Bank = "Bank";
    public const string CompensationConceptType = "CompensationConceptType";
    public const string PayPeriod = "PayPeriod";
    public const string CalculationBase = "CalculationBase";
    public const string PaymentMethod = "PaymentMethod";
    public const string AssetAccessType = "AssetAccessType";
    public const string DeliveryStatus = "DeliveryStatus";
}

internal static class PersonnelCurriculumCatalogValidation
{
    public static async Task<Error> ValidateCodeAsync(
        IPersonnelFileRepository repository,
        Guid tenantId,
        string fieldName,
        string category,
        string code,
        CancellationToken cancellationToken)
    {
        var isActive = await repository.CatalogCodeIsActiveAsync(tenantId, category, code, cancellationToken);
        return isActive
            ? Error.None
            : ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    [fieldName] = [$"Catalog code '{code}' is not active for category '{category}'."]
                });
    }
}

internal static class PersonnelReferenceCatalogValidation
{
    public static async Task<Error> ValidatePersonalInfoCodesAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string? maritalStatusCode,
        string? professionCode,
        string? birthCountryCode,
        string? birthDepartmentCode,
        string? birthMunicipalityCode,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(maritalStatusCode))
        {
            var statusError = await ValidateOptionalReferenceCodeAsync(
                repository,
                "maritalStatusCode",
                await ResolveCompanyCountryCodeAsync(repository, companyId, cancellationToken),
                PersonnelReferenceCatalogCategories.MaritalStatus,
                maritalStatusCode,
                cancellationToken);
            if (statusError != Error.None)
            {
                return statusError;
            }
        }

        if (!string.IsNullOrWhiteSpace(professionCode))
        {
            var professionError = await ValidateOptionalReferenceCodeAsync(
                repository,
                "professionCode",
                await ResolveCompanyCountryCodeAsync(repository, companyId, cancellationToken),
                PersonnelReferenceCatalogCategories.Profession,
                professionCode,
                cancellationToken);
            if (professionError != Error.None)
            {
                return professionError;
            }
        }

        return await ValidateBirthLocationAsync(
            repository,
            birthCountryCode,
            birthDepartmentCode,
            birthMunicipalityCode,
            cancellationToken);
    }

    public static Task<Error> ValidateIdentificationTypeCodeAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string identificationTypeCode,
        CancellationToken cancellationToken) =>
        ValidateOptionalReferenceCodeForCompanyAsync(
            repository,
            companyId,
            "identificationTypeCode",
            PersonnelReferenceCatalogCategories.IdentificationType,
            identificationTypeCode,
            cancellationToken);

    public static Task<Error> ValidateKinshipCodeAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string fieldName,
        string kinshipCode,
        CancellationToken cancellationToken) =>
        ValidateOptionalReferenceCodeForCompanyAsync(
            repository,
            companyId,
            fieldName,
            PersonnelReferenceCatalogCategories.Kinship,
            kinshipCode,
            cancellationToken);

    private static async Task<Error> ValidateBirthLocationAsync(
        IPersonnelFileRepository repository,
        string? birthCountryCode,
        string? birthDepartmentCode,
        string? birthMunicipalityCode,
        CancellationToken cancellationToken)
    {
        var normalizedCountry = string.IsNullOrWhiteSpace(birthCountryCode)
            ? null
            : birthCountryCode.Trim().ToUpperInvariant();
        var normalizedDepartment = string.IsNullOrWhiteSpace(birthDepartmentCode)
            ? null
            : birthDepartmentCode.Trim().ToUpperInvariant();
        var normalizedMunicipality = string.IsNullOrWhiteSpace(birthMunicipalityCode)
            ? null
            : birthMunicipalityCode.Trim().ToUpperInvariant();

        if (normalizedDepartment is not null && normalizedCountry is null)
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthCountryCode"] = ["BirthCountryCode is required when BirthDepartmentCode is provided."]
                });
        }

        if (normalizedMunicipality is not null && normalizedDepartment is null)
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthDepartmentCode"] = ["BirthDepartmentCode is required when BirthMunicipalityCode is provided."]
                });
        }

        if (normalizedCountry is null)
        {
            return Error.None;
        }

        if (!await repository.CountryCodeIsActiveAsync(normalizedCountry, cancellationToken))
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthCountryCode"] = [$"Country code '{normalizedCountry}' is not active."]
                });
        }

        if (normalizedDepartment is null && normalizedMunicipality is null)
        {
            return Error.None;
        }

        if (normalizedCountry != LocationValidationRules.ElSalvadorCountryCode)
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthCountryCode"] = ["Birth department and municipality catalogs are only available for country code 'SV' in this phase."]
                });
        }

        if (normalizedDepartment is not null &&
            !await repository.ReferenceCatalogCodeIsActiveAsync(
                normalizedCountry,
                PersonnelReferenceCatalogCategories.Department,
                normalizedDepartment,
                cancellationToken))
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthDepartmentCode"] = [$"Catalog code '{normalizedDepartment}' is not active for category '{PersonnelReferenceCatalogCategories.Department}'."]
                });
        }

        if (normalizedMunicipality is not null &&
            !await repository.ReferenceCatalogCodeIsActiveAsync(
                normalizedCountry,
                PersonnelReferenceCatalogCategories.Municipality,
                normalizedMunicipality,
                cancellationToken))
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthMunicipalityCode"] = [$"Catalog code '{normalizedMunicipality}' is not active for category '{PersonnelReferenceCatalogCategories.Municipality}'."]
                });
        }

        if (normalizedDepartment is not null &&
            normalizedMunicipality is not null &&
            !await repository.ReferenceMunicipalityBelongsToDepartmentAsync(
                normalizedCountry,
                normalizedDepartment,
                normalizedMunicipality,
                cancellationToken))
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["birthMunicipalityCode"] = ["BirthMunicipalityCode does not belong to the selected BirthDepartmentCode."]
                });
        }

        return Error.None;
    }

    private static async Task<Error> ValidateOptionalReferenceCodeAsync(
        IPersonnelFileRepository repository,
        string fieldName,
        string countryCode,
        string category,
        string? code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Error.None;
        }

        var isActive = await repository.ReferenceCatalogCodeIsActiveAsync(countryCode, category, code, cancellationToken);
        return isActive
            ? Error.None
            : ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    [fieldName] = [$"Catalog code '{code.Trim().ToUpperInvariant()}' is not active for category '{category}'."]
                });
    }

    private static async Task<Error> ValidateOptionalReferenceCodeForCompanyAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string fieldName,
        string category,
        string? code,
        CancellationToken cancellationToken)
    {
        var companyCountryCode = await ResolveCompanyCountryCodeAsync(repository, companyId, cancellationToken);
        return await ValidateOptionalReferenceCodeAsync(
            repository,
            fieldName,
            companyCountryCode,
            category,
            code,
            cancellationToken);
    }

    private static async Task<string> ResolveCompanyCountryCodeAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var countryCode = await repository.GetCompanyCountryCodeAsync(companyId, cancellationToken);
        return string.IsNullOrWhiteSpace(countryCode)
            ? LocationValidationRules.ElSalvadorCountryCode
            : countryCode.Trim().ToUpperInvariant();
    }
}

// Read authz is authn-only: these are global / country reference catalogs (not tenant data) consumed
// before a company exists (onboarding) and on every form load. The controller's [Authorize] is the only
// gate — mirror AccountCompanyCatalogsController — so no per-company authorization service is involved.
internal sealed class GetPersonnelCatalogItemsQueryHandler(
    IPersonnelFileRepository repository)
    : IQueryHandler<GetPersonnelCatalogItemsQuery, IReadOnlyCollection<PersonnelCatalogItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelCatalogItemResponse>>> Handle(
        GetPersonnelCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetCatalogItemsAsync(query.CountryCode, query.Category, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelCatalogItemResponse>>.Success(items);
    }
}

internal sealed class GetPersonnelReferenceCatalogItemsQueryHandler(
    IPersonnelFileRepository repository)
    : IQueryHandler<GetPersonnelReferenceCatalogItemsQuery, IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>> Handle(
        GetPersonnelReferenceCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetReferenceCatalogItemsAsync(
            query.CountryCode,
            query.Category,
            query.ParentCode,
            cancellationToken);
        return Result<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>.Success(items);
    }
}

