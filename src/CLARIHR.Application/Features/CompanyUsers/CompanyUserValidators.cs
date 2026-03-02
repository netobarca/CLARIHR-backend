using CLARIHR.Application.Features.Auth.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.CompanyUsers;

internal sealed class GetCompanyUsersQueryValidator : AbstractValidator<GetCompanyUsersQuery>
{
    public GetCompanyUsersQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.RoleId)
            .NotEqual(Guid.Empty)
            .When(static query => query.RoleId.HasValue);

        RuleFor(query => query.Search)
            .MaximumLength(100)
            .When(static query => !string.IsNullOrWhiteSpace(query.Search));
    }
}

internal sealed class CreateCompanyUserCommandValidator : AbstractValidator<CreateCompanyUserCommand>
{
    public CreateCompanyUserCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(AuthValidationRules.BeValidPersonName)
            .WithMessage("First name contains invalid characters.");

        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(AuthValidationRules.BeValidPersonName)
            .WithMessage("Last name contains invalid characters.");

        RuleFor(command => command.RoleId)
            .NotEmpty();
    }
}

internal sealed class UpdateCompanyUserCommandValidator : AbstractValidator<UpdateCompanyUserCommand>
{
    public UpdateCompanyUserCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(AuthValidationRules.BeValidPersonName)
            .WithMessage("First name contains invalid characters.");

        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(AuthValidationRules.BeValidPersonName)
            .WithMessage("Last name contains invalid characters.");

        RuleFor(command => command.RoleId)
            .NotEmpty();
    }
}

internal sealed class DeactivateCompanyUserCommandValidator : AbstractValidator<DeactivateCompanyUserCommand>
{
    public DeactivateCompanyUserCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}

internal sealed class ReactivateCompanyUserCommandValidator : AbstractValidator<ReactivateCompanyUserCommand>
{
    public ReactivateCompanyUserCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}

internal sealed class ResetInvitationCommandValidator : AbstractValidator<ResetInvitationCommand>
{
    public ResetInvitationCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}
