using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Domain.Auth;
using FluentValidation;

namespace CLARIHR.Application.Features.Auth.RefreshToken;

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthResponse>;

internal sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(command => command.RefreshToken)
            .NotEmpty()
            .MaximumLength(2048);
    }
}

internal sealed class RefreshTokenCommandHandler(
    ITokenService tokenService) : ICommandHandler<RefreshTokenCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var tokenResult = await tokenService.RefreshAsync(command.RefreshToken, AuthClientType.Core, cancellationToken);
        if (tokenResult.IsFailure)
        {
            return Result<AuthResponse>.Failure(tokenResult.Error);
        }

        var user = tokenResult.Value.User;
        var tokens = tokenResult.Value.Tokens;

        return Result<AuthResponse>.Success(new AuthResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresIn,
            new UserDto(
                user.PublicId,
                user.Email,
                user.FirstName,
                user.LastName,
                user.AuthProvider)));
    }
}
