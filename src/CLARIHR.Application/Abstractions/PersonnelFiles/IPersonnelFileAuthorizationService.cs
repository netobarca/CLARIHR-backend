using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

public interface IPersonnelFileAuthorizationService
{
    Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>
    /// True when the caller may authorize the rehire of a "not rehireable" file (D-10). Requires
    /// the dedicated <c>PersonnelFiles.AuthorizeRehire</c> permission (or the IAM super-admin),
    /// deliberately NOT implied by <c>PersonnelFiles.Admin</c>, so a manager who can run rehires
    /// cannot also approve the override of a blocked file.
    /// </summary>
    Task<bool> HasRehireAuthorizationAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>
    /// Role gate for reading compensation (salary/ingresos/egresos) of any file in the tenant (D-16):
    /// the dedicated <c>PersonnelFiles.ViewCompensation</c> permission, or Admin / IAM super-admin.
    /// (Employees reading their OWN compensation are allowed by a separate self-service check.)
    /// </summary>
    // Fail-closed default so test doubles need not implement it; the production service overrides it.
    Task<Result> EnsureCanViewCompensationAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));

    Error TenantMismatch(RbacPermissionAction action);
}
