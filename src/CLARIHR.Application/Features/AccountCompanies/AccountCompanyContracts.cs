using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Domain.Companies;
using FluentValidation;

namespace CLARIHR.Application.Features.AccountCompanies;

public sealed record AccountCompanySummaryResponse(
    Guid CompanyId,
    string Name,
    string Slug,
    CompanyStatus Status,
    string PlanCode,
    bool IsActiveContext,
    bool IsOwnedByCurrentUser,
    DateTime CreatedAtUtc);

public sealed record AccountCompanyDetailResponse(
    Guid CompanyId,
    string Name,
    string Slug,
    CompanyStatus Status,
    string PlanCode,
    bool IsActiveContext,
    bool IsOwnedByCurrentUser,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record ActiveCompanyDto(
    Guid CompanyId,
    string Name,
    string Slug,
    CompanyStatus Status);

public sealed record SwitchActiveCompanyResponse(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    ActiveCompanyDto ActiveCompany);

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
    int PageSize = 20) : IQuery<PagedResponse<AccountCompanySummaryResponse>>;

public sealed record GetOwnedCompanyByIdQuery(Guid CompanyId) : IQuery<AccountCompanyDetailResponse>;

public sealed record CreateAccountCompanyCommand(string Name) : ICommand<AccountCompanyDetailResponse>;

public sealed record UpdateAccountCompanyCommand(Guid CompanyId, string Name) : ICommand<AccountCompanyDetailResponse>;

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
