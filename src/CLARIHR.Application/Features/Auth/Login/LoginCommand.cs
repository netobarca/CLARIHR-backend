using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Domain.Auth;
using FluentValidation;

namespace CLARIHR.Application.Features.Auth.Login;

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record LoginCommand(
    string Email,
    string Password) : ICommand<AuthResponse>;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(100);
    }
}

internal sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : ICommandHandler<LoginCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (user is null ||
            user.Status != UserStatus.Active ||
            user.AuthProvider != AuthProvider.Local ||
            !passwordHasher.Verify(command.Password, user.PasswordHash ?? string.Empty))
        {
            return Result<AuthResponse>.Failure(AuthErrors.InvalidCredentials);
        }

        var tokenResult = await tokenService.GenerateAsync(user, cancellationToken);
        if (tokenResult.IsFailure)
        {
            return Result<AuthResponse>.Failure(tokenResult.Error);
        }

        return Result<AuthResponse>.Success(new AuthResponse(
            tokenResult.Value.AccessToken,
            tokenResult.Value.RefreshToken,
            tokenResult.Value.ExpiresIn,
            new UserDto(
                user.PublicId,
                user.Email,
                user.FirstName,
                user.LastName,
                user.AuthProvider)));
    }
}
