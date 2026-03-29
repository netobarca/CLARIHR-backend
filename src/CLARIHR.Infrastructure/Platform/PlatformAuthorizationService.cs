using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Platform.Common;
using CLARIHR.Domain.Platform;

namespace CLARIHR.Infrastructure.Platform;

internal sealed class PlatformAuthorizationService(
    ICurrentUserService currentUserService,
    IPlatformOperatorRepository platformOperatorRepository) : IPlatformAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(manageRequired: false, cancellationToken);

    public Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(manageRequired: true, cancellationToken);

    private async Task<Result> EnsureAuthorizedAsync(bool manageRequired, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !Guid.TryParse(currentUserService.UserId, out var userPublicId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        var platformOperator = await platformOperatorRepository.GetActiveByUserPublicIdAsync(userPublicId, cancellationToken);
        if (platformOperator is null)
        {
            return Result.Failure(PlatformAccessErrors.Forbidden);
        }

        if (!manageRequired || platformOperator.Role == PlatformOperatorRole.Admin)
        {
            return Result.Success();
        }

        return Result.Failure(PlatformAccessErrors.Forbidden);
    }
}
