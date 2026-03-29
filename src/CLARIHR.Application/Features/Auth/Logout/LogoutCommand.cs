using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Features.Auth.Logout;

public sealed record LogoutCommand : ICommand<bool>;

internal sealed class LogoutCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<LogoutCommand, bool>
{
    public async Task<Result<bool>> Handle(LogoutCommand command, CancellationToken cancellationToken)
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
            AuthClientType.Core,
            dateTimeProvider.UtcNow,
            "logout",
            cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
