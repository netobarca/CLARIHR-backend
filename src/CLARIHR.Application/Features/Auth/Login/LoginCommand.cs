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
    // AU-5: a fixed dummy hash, computed once (lazily) via the injected hasher, lets login always pay one
    // full password verification — even for a non-existent / inactive / external account — so the response
    // time does not reveal whether the email maps to an active local account (timing-based enumeration).
    private const string TimingEqualizationPassword = "timing-equalization-placeholder-not-a-credential";
    private static string? _dummyPasswordHash;

    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);

        // Always run exactly one PBKDF2 verification regardless of account state (timing equalization). A
        // non-eligible account (missing / inactive / external / no password) is checked against the dummy hash
        // so the cryptographic cost — and therefore the response time — is identical on every path.
        var comparisonHash = user is { AuthProvider: AuthProvider.Local, PasswordHash: { Length: > 0 } passwordHash }
            ? passwordHash
            : _dummyPasswordHash ??= passwordHasher.Hash(TimingEqualizationPassword);
        var passwordMatches = passwordHasher.Verify(command.Password, comparisonHash);

        if (user is null ||
            user.Status != UserStatus.Active ||
            user.AuthProvider != AuthProvider.Local ||
            !passwordMatches)
        {
            return Result<AuthResponse>.Failure(AuthErrors.InvalidCredentials);
        }

        var tokenResult = await tokenService.GenerateLoginAsync(user, cancellationToken);
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
