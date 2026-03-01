using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICompanyUserAuthorizationService
{
    Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken);

    Task<Result> EnsureAuthorizedAsync(RbacPermissionAction action, CancellationToken cancellationToken);
}
