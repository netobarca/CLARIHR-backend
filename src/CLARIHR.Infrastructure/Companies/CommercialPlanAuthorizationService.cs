using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.CommercialPlans.Common;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class CommercialPlanAuthorizationService(
    ICurrentUserService currentUserService) : ICommercialPlanAuthorizationService
{
    public Task<Result> EnsurePlatformAdministrationAsync(CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));
        }

        return currentUserService.Roles.Contains(CommercialPlanPermissionCodes.PlatformAdminRole, StringComparer.OrdinalIgnoreCase)
            ? Task.FromResult(Result.Success())
            : Task.FromResult(Result.Failure(CommercialPlanErrors.Forbidden));
    }
}
