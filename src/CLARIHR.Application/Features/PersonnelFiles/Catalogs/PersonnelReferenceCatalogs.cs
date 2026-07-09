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
    public const string MedicalClaimType = "CurriculumMedicalClaimType";
    public const string MedicalClaimStatus = "CurriculumMedicalClaimStatus";
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
    public const string BankAccountType = "BankAccountType";
    public const string AssetAccessType = "AssetAccessType";
    public const string DeliveryStatus = "DeliveryStatus";
    public const string ExperienceMetric = "ExperienceMetric";
    public const string OffPayrollTransactionType = "OffPayrollTransactionType";
    public const string FormControlType = "FormControlType";
    public const string ContractType = "ContractType";
    public const string ActionType = "ActionType";
    public const string ActionStatus = "ActionStatus";
    public const string EconomicAidType = "EconomicAidType";
    public const string EconomicAidStatus = "EconomicAidStatus";
    public const string CertificateType = "CertificateType";
    public const string CertificateRequestStatus = "CertificateRequestStatus";
    public const string CertificateDeliveryMethod = "CertificateDeliveryMethod";
    public const string CertificatePurpose = "CertificatePurpose";
    public const string Hobby = "Hobby";
    public const string Association = "Association";
    public const string AdditionalBenefitType = "AdditionalBenefitType";
    public const string EducationLevel = "CurriculumEducationLevel";
    public const string Afp = "Afp";
    public const string RetirementRequestStatus = "RetirementRequestStatus";
    public const string SettlementStatus = "SettlementStatus";
    public const string SettlementConcept = "SettlementConcept";
    public const string ClinicSector = "ClinicSector";
    public const string IncapacityStatus = "IncapacityStatus";
    public const string VacationRequestStatus = "VacationRequestStatus";
    public const string CompensatoryTimeStatus = "CompensatoryTimeStatus";
    public const string CompensatoryTimeOperation = "CompensatoryTimeOperation";
    public const string PersonnelTransactionStatus = "PersonnelTransactionStatus";
    public const string PayrollType = "PayrollType";
    public const string RecurringIncomeStatus = "RecurringIncomeStatus";
    public const string RecurringIncomeSettlementAction = "RecurringIncomeSettlementAction";
    public const string RecurringIncomeType = "RecurringIncomeType";
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
        CancellationToken cancellationToken,
        string? personalTitleCode = null)
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

        if (!string.IsNullOrWhiteSpace(personalTitleCode))
        {
            var personalTitleError = await ValidateOptionalReferenceCodeAsync(
                repository,
                "personalTitleCode",
                await ResolveCompanyCountryCodeAsync(repository, companyId, cancellationToken),
                PersonnelReferenceCatalogCategories.PersonalTitle,
                personalTitleCode,
                cancellationToken);
            if (personalTitleError != Error.None)
            {
                return personalTitleError;
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

    /// <summary>
    /// Validates the identification number against the anchored regex configured for the
    /// identification type (RF-003). Types without a configured format keep the generic
    /// validation only (no-op). Runs against the trimmed, upper-cased number (the same
    /// normalization the entity stores). An unparsable/pathological pattern fails safe (no-op)
    /// so a bad admin regex can never block writes.
    /// </summary>
    public static async Task<Error> ValidateIdentificationNumberFormatAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string identificationTypeCode,
        string identificationNumber,
        CancellationToken cancellationToken)
    {
        var pattern = await repository.GetIdentificationTypeNumberFormatAsync(companyId, identificationTypeCode, cancellationToken);
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return Error.None;
        }

        var normalizedNumber = identificationNumber.Trim().ToUpperInvariant();
        try
        {
            if (!global::System.Text.RegularExpressions.Regex.IsMatch(
                normalizedNumber,
                pattern,
                global::System.Text.RegularExpressions.RegexOptions.None,
                TimeSpan.FromMilliseconds(250)))
            {
                return PersonnelFileErrors.IdentificationNumberFormatInvalid;
            }
        }
        catch (ArgumentException)
        {
            return Error.None;
        }
        catch (global::System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return Error.None;
        }

        return Error.None;
    }

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

    public static Task<Error> ValidateAddressTypeCodeAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string fieldName,
        string? addressTypeCode,
        CancellationToken cancellationToken) =>
        ValidateOptionalReferenceCodeForCompanyAsync(
            repository,
            companyId,
            fieldName,
            PersonnelReferenceCatalogCategories.AddressType,
            addressTypeCode,
            cancellationToken);

    public static Task<Error> ValidateInsuranceTypeCodeAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string fieldName,
        string insuranceTypeCode,
        CancellationToken cancellationToken) =>
        ValidateOptionalReferenceCodeForCompanyAsync(
            repository,
            companyId,
            fieldName,
            PersonnelReferenceCatalogCategories.InsuranceType,
            insuranceTypeCode,
            cancellationToken);

    public static async Task<Error> ValidateInsuranceRangeCodeAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string insuranceTypeCode,
        string? rangeCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rangeCode))
        {
            return Error.None;
        }

        var countryCode = await ResolveCompanyCountryCodeAsync(repository, companyId, cancellationToken);
        var existsError = await ValidateOptionalReferenceCodeAsync(
            repository,
            "rangeCode",
            countryCode,
            PersonnelReferenceCatalogCategories.InsuranceRange,
            rangeCode,
            cancellationToken);
        if (existsError != Error.None)
        {
            return existsError;
        }

        var belongs = await repository.ReferenceInsuranceRangeBelongsToTypeAsync(
            countryCode, insuranceTypeCode, rangeCode, cancellationToken);
        return belongs
            ? Error.None
            : ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["rangeCode"] = ["RangeCode does not belong to the selected insurance type."]
                });
    }

    // Retirement "motivo de baja": category + reason are a hierarchy (D-02). Both optional, but a reason
    // requires its category, the category/reason must be active for the country, and the reason must belong
    // to the category. Mirrors ValidateInsuranceRangeCodeAsync (type → range).
    public static async Task<Error> ValidateRetirementCodesAsync(
        IPersonnelFileRepository repository,
        Guid companyId,
        string? retirementCategoryCode,
        string? retirementReasonCode,
        CancellationToken cancellationToken)
    {
        var hasCategory = !string.IsNullOrWhiteSpace(retirementCategoryCode);
        var hasReason = !string.IsNullOrWhiteSpace(retirementReasonCode);
        if (!hasCategory && !hasReason)
        {
            return Error.None;
        }

        if (hasReason && !hasCategory)
        {
            return ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["retirementCategoryCode"] = ["RetirementCategoryCode is required when RetirementReasonCode is provided."]
                });
        }

        var countryCode = await ResolveCompanyCountryCodeAsync(repository, companyId, cancellationToken);

        var categoryError = await ValidateOptionalReferenceCodeAsync(
            repository,
            "retirementCategoryCode",
            countryCode,
            PersonnelReferenceCatalogCategories.RetirementCategory,
            retirementCategoryCode,
            cancellationToken);
        if (categoryError != Error.None)
        {
            return categoryError;
        }

        if (!hasReason)
        {
            return Error.None;
        }

        var reasonError = await ValidateOptionalReferenceCodeAsync(
            repository,
            "retirementReasonCode",
            countryCode,
            PersonnelReferenceCatalogCategories.RetirementReason,
            retirementReasonCode,
            cancellationToken);
        if (reasonError != Error.None)
        {
            return reasonError;
        }

        var belongs = await repository.ReferenceRetirementReasonBelongsToCategoryAsync(
            countryCode, retirementCategoryCode!, retirementReasonCode!, cancellationToken);
        return belongs
            ? Error.None
            : ErrorCatalog.Validation(
                new Dictionary<string, string[]>
                {
                    ["retirementReasonCode"] = ["RetirementReasonCode does not belong to the selected RetirementCategoryCode."]
                });
    }

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

