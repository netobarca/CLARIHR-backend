using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.IdentityAccess;

public interface IIamAdministrationAuthorizationService
{
    Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken);

    Task<Result> EnsureAuthorizedAsync(
        RbacPermissionScreen screen,
        RbacPermissionAction action,
        CancellationToken cancellationToken);
}
