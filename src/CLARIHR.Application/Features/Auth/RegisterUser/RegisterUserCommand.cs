using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Domain.Auth;
using FluentValidation;

namespace CLARIHR.Application.Features.Auth.RegisterUser;

public sealed record RegisterUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? Country,
    string? Source) : ICommand<AuthResponse>;

internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
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

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(100)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(command => command.Country)
            .MaximumLength(100)
            .Must(AuthValidationRules.BeValidCountry)
            .WithMessage("Country contains invalid characters.")
            .When(static command => !string.IsNullOrWhiteSpace(command.Country));

        RuleFor(command => command.Source)
            .MaximumLength(100)
            .Must(AuthValidationRules.BeValidSource)
            .WithMessage("Source contains invalid characters.")
            .When(static command => !string.IsNullOrWhiteSpace(command.Source));
    }
}

internal sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : ICommandHandler<RegisterUserCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var existingUser = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (existingUser is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<AuthResponse>.Failure(AuthErrors.UserAlreadyExists);
        }

        var passwordHash = passwordHasher.Hash(command.Password);
        var user = User.RegisterLocal(
            command.FirstName,
            command.LastName,
            command.Email,
            passwordHash,
            command.Country,
            command.Source);

        await userRepository.AddAsync(user, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var tokenResult = await tokenService.GenerateAsync(user, cancellationToken);
        if (tokenResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<AuthResponse>.Failure(tokenResult.Error);
        }

        await transaction.CommitAsync(cancellationToken);

        var response = new AuthResponse(
            tokenResult.Value.AccessToken,
            tokenResult.Value.RefreshToken,
            tokenResult.Value.ExpiresIn,
            new UserDto(
                user.PublicId,
                user.Email,
                user.FirstName,
                user.LastName,
                user.AuthProvider));

        return Result<AuthResponse>.Success(response);
    }
}
