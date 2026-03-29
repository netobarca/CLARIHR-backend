using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Application.Features.Platform.Common;
using CLARIHR.Domain.Auth;
using FluentValidation;

namespace CLARIHR.Application.Features.Platform.Auth;

public sealed record PlatformLoginRequest(
    string Email,
    string Password);

public sealed record PlatformRefreshTokenRequest(string RefreshToken);

public sealed record PlatformLoginCommand(
    string Email,
    string Password) : ICommand<AuthResponse>;

public sealed record PlatformRefreshTokenCommand(string RefreshToken) : ICommand<AuthResponse>;

public sealed record PlatformLogoutCommand : ICommand<bool>;

internal sealed class PlatformLoginCommandValidator : AbstractValidator<PlatformLoginCommand>
{
    public PlatformLoginCommandValidator()
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

internal sealed class PlatformRefreshTokenCommandValidator : AbstractValidator<PlatformRefreshTokenCommand>
{
    public PlatformRefreshTokenCommandValidator()
    {
        RuleFor(command => command.RefreshToken)
            .NotEmpty()
            .MaximumLength(2048);
    }
}

internal sealed class PlatformLoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IPlatformOperatorRepository platformOperatorRepository,
    ITokenService tokenService) : ICommandHandler<PlatformLoginCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(PlatformLoginCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (user is null ||
            user.Status != UserStatus.Active ||
            user.AuthProvider != AuthProvider.Local ||
            !passwordHasher.Verify(command.Password, user.PasswordHash ?? string.Empty))
        {
            return Result<AuthResponse>.Failure(AuthErrors.InvalidCredentials);
        }

        var platformOperator = await platformOperatorRepository.GetByUserIdAsync(user.Id, cancellationToken);
        if (platformOperator is null || !platformOperator.IsActive)
        {
            return Result<AuthResponse>.Failure(PlatformAccessErrors.Forbidden);
        }

        var tokenResult = await tokenService.GeneratePlatformAsync(user, cancellationToken);
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

internal sealed class PlatformRefreshTokenCommandHandler(
    ITokenService tokenService,
    IPlatformOperatorRepository platformOperatorRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<PlatformRefreshTokenCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(PlatformRefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var tokenResult = await tokenService.RefreshAsync(command.RefreshToken, AuthClientType.Platform, cancellationToken);
        if (tokenResult.IsFailure)
        {
            return Result<AuthResponse>.Failure(tokenResult.Error);
        }

        var user = tokenResult.Value.User;
        var platformOperator = await platformOperatorRepository.GetByUserIdAsync(user.Id, cancellationToken);
        if (platformOperator is null || !platformOperator.IsActive)
        {
            await refreshTokenRepository.RevokeUserTokensAsync(
                user.Id,
                AuthClientType.Platform,
                dateTimeProvider.UtcNow,
                "platform-access-revoked",
                cancellationToken);
            await refreshTokenRepository.SaveChangesAsync(cancellationToken);

            return Result<AuthResponse>.Failure(PlatformAccessErrors.Forbidden);
        }

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

internal sealed class PlatformLogoutCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<PlatformLogoutCommand, bool>
{
    public async Task<Result<bool>> Handle(PlatformLogoutCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUserService.UserId, out var userPublicId))
        {
            return Result<bool>.Failure(AuthErrors.InvalidCurrentUser);
        }

        var user = await userRepository.GetByPublicIdAsync(userPublicId, cancellationToken);
        if (user is null)
        {
            return Result<bool>.Failure(AuthErrors.InvalidCurrentUser);
        }

        await refreshTokenRepository.RevokeUserTokensAsync(
            user.Id,
            AuthClientType.Platform,
            dateTimeProvider.UtcNow,
            "logout",
            cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
