using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.JobProfiles;

public interface IJobProfileAuthorizationService
{
    Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanManageProfilesAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>
    /// H-01 — gate of the state transitions (publish / reopen / archive). Requires
    /// <c>JobProfiles.Publish</c>, which <c>JobProfiles.Admin</c> deliberately does NOT imply, so the
    /// person who drafts a descriptor is not automatically the one who approves it.
    /// </summary>
    Task<Result> EnsureCanPublishProfilesAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanManageCatalogsAsync(Guid companyId, CancellationToken cancellationToken);

    Error TenantMismatch(RbacPermissionAction action);
}
