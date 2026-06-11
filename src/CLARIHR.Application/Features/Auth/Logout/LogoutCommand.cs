using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Features.Auth.Logout;

public sealed record LogoutCommand : ICommand<bool>;

internal sealed class LogoutCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPlatformAuditService platformAuditService,
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

        // AU-6: revoke the user's refresh tokens across BOTH client types so logout ends every session
        // (mirrors password-reset). A user with a core + platform session is fully logged out.
        var utcNow = dateTimeProvider.UtcNow;
        await refreshTokenRepository.RevokeUserTokensAsync(user.Id, AuthClientType.Core, utcNow, "logout", cancellationToken);
        await refreshTokenRepository.RevokeUserTokensAsync(user.Id, AuthClientType.Platform, utcNow, "logout", cancellationToken);

        await platformAuditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.UserLoggedOut,
                AuditEntityTypes.User,
                user.PublicId,
                user.Email,
                AuditActions.Logout,
                $"User {user.Email} logged out."),
            cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
