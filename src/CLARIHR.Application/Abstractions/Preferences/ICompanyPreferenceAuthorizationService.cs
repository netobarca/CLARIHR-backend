using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.Preferences;

public interface ICompanyPreferenceAuthorizationService
{
    Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken);

    Error TenantMismatch(RbacPermissionAction action);
}
