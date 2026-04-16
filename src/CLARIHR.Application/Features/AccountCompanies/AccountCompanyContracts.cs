using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.LegalRepresentatives;
using FluentValidation;

namespace CLARIHR.Application.Features.AccountCompanies;

public sealed record AccountCompanySummaryResponse(
    Guid PublicId,
    string Name,
    string Slug,
    string CountryCode,
    CompanyStatus Status,
    string PlanCode,
    bool IsActiveContext,
    bool IsOwnedByCurrentUser,
    DateTime CreatedAtUtc,
    CompanyTypeMetadataResponse? CompanyType,
    AllowedActionsResponse? AllowedActions = null)
{
    public string DefaultLocale { get; init; } = "en-US";
}

public sealed record AccountCompanyDetailResponse(
    Guid PublicId,
    string Name,
    string Slug,
    string CountryCode,
    CompanyStatus Status,
    string PlanCode,
    bool IsActiveContext,
    bool IsOwnedByCurrentUser,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    IReadOnlyCollection<ActiveLegalRepresentativeSummaryResponse> ActiveLegalRepresentatives,
    CompanyTypeMetadataResponse? CompanyType)
{
    public string DefaultLocale { get; init; } = "en-US";
}

public sealed record CompanyTypeMetadataResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsActive);

public sealed record ActiveLegalRepresentativeSummaryResponse(
    Guid Id,
    string FullName,
    LegalRepresentativeRepresentationType RepresentationType,
    string PositionTitle,
    bool? IsPrimary);

public sealed record ActiveCompanyDto(
    Guid PublicId,
    string Name,
    string Slug,
    string CountryCode,
    CompanyStatus Status)
{
    public string DefaultLocale { get; init; } = "en-US";
}

public sealed record SwitchActiveCompanyResponse(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    ActiveCompanyDto ActiveCompany,
    AccountCompanyAccessContextResponse AccessContext);

public sealed record CompanyOwnershipCountFilter(
    CompanyStatus[] Statuses);

public sealed record CompanyListFilter(
    CompanyStatus? Status,
    int PageNumber,
    int PageSize,
    Guid? ActiveTenantId);

public sealed record GetOwnedCompaniesQuery(
    CompanyStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<AccountCompanySummaryResponse>>;

public sealed record GetAvailableCompanyTypesQuery(string CountryCode)
    : IQuery<IReadOnlyCollection<CompanyTypeCatalogItemResponse>>;

public sealed record GetOwnedCompanyByIdQuery(Guid CompanyId) : IQuery<AccountCompanyDetailResponse>;

public sealed record CreateAccountCompanyCommand(
    string Name,
    string CountryCode,
    Guid? CompanyTypeId,
    InitialLegalRepresentativeInput InitialLegalRepresentative) : ICommand<AccountCompanyDetailResponse>;

public sealed record UpdateAccountCompanyCommand(Guid CompanyId, string Name, Guid? CompanyTypeId) : ICommand<AccountCompanyDetailResponse>;

public sealed record ArchiveAccountCompanyCommand(Guid CompanyId) : ICommand<AccountCompanyDetailResponse>;

public sealed record ReactivateAccountCompanyCommand(Guid CompanyId) : ICommand<AccountCompanyDetailResponse>;

public sealed record SwitchActiveCompanyCommand(Guid CompanyId) : ICommand<SwitchActiveCompanyResponse>;

internal sealed class GetOwnedCompaniesQueryValidator : AbstractValidator<GetOwnedCompaniesQuery>
{
    public GetOwnedCompaniesQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetAvailableCompanyTypesQueryValidator : AbstractValidator<GetAvailableCompanyTypesQuery>
{
    public GetAvailableCompanyTypesQueryValidator()
    {
        RuleFor(query => query.CountryCode)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .WithMessage("Country code must be a 2 or 3 letter ISO-style code.");
    }
}

internal sealed class GetOwnedCompanyByIdQueryValidator : AbstractValidator<GetOwnedCompanyByIdQuery>
{
    public GetOwnedCompanyByIdQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class CreateAccountCompanyCommandValidator : AbstractValidator<CreateAccountCompanyCommand>
{
    public CreateAccountCompanyCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(150);
        RuleFor(command => command.CountryCode)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .WithMessage("Country code must be a 2 or 3 letter ISO-style code.");
        RuleFor(command => command.CompanyTypeId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CompanyTypeId.HasValue);
        RuleFor(command => command.InitialLegalRepresentative)
            .NotNull()
            .SetValidator(new InitialLegalRepresentativeInputValidator());
    }
}

internal sealed class UpdateAccountCompanyCommandValidator : AbstractValidator<UpdateAccountCompanyCommand>
{
    public UpdateAccountCompanyCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(150);
        RuleFor(command => command.CompanyTypeId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CompanyTypeId.HasValue);
    }
}

internal sealed class ArchiveAccountCompanyCommandValidator : AbstractValidator<ArchiveAccountCompanyCommand>
{
    public ArchiveAccountCompanyCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
    }
}

internal sealed class ReactivateAccountCompanyCommandValidator : AbstractValidator<ReactivateAccountCompanyCommand>
{
    public ReactivateAccountCompanyCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
    }
}

internal sealed class SwitchActiveCompanyCommandValidator : AbstractValidator<SwitchActiveCompanyCommand>
{
    public SwitchActiveCompanyCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
    }
}
